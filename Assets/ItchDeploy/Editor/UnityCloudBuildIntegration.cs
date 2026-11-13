using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace ItchDeploy.Editor
{
    [InitializeOnLoad]
    public static class UnityCloudBuildIntegration
    {
        public enum CloudSetupFailure
        {
            None,
            PackageMissing,
            ProjectNotLinked,
            UnitySessionMissing,
            NoBuildTargets,
            SourceControlMissing,
            BillingUnavailable,
            UnsupportedPlatform,
            TargetCreationFailed,
            LocalButlerKeyMissing,
            RemoteHookMissing,
            RemoteSecretMissing,
            RequestFailed,
        }

        public sealed class CloudSetupResult
        {
            public bool verified;
            public bool changed;
            public int targetCount;
            public int hookReadyCount;
            public int secretReadyCount;
            public string billingPlan;
            public CloudSetupFailure failure;
            public string error;
        }

        public enum CloudBuildStartFailure
        {
            None,
            PackageMissing,
            ProjectNotLinked,
            UnitySessionMissing,
            UnsupportedPlatform,
            TargetMissing,
            BillingUnavailable,
            RequestFailed,
        }

        [Serializable]
        public sealed class CloudBuildStartResult
        {
            public bool started;
            public string targetId;
            public string targetName;
            public int buildNumber;
            public string buildStatus;
            public CloudBuildStartFailure failure;
            public string error;
        }

        public enum HookPublishFailure
        {
            None,
            RepositoryMissing,
            DetachedHead,
            UnpushedCommitsPresent,
            StageFailed,
            CommitFailed,
            PushFailed,
            VerificationFailed,
        }

        public sealed class HookPublishResult
        {
            public bool published;
            public bool committed;
            public string branch;
            public string repositoryUrl;
            public HookPublishFailure failure;
            public string error;
        }

        internal sealed class CloudTargetSnapshot
        {
            public string id;
            public string name;
            public string platform;
            public bool enabled;
        }

        internal sealed class CloudOperatingSystemSnapshot
        {
            public string name;
            public string value;
            public string family;
            public bool hidden;
            public bool deprecated;
            public string[] unityVersions = Array.Empty<string>();
        }

        public const string PackageId = "com.unity.services.cloud-build";
        public const string HookRelativePath = "ci/itchdeploy-uba-post-build.sh";
        public const string RequiredSecret = "BUTLER_API_KEY";
        public const string ProjectSettingsPath =
            "Project/Services/Build Automation";

        private const string BuildApiUrl = "https://build-api.cloud.unity3d.com/api/v1";
        private const string DashboardUrl = "https://cloud.unity.com/";
        private const string DashboardProjectUrl =
            "https://cloud.unity.com/organizations/{0}/projects/{1}/cloud-build/config";
        private const string SetupGuide =
            "https://docs.unity.com/en-us/build-automation/build-automation-editor-package";
        private const string PendingSetupKey = "ItchDeploy_UBA_PendingSetup";
        private const string PendingStartedKey = "ItchDeploy_UBA_PendingStarted";
        private const string LastBillingFailureKeyPrefix =
            "ItchDeploy_UBA_LastBillingFailure_";

        private static AddRequest _installRequest;
        private static bool _legacySetupRunning;
        private static CloudSetupResult _legacySetupResult;
        private static double _publishedHookCheckedAt = double.NegativeInfinity;
        private static string _publishedHookExpected = string.Empty;
        private static bool _publishedHookCurrent;
        private static string _publishedHookIssue = string.Empty;

        static UnityCloudBuildIntegration()
        {
            if (SessionState.GetBool(PendingSetupKey, false))
            {
                EditorApplication.update -= PollPackageInstall;
                EditorApplication.update += PollPackageInstall;
            }
        }

        public static string HookAbsolutePath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", HookRelativePath));

        public static bool IsPackageInstalled
        {
            get
            {
                try
                {
                    if (UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                        .Any(package => package.name == PackageId))
                        return true;
                }
                catch { }

                try
                {
                    var manifest = Path.GetFullPath(Path.Combine(
                        Application.dataPath,
                        "..",
                        "Packages",
                        "manifest.json"));
                    return File.Exists(manifest) &&
                           File.ReadAllText(manifest).Contains(
                               "\"" + PackageId + "\"",
                               StringComparison.Ordinal);
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool IsInstallInProgress =>
            (_installRequest != null && !_installRequest.IsCompleted) ||
            SessionState.GetBool(PendingSetupKey, false);

        public static bool IsProjectLinked =>
            !string.IsNullOrWhiteSpace(CloudProjectSettings.projectId);

        public static bool IsDeployHookCurrent(
            ItchDeployConfig.ConfigData config,
            bool deployToItch)
        {
            try
            {
                return File.Exists(HookAbsolutePath) &&
                       string.Equals(
                           NormalizeNewlines(File.ReadAllText(HookAbsolutePath)),
                           GeneratePostBuildScript(config, deployToItch),
                           StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsDeployHookPublished(
            ItchDeployConfig.ConfigData config,
            bool deployToItch,
            out string issue)
        {
            if (config == null || !IsDeployHookCurrent(config, deployToItch))
            {
                issue = "Generate or update the Butler hook first.";
                return false;
            }

            var expected = GeneratePostBuildScript(config, deployToItch);
            if (string.Equals(_publishedHookExpected, expected, StringComparison.Ordinal) &&
                EditorApplication.timeSinceStartup - _publishedHookCheckedAt < 5d)
            {
                issue = _publishedHookIssue;
                return _publishedHookCurrent;
            }

            _publishedHookExpected = expected;
            _publishedHookCheckedAt = EditorApplication.timeSinceStartup;
            _publishedHookCurrent = false;
            _publishedHookIssue = string.Empty;

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!TryRunGit(
                    projectRoot,
                    "rev-parse --abbrev-ref --symbolic-full-name \"@{u}\"",
                    out var upstream,
                    out _))
            {
                _publishedHookIssue =
                    "The current branch has no tracked remote branch. Commit and push the generated hook.";
                issue = _publishedHookIssue;
                return false;
            }

            upstream = upstream.Trim();
            var gitObject = upstream + ":" + HookRelativePath;
            if (string.IsNullOrWhiteSpace(upstream) ||
                !TryRunGit(
                    projectRoot,
                    "show " + QuoteCommandLineArgument(gitObject),
                    out var publishedHook,
                    out _))
            {
                _publishedHookIssue =
                    "The generated hook is not present on the tracked remote branch. Commit and push it.";
                issue = _publishedHookIssue;
                return false;
            }

            _publishedHookCurrent = IsPublishedHookContentCurrent(
                publishedHook,
                config,
                deployToItch);
            _publishedHookIssue = _publishedHookCurrent
                ? string.Empty
                : "The tracked remote branch contains an outdated Butler hook. Commit and push the current file.";
            issue = _publishedHookIssue;
            return _publishedHookCurrent;
        }

        internal static bool IsPublishedHookContentCurrent(
            string publishedHook,
            ItchDeployConfig.ConfigData config,
            bool deployToItch)
        {
            if (config == null || string.IsNullOrWhiteSpace(publishedHook))
                return false;

            return string.Equals(
                NormalizeNewlines(publishedHook).TrimEnd('\n'),
                GeneratePostBuildScript(config, deployToItch).TrimEnd('\n'),
                StringComparison.Ordinal);
        }

        public static async Task<HookPublishResult> PublishDeployHookAsync(
            ItchDeployConfig.ConfigData config,
            bool deployToItch)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            WritePostBuildHook(config, deployToItch);
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            GitHubActionsIntegration.TryGetRepository(out _, out var repositoryUrl);
            var expectedHook = GeneratePostBuildScript(config, deployToItch);
            var result = await Task.Run(() => PublishDeployHook(
                projectRoot,
                expectedHook,
                repositoryUrl));

            _publishedHookCheckedAt = double.NegativeInfinity;
            _publishedHookExpected = string.Empty;
            return result;
        }

        public static string BuildHookRepositoryUrl(string repositoryUrl, string branch)
        {
            if (string.IsNullOrWhiteSpace(repositoryUrl))
                return string.Empty;

            var normalizedRepository = repositoryUrl.Trim().TrimEnd('/');
            var normalizedBranch = string.IsNullOrWhiteSpace(branch)
                ? "main"
                : branch.Trim();
            return normalizedRepository + "/new/" +
                   Uri.EscapeDataString(normalizedBranch) +
                   "?filename=" + Uri.EscapeDataString(HookRelativePath);
        }

        private static HookPublishResult PublishDeployHook(
            string projectRoot,
            string expectedHook,
            string repositoryUrl)
        {
            var result = new HookPublishResult
            {
                repositoryUrl = repositoryUrl ?? string.Empty,
            };
            if (!TryRunGit(
                    projectRoot,
                    "rev-parse --show-toplevel",
                    out _,
                    out var repositoryError))
            {
                result.failure = HookPublishFailure.RepositoryMissing;
                result.error = CompactGitError(repositoryError, "Git repository was not found.");
                return result;
            }

            if (!TryRunGit(
                    projectRoot,
                    "symbolic-ref --quiet --short HEAD",
                    out var branch,
                    out var branchError))
            {
                result.failure = HookPublishFailure.DetachedHead;
                result.error = CompactGitError(branchError, "Check out a branch before publishing the hook.");
                return result;
            }

            result.branch = branch.Trim();
            var hasUpstream = TryRunGit(
                projectRoot,
                "rev-parse --abbrev-ref --symbolic-full-name \"@{u}\"",
                out var upstream,
                out _);
            upstream = upstream.Trim();
            if (hasUpstream)
            {
                if (!TryRunGit(
                        projectRoot,
                        "diff --name-only " + QuoteCommandLineArgument(upstream + "..HEAD"),
                        out var aheadPaths,
                        out var aheadError))
                {
                    result.failure = HookPublishFailure.RepositoryMissing;
                    result.error = CompactGitError(aheadError, "Could not inspect the current Git branch.");
                    return result;
                }

                var unrelatedAheadPaths = SplitGitLines(aheadPaths)
                    .Where(path => !string.Equals(
                        NormalizeCloudPath(path),
                        HookRelativePath,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (unrelatedAheadPaths.Length > 0)
                {
                    result.failure = HookPublishFailure.UnpushedCommitsPresent;
                    result.error =
                        "The branch already contains unpublished commits for other files. " +
                        "Push or move them first so ItchDeploy does not publish unrelated work.";
                    return result;
                }
            }
            else if (!TryRunGit(
                         projectRoot,
                         "remote get-url origin",
                         out _,
                         out var remoteError))
            {
                result.failure = HookPublishFailure.RepositoryMissing;
                result.error = CompactGitError(remoteError, "Git remote 'origin' is not configured.");
                return result;
            }

            TryRunGit(
                projectRoot,
                "diff --cached --name-only -- " + QuoteCommandLineArgument(HookRelativePath),
                out var stagedBefore,
                out _);
            var hookWasStaged = SplitGitLines(stagedBefore).Any();
            if (!TryRunGit(
                    projectRoot,
                    "add -- " + QuoteCommandLineArgument(HookRelativePath),
                    out _,
                    out var stageError))
            {
                result.failure = HookPublishFailure.StageFailed;
                result.error = CompactGitError(stageError, "Could not stage the Butler hook.");
                return result;
            }

            if (!TryRunGit(
                    projectRoot,
                    "diff --cached --name-only -- " + QuoteCommandLineArgument(HookRelativePath),
                    out var stagedHook,
                    out var stagedError))
            {
                result.failure = HookPublishFailure.StageFailed;
                result.error = CompactGitError(stagedError, "Could not inspect the staged Butler hook.");
                return result;
            }

            if (SplitGitLines(stagedHook).Any())
            {
                var commitArguments =
                    "commit --only --no-gpg-sign -m " +
                    QuoteCommandLineArgument("chore: update ItchDeploy Build Automation hook") +
                    " -- " + QuoteCommandLineArgument(HookRelativePath);
                if (!TryRunGit(
                        projectRoot,
                        commitArguments,
                        out _,
                        out var commitError,
                        timeoutMilliseconds: 15000,
                        disablePrompt: true))
                {
                    if (!hookWasStaged)
                    {
                        TryRunGit(
                            projectRoot,
                            "reset -q HEAD -- " + QuoteCommandLineArgument(HookRelativePath),
                            out _,
                            out _);
                    }

                    result.failure = HookPublishFailure.CommitFailed;
                    result.error = CompactGitError(commitError, "Could not commit the Butler hook.");
                    return result;
                }

                result.committed = true;
            }

            var pushArguments = hasUpstream
                ? "push"
                : "push -u origin " + QuoteCommandLineArgument(result.branch);
            if (!TryRunGit(
                    projectRoot,
                    pushArguments,
                    out _,
                    out var pushError,
                    timeoutMilliseconds: 30000,
                    disablePrompt: true))
            {
                result.failure = HookPublishFailure.PushFailed;
                result.error = CompactGitError(pushError, "Could not push the Butler hook.");
                return result;
            }

            var publishedRef = hasUpstream ? upstream : "origin/" + result.branch;
            if (!TryRunGit(
                    projectRoot,
                    "show " + QuoteCommandLineArgument(publishedRef + ":" + HookRelativePath),
                    out var publishedHook,
                    out var verificationError) ||
                !string.Equals(
                    NormalizeNewlines(publishedHook).TrimEnd('\n'),
                    expectedHook.TrimEnd('\n'),
                    StringComparison.Ordinal))
            {
                result.failure = HookPublishFailure.VerificationFailed;
                result.error = CompactGitError(
                    verificationError,
                    "Git push finished, but the remote hook could not be verified.");
                return result;
            }

            result.published = true;
            result.failure = HookPublishFailure.None;
            return result;
        }

        public static async Task<CloudSetupResult> CheckCloudReadinessAsync(
            bool deployToItch)
        {
            return await InspectCloudAsync(deployToItch, configure: false);
        }

        public static async Task<CloudSetupResult> ConfigureAndVerifyCloudAsync(
            ItchDeployConfig.ConfigData config,
            bool deployToItch)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            WritePostBuildHook(config, deployToItch);
            return await InspectCloudAsync(deployToItch, configure: true);
        }

        public static async Task<CloudBuildStartResult> StartCloudBuildAsync(
            BuildTarget activeTarget,
            bool clean = false)
        {
            var result = new CloudBuildStartResult();
            if (!IsPackageInstalled)
            {
                result.failure = CloudBuildStartFailure.PackageMissing;
                return result;
            }

            if (!IsProjectLinked)
            {
                result.failure = CloudBuildStartFailure.ProjectNotLinked;
                return result;
            }

            if (string.IsNullOrWhiteSpace(CloudProjectSettings.accessToken))
            {
                result.failure = CloudBuildStartFailure.UnitySessionMissing;
                return result;
            }

            if (!TryGetCloudPlatform(activeTarget, out var cloudPlatform))
            {
                result.failure = CloudBuildStartFailure.UnsupportedPlatform;
                result.error = PlatformChannelMap.GetDisplayName(activeTarget);
                return result;
            }

            try
            {
                var targetsJson = await SendJsonAsync(
                    BuildTargetsUrl(),
                    UnityWebRequest.kHttpVerbGET);
                var target = SelectCloudTargetForPlatform(
                    ParseBuildTargets(targetsJson),
                    cloudPlatform);
                if (target == null)
                {
                    result.failure = CloudBuildStartFailure.TargetMissing;
                    result.error = PlatformChannelMap.GetDisplayName(activeTarget);
                    return result;
                }

                var responseJson = await SendJsonAsync(
                    BuildTargetBuildsUrl(target.id),
                    UnityWebRequest.kHttpVerbPOST,
                    BuildCloudBuildPayload(clean));
                result = ParseCloudBuildStartResponse(
                    responseJson,
                    target.id,
                    target.name);
                if (!result.started && result.failure == CloudBuildStartFailure.None)
                    result.failure = CloudBuildStartFailure.RequestFailed;
                return result;
            }
            catch (Exception ex)
            {
                result.error = SanitizeApiError(ex.Message);
                result.failure = ClassifyBuildStartFailure(result.error);
                return result;
            }
        }

        public static string PrepareProject(
            ItchDeployConfig.ConfigData config,
            bool deployToItch)
        {
            WritePostBuildHook(config, deployToItch);

            if (IsPackageInstalled)
            {
                return "Build Automation package and itch.io hook are ready.";
            }

            if (IsInstallInProgress)
                return "Build Automation package installation is already running.";

            SessionState.SetBool(PendingSetupKey, true);
            SessionState.SetFloat(
                PendingStartedKey,
                (float)EditorApplication.timeSinceStartup);
            _installRequest = Client.Add(PackageId);
            EditorApplication.update -= PollPackageInstall;
            EditorApplication.update += PollPackageInstall;
            return "Installing the official Build Automation package…";
        }

        public static string WritePostBuildHook(
            ItchDeployConfig.ConfigData config,
            bool deployToItch)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var directory = Path.GetDirectoryName(HookAbsolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(
                HookAbsolutePath,
                GeneratePostBuildScript(config, deployToItch),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return HookAbsolutePath;
        }

        internal static string GeneratePostBuildScript(
            ItchDeployConfig.ConfigData config,
            bool deployToItch)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var target = ShellSingleQuote(
                (config.itchUser ?? string.Empty) + "/" +
                (config.itchGame ?? string.Empty));
            var windows = ShellSingleQuote(GetChannel(config, "StandaloneWindows64", "windows"));
            var linux = ShellSingleQuote(GetChannel(config, "StandaloneLinux64", "linux"));
            var mac = ShellSingleQuote(GetChannel(config, "StandaloneOSX", "mac"));
            var web = ShellSingleQuote(GetChannel(config, "WebGL", "html5"));
            var android = ShellSingleQuote(GetChannel(config, "Android", "android"));
            var ios = ShellSingleQuote(GetChannel(config, "iOS", "ios"));
            var fallback = ShellSingleQuote(config.defaultChannel ?? "build");

            var sb = new StringBuilder();
            sb.AppendLine("#!/usr/bin/env bash");
            sb.AppendLine("set -euo pipefail");
            sb.AppendLine();
            sb.AppendLine("# Generated by ItchDeploy. Configure this repository-relative path as");
            sb.AppendLine("# the Unity Build Automation Post-Build Script and add BUTLER_API_KEY");
            sb.AppendLine("# as a protected environment variable on the build configuration.");
            sb.AppendLine("DEPLOY_TO_ITCH=" + (deployToItch ? "1" : "0"));
            sb.AppendLine("ITCH_TARGET=" + target);
            sb.AppendLine();
            sb.AppendLine("if [[ \"$DEPLOY_TO_ITCH\" != \"1\" ]]; then");
            sb.AppendLine("  echo \"ItchDeploy: cloud deployment is disabled; keeping the build artifact only.\"");
            sb.AppendLine("  exit 0");
            sb.AppendLine("fi");
            sb.AppendLine();
            sb.AppendLine(": \"${BUTLER_API_KEY:?Add BUTLER_API_KEY to the Build Automation environment variables}\"");
            sb.AppendLine(": \"${OUTPUT_DIRECTORY:?Unity Build Automation did not expose OUTPUT_DIRECTORY}\"");
            sb.AppendLine("echo \"::mask-value::$BUTLER_API_KEY\"");
            sb.AppendLine();
            sb.AppendLine("case \"${BUILDER_OS:-}\" in");
            sb.AppendLine("  WINDOWS) BUTLER_PLATFORM=windows-amd64; BUTLER_EXE=butler.exe ;;");
            sb.AppendLine("  MAC)     BUTLER_PLATFORM=darwin-amd64;  BUTLER_EXE=butler ;;");
            sb.AppendLine("  *)       BUTLER_PLATFORM=linux-amd64;   BUTLER_EXE=butler ;;");
            sb.AppendLine("esac");
            sb.AppendLine();
            sb.AppendLine("BUTLER_ROOT=\"${WORKSPACE:-${TMPDIR:-/tmp}}/.itchdeploy-butler\"");
            sb.AppendLine("mkdir -p \"$BUTLER_ROOT\"");
            sb.AppendLine("if [[ ! -f \"$BUTLER_ROOT/$BUTLER_EXE\" ]]; then");
            sb.AppendLine("  BASE_URL=\"https://broth.itch.zone/butler/$BUTLER_PLATFORM\"");
            sb.AppendLine("  VERSION=\"$(curl --fail --silent --show-error --location \"$BASE_URL/LATEST\")\"");
            sb.AppendLine("  curl --fail --silent --show-error --location \\");
            sb.AppendLine("    \"$BASE_URL/$VERSION/archive/default\" --output \"$BUTLER_ROOT/butler.zip\"");
            sb.AppendLine("  unzip -q -o \"$BUTLER_ROOT/butler.zip\" -d \"$BUTLER_ROOT\"");
            sb.AppendLine("  chmod +x \"$BUTLER_ROOT/$BUTLER_EXE\" || true");
            sb.AppendLine("fi");
            sb.AppendLine();
            sb.AppendLine("PLATFORM=\"$(printf '%s' \"${BUILD_PLATFORM:-${BUILDCFG_TARGET:-build}}\" | tr '[:upper:]' '[:lower:]')\"");
            sb.AppendLine("case \"$PLATFORM\" in");
            sb.AppendLine("  *webgl*)                 CHANNEL=" + web + " ;;");
            sb.AppendLine("  *windows*|*win64*)       CHANNEL=" + windows + " ;;");
            sb.AppendLine("  *linux*)                 CHANNEL=" + linux + " ;;");
            sb.AppendLine("  *osx*|*mac*)             CHANNEL=" + mac + " ;;");
            sb.AppendLine("  *android*)               CHANNEL=" + android + " ;;");
            sb.AppendLine("  *ios*)                   CHANNEL=" + ios + " ;;");
            sb.AppendLine("  *)                       CHANNEL=" + fallback + " ;;");
            sb.AppendLine("esac");
            sb.AppendLine();
            sb.AppendLine("SOURCE_PATH=\"$OUTPUT_DIRECTORY\"");
            sb.AppendLine("if [[ \"${BUILDER_OS:-}\" == \"WINDOWS\" ]]; then");
            sb.AppendLine("  SOURCE_PATH=\"$(cygpath -wa \"$OUTPUT_DIRECTORY\")\"");
            sb.AppendLine("  BUTLER_PATH=\"$(cygpath -wa \"$BUTLER_ROOT/$BUTLER_EXE\")\"");
            sb.AppendLine("else");
            sb.AppendLine("  BUTLER_PATH=\"$BUTLER_ROOT/$BUTLER_EXE\"");
            sb.AppendLine("fi");
            sb.AppendLine();
            sb.AppendLine("VERSION_LABEL=\"${UCB_BUILD_NUMBER:-${BUILD_NUMBER:-cloud}}\"");
            sb.AppendLine("\"$BUTLER_PATH\" push \"$SOURCE_PATH\" \"$ITCH_TARGET:$CHANNEL\" --userversion \"$VERSION_LABEL\"");
            return NormalizeNewlines(sb.ToString());
        }

        private static async Task<CloudSetupResult> InspectCloudAsync(
            bool deployToItch,
            bool configure)
        {
            var result = new CloudSetupResult();
            if (!IsPackageInstalled)
            {
                result.failure = CloudSetupFailure.PackageMissing;
                return result;
            }

            if (!IsProjectLinked)
            {
                result.failure = CloudSetupFailure.ProjectNotLinked;
                return result;
            }

            if (string.IsNullOrWhiteSpace(CloudProjectSettings.accessToken))
            {
                result.failure = CloudSetupFailure.UnitySessionMissing;
                return result;
            }

            try
            {
                var billingJson = await SendJsonAsync(
                    BuildProjectBillingPlanUrl(),
                    UnityWebRequest.kHttpVerbGET);
                result.billingPlan = ParseEffectiveBillingPlan(billingJson);
                if (!IsCloudBuildAllowedForBillingPlan(result.billingPlan))
                {
                    result.failure = CloudSetupFailure.BillingUnavailable;
                    return result;
                }

                var targetsJson = await SendJsonAsync(BuildTargetsUrl(), UnityWebRequest.kHttpVerbGET);
                var targets = ParseBuildTargets(targetsJson)
                    .Where(target => target.enabled && !string.IsNullOrWhiteSpace(target.id))
                    .ToList();
                result.targetCount = targets.Count;
                var activeTarget = EditorUserBuildSettings.activeBuildTarget;
                if (!TryGetCloudPlatform(activeTarget, out var activeCloudPlatform))
                {
                    result.failure = CloudSetupFailure.UnsupportedPlatform;
                    result.error = PlatformChannelMap.GetDisplayName(activeTarget);
                    return result;
                }

                if (SelectCloudTargetForPlatform(targets, activeCloudPlatform) == null)
                {
                    var projectJson = await SendJsonAsync(
                        BuildProjectApiUrl(),
                        UnityWebRequest.kHttpVerbGET);
                    if (!HasConfiguredSourceControl(projectJson))
                    {
                        result.failure = CloudSetupFailure.SourceControlMissing;
                        return result;
                    }

                    if (!configure)
                    {
                        result.failure = CloudSetupFailure.NoBuildTargets;
                        return result;
                    }

                    try
                    {
                        var operatingSystemsJson = await SendJsonAsync(
                            BuildOperatingSystemVersionsUrl(),
                            UnityWebRequest.kHttpVerbGET);
                        var operatingSystem = SelectCloudOperatingSystem(
                            operatingSystemsJson,
                            activeTarget,
                            Application.unityVersion);
                        await SendJsonAsync(
                            BuildTargetsUrl(),
                            UnityWebRequest.kHttpVerbPOST,
                            BuildCreateTargetPayload(
                                activeTarget,
                                ResolveLocalBranch(),
                                projectJson,
                                operatingSystem.family,
                                operatingSystem.value));
                        result.changed = true;
                    }
                    catch (Exception ex)
                    {
                        result.failure = CloudSetupFailure.TargetCreationFailed;
                        result.error = SanitizeApiError(ex.Message);
                        return result;
                    }

                    targets = await WaitForCreatedTargetsAsync(activeCloudPlatform);
                    result.targetCount = targets.Count;
                    if (SelectCloudTargetForPlatform(targets, activeCloudPlatform) == null)
                    {
                        result.failure = CloudSetupFailure.TargetCreationFailed;
                        result.error = "Unity accepted the target, but it is not available yet. Retry the check in a few seconds.";
                        return result;
                    }
                }

                var localApiKey = deployToItch
                    ? ItchDeployCredentials.ResolveApiKey()
                    : string.Empty;

                foreach (var target in targets)
                {
                    var targetUrl = BuildTargetUrl(target.id);
                    var targetJson = await SendJsonAsync(
                        targetUrl,
                        UnityWebRequest.kHttpVerbGET);
                    var hookReady = IsRemoteHookCurrent(targetJson);
                    if (configure && !hookReady)
                    {
                        await SendJsonAsync(
                            targetUrl,
                            UnityWebRequest.kHttpVerbPUT,
                            BuildUpdatedTargetSettingsPayload(targetJson));
                        hookReady = true;
                        result.changed = true;
                    }

                    if (hookReady)
                        result.hookReadyCount++;

                    if (!deployToItch)
                    {
                        result.secretReadyCount++;
                        continue;
                    }

                    var environmentUrl = BuildTargetEnvironmentUrl(target.id);
                    var environmentJson = await SendJsonAsync(
                        environmentUrl,
                        UnityWebRequest.kHttpVerbGET);
                    var secretReady = HasRequiredSecret(environmentJson);
                    if (configure &&
                        !string.IsNullOrWhiteSpace(localApiKey) &&
                        !EnvironmentMatchesLocalApiKey(environmentJson, localApiKey))
                    {
                        await SendJsonAsync(
                            environmentUrl,
                            UnityWebRequest.kHttpVerbPUT,
                            BuildUpdatedEnvironmentPayload(environmentJson, localApiKey));
                        secretReady = true;
                        result.changed = true;
                    }

                    if (secretReady)
                        result.secretReadyCount++;
                }

                if (result.hookReadyCount != result.targetCount)
                {
                    result.failure = CloudSetupFailure.RemoteHookMissing;
                    return result;
                }

                if (deployToItch && result.secretReadyCount != result.targetCount)
                {
                    result.failure = configure && string.IsNullOrWhiteSpace(localApiKey)
                        ? CloudSetupFailure.LocalButlerKeyMissing
                        : CloudSetupFailure.RemoteSecretMissing;
                    return result;
                }

                result.verified = true;
                result.failure = CloudSetupFailure.None;
                return result;
            }
            catch (Exception ex)
            {
                result.failure = CloudSetupFailure.RequestFailed;
                result.error = SanitizeApiError(ex.Message);
                return result;
            }
        }

        internal static IReadOnlyList<CloudTargetSnapshot> ParseBuildTargets(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<CloudTargetSnapshot>();

            var array = MiniJson.Parse(json) as MiniJson.Array
                ?? throw new FormatException("Unity returned an invalid build-target list.");
            return array.Items
                .OfType<MiniJson.Object>()
                .Select(item => new CloudTargetSnapshot
                {
                    id = item.GetString("buildtargetid"),
                    name = item.GetString("name"),
                    platform = item.GetString("platform"),
                    enabled = item.GetBool("enabled"),
                })
                .ToArray();
        }

        internal static CloudTargetSnapshot SelectCloudTargetForPlatform(
            IEnumerable<CloudTargetSnapshot> targets,
            string cloudPlatform)
        {
            if (targets == null || string.IsNullOrWhiteSpace(cloudPlatform))
                return null;

            var expectedName = BuildCloudTargetIdentifier(cloudPlatform);
            return targets
                .Where(target =>
                    target != null &&
                    target.enabled &&
                    !string.IsNullOrWhiteSpace(target.id) &&
                    string.Equals(
                        target.platform,
                        cloudPlatform,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(target => string.Equals(
                    target.name,
                    expectedName,
                    StringComparison.OrdinalIgnoreCase))
                .ThenBy(target => target.name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        internal static string BuildCloudBuildPayload(bool clean) =>
            MiniJson.Serialize(new MiniJson.Object().Set("clean", clean));

        internal static CloudBuildStartResult ParseCloudBuildStartResponse(
            string json,
            string fallbackTargetId,
            string fallbackTargetName)
        {
            var result = new CloudBuildStartResult
            {
                targetId = fallbackTargetId ?? string.Empty,
                targetName = fallbackTargetName ?? string.Empty,
            };
            if (string.IsNullOrWhiteSpace(json))
            {
                result.failure = CloudBuildStartFailure.RequestFailed;
                result.error = "Unity returned an empty build response.";
                return result;
            }

            try
            {
                var token = MiniJson.Parse(json);
                var build = token is MiniJson.Array array
                    ? array.Items.OfType<MiniJson.Object>().FirstOrDefault()
                    : token as MiniJson.Object;
                if (build == null)
                {
                    result.failure = CloudBuildStartFailure.RequestFailed;
                    result.error = "Unity returned an invalid build response.";
                    return result;
                }

                result.targetId = build.GetString("buildtargetid", result.targetId);
                result.targetName = build.GetString("buildTargetName", result.targetName);
                result.buildNumber = (int)build.GetNumber("build");
                result.buildStatus = build.GetString("buildStatus");
                result.error = build.GetString("error");
                result.started = string.IsNullOrWhiteSpace(result.error) &&
                                 (result.buildNumber > 0 ||
                                  !string.IsNullOrWhiteSpace(result.buildStatus));
                result.failure = result.started
                    ? CloudBuildStartFailure.None
                    : ClassifyBuildStartFailure(result.error);
                return result;
            }
            catch (Exception ex)
            {
                result.failure = CloudBuildStartFailure.RequestFailed;
                result.error = SanitizeApiError(ex.Message);
                return result;
            }
        }

        internal static bool IsRemoteHookCurrent(string targetJson)
        {
            if (string.IsNullOrWhiteSpace(targetJson))
                return false;

            var root = ParseObject(targetJson);
            var configuredPath = GetStringAt(
                root,
                "settings",
                "advanced",
                "unity",
                "postBuildScript");
            return string.Equals(
                NormalizeCloudPath(configuredPath),
                NormalizeCloudPath(HookRelativePath),
                StringComparison.OrdinalIgnoreCase);
        }

        internal static bool HasRequiredSecret(string environmentJson)
        {
            if (string.IsNullOrWhiteSpace(environmentJson))
                return false;

            var environment = ParseObject(environmentJson);
            return !string.IsNullOrWhiteSpace(environment.GetString(RequiredSecret));
        }

        internal static string ParseEffectiveBillingPlan(string billingJson)
        {
            if (string.IsNullOrWhiteSpace(billingJson))
                return string.Empty;

            try
            {
                return GetStringAt(ParseObject(billingJson), "effective", "label").Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static bool IsCloudBuildAllowedForBillingPlan(string billingPlan)
        {
            if (string.IsNullOrWhiteSpace(billingPlan))
                return true;

            return !string.Equals(
                       billingPlan,
                       "personal",
                       StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(
                       billingPlan,
                       "teams basic",
                       StringComparison.OrdinalIgnoreCase);
        }

        internal static string BuildUpdatedTargetSettingsPayload(string targetJson)
        {
            var target = ParseObject(targetJson ?? "{}");
            var settings = target.GetObject("settings") ?? new MiniJson.Object();
            var advanced = GetOrCreateObject(settings, "advanced");
            var unity = GetOrCreateObject(advanced, "unity");
            unity.Set("postBuildScript", HookRelativePath);
            unity.Set("postBuildScriptFailsBuild", true);

            return MiniJson.Serialize(new MiniJson.Object().Set("settings", settings));
        }

        internal static bool TryGetCloudPlatform(BuildTarget target, out string platform)
        {
            platform = target switch
            {
                BuildTarget.WebGL => "webgl",
                BuildTarget.StandaloneWindows => "standalonewindows",
                BuildTarget.StandaloneWindows64 => "standalonewindows64",
                BuildTarget.StandaloneLinux64 => "standalonelinux64",
                BuildTarget.StandaloneOSX => "standaloneosx",
                BuildTarget.Android => "android",
                BuildTarget.iOS => "ios",
                BuildTarget.WSAPlayer => "wsaplayer",
                _ => string.Empty,
            };
            return platform.Length > 0;
        }

        internal static bool HasConfiguredSourceControl(string projectJson)
        {
            if (string.IsNullOrWhiteSpace(projectJson))
                return false;

            try
            {
                return !string.IsNullOrWhiteSpace(
                    GetStringAt(ParseObject(projectJson), "settings", "scm", "type"));
            }
            catch
            {
                return false;
            }
        }

        internal static string BuildCreateTargetPayload(
            BuildTarget target,
            string branch,
            string projectJson,
            string operatingSystemFamily,
            string operatingSystemVersion)
        {
            if (!TryGetCloudPlatform(target, out var platform))
            {
                throw new NotSupportedException(
                    "Unity Build Automation does not support the active platform: " + target);
            }

            var project = ParseObject(projectJson ?? "{}");
            var sourceControlType = GetStringAt(project, "settings", "scm", "type");
            if (string.IsNullOrWhiteSpace(sourceControlType))
            {
                throw new InvalidOperationException(
                    "Connect source control in Unity Cloud before creating a build target.");
            }

            if (operatingSystemFamily != "windows" && operatingSystemFamily != "mac")
            {
                throw new ArgumentException(
                    "Builder operating system family must be windows or mac.",
                    nameof(operatingSystemFamily));
            }

            if (!IsPathSafeIdentifier(operatingSystemVersion))
            {
                throw new ArgumentException(
                    "Builder operating system version is not a path-safe identifier.",
                    nameof(operatingSystemVersion));
            }

            var normalizedBranch = string.IsNullOrWhiteSpace(branch)
                ? "main"
                : branch.Trim();
            var targetIdentifier = BuildCloudTargetIdentifier(platform);
            var sourceControl = new MiniJson.Object()
                .Set("type", sourceControlType)
                .Set("branch", normalizedBranch);
            var unity = new MiniJson.Object()
                .Set("postBuildScript", HookRelativePath)
                .Set("postBuildScriptFailsBuild", true);
            var settings = new MiniJson.Object()
                .Set("autoBuild", false)
                .Set("autoBuildLatest", "inherit")
                .Set("unityVersion", NormalizeUnityVersion(Application.unityVersion))
                .Set("autoDetectUnityVersion", true)
                .Set("fallbackPatchVersion", true)
                .Set("executablename", targetIdentifier)
                .Set("operatingSystemSelected", operatingSystemFamily)
                .Set("operatingSystemVersion", operatingSystemVersion)
                .Set("scm", sourceControl)
                .Set("advanced", new MiniJson.Object().Set("unity", unity));

            return MiniJson.Serialize(new MiniJson.Object()
                .Set("name", targetIdentifier)
                .Set("platform", platform)
                .Set("targetType", "normal")
                .Set("enabled", true)
                .Set("settings", settings));
        }

        internal static string BuildCloudTargetIdentifier(string cloudPlatform)
        {
            if (!IsPathSafeIdentifier(cloudPlatform))
            {
                throw new ArgumentException(
                    "Cloud platform must contain only ASCII letters, digits, or hyphens.",
                    nameof(cloudPlatform));
            }

            return "itchdeploy-" + cloudPlatform.ToLowerInvariant();
        }

        internal static CloudOperatingSystemSnapshot SelectCloudOperatingSystem(
            string operatingSystemsJson,
            BuildTarget target,
            string unityVersion)
        {
            var desiredFamily = target == BuildTarget.iOS || target == BuildTarget.StandaloneOSX
                ? "mac"
                : "windows";
            var normalizedUnityVersion = NormalizeUnityVersion(unityVersion);
            var systemsArray = MiniJson.Parse(operatingSystemsJson ?? "[]") as MiniJson.Array
                ?? throw new FormatException("Unity returned an invalid operating-system list.");
            var systems = systemsArray.Items
                .OfType<MiniJson.Object>()
                .Select(item => new CloudOperatingSystemSnapshot
                {
                    name = item.GetString("name"),
                    value = item.GetString("value"),
                    family = item.GetString("family"),
                    hidden = item.GetBool("hidden"),
                    deprecated = item.GetBool("deprecated"),
                    unityVersions = item.GetArray("unity_versions") is { } versions
                        ? versions.Items
                            .OfType<MiniJson.Primitive>()
                            .Select(value => value.Value as string)
                            .Where(value => value != null)
                            .ToArray()
                        : Array.Empty<string>(),
                })
                .Where(system =>
                    system.family == desiredFamily &&
                    !system.hidden &&
                    !system.deprecated &&
                    IsPathSafeIdentifier(system.value) &&
                    system.unityVersions.Any(version => string.Equals(
                        version,
                        normalizedUnityVersion,
                        StringComparison.OrdinalIgnoreCase)))
                .OrderBy(system => system.name.IndexOf(
                    "EXPERIMENTAL",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenBy(system => system.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (systems.Count == 0)
            {
                throw new InvalidOperationException(
                    "Unity Build Automation has no current " + desiredFamily +
                    " builder for Unity " + normalizedUnityVersion + ".");
            }

            return systems[0];
        }

        private static bool IsPathSafeIdentifier(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.All(character =>
                IsAsciiLetterOrDigit(character) ||
                character == '-' ||
                character == '_');

        private static bool IsAsciiLetterOrDigit(char character) =>
            (character >= 'a' && character <= 'z') ||
            (character >= 'A' && character <= 'Z') ||
            (character >= '0' && character <= '9');

        internal static string NormalizeUnityVersion(string unityVersion) =>
            (unityVersion ?? string.Empty).Trim().Replace('.', '_');

        private static async Task<List<CloudTargetSnapshot>> WaitForCreatedTargetsAsync(
            string cloudPlatform)
        {
            const int attempts = 4;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                if (attempt > 0)
                    await Task.Delay(400);

                var targetsJson = await SendJsonAsync(
                    BuildTargetsUrl(),
                    UnityWebRequest.kHttpVerbGET);
                var targets = ParseBuildTargets(targetsJson)
                    .Where(target => target.enabled && !string.IsNullOrWhiteSpace(target.id))
                    .ToList();
                if (SelectCloudTargetForPlatform(targets, cloudPlatform) != null)
                    return targets;
            }

            return new List<CloudTargetSnapshot>();
        }

        private static string ResolveLocalBranch()
        {
            try
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var gitEntry = Path.Combine(projectRoot, ".git");
                var gitDirectory = Directory.Exists(gitEntry)
                    ? gitEntry
                    : ResolveGitDirectoryFromFile(projectRoot, gitEntry);
                if (string.IsNullOrWhiteSpace(gitDirectory))
                    return "main";

                var headPath = Path.Combine(gitDirectory, "HEAD");
                if (!File.Exists(headPath))
                    return "main";

                var head = File.ReadAllText(headPath).Trim();
                const string prefix = "ref: refs/heads/";
                return head.StartsWith(prefix, StringComparison.Ordinal)
                    ? head.Substring(prefix.Length)
                    : "main";
            }
            catch
            {
                return "main";
            }
        }

        private static string ResolveGitDirectoryFromFile(
            string projectRoot,
            string gitEntry)
        {
            if (!File.Exists(gitEntry))
                return string.Empty;

            var pointer = File.ReadAllText(gitEntry).Trim();
            const string prefix = "gitdir:";
            if (!pointer.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var path = pointer.Substring(prefix.Length).Trim();
            return Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(projectRoot, path));
        }

        internal static string BuildUpdatedEnvironmentPayload(
            string environmentJson,
            string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("An itch.io API key is required.", nameof(apiKey));

            var environment = ParseObject(environmentJson ?? "{}");
            environment.Set(RequiredSecret, apiKey);
            return MiniJson.Serialize(new MiniJson.Object().Set("envvars", environment));
        }

        private static bool EnvironmentMatchesLocalApiKey(
            string environmentJson,
            string apiKey)
        {
            if (string.IsNullOrWhiteSpace(environmentJson) ||
                string.IsNullOrWhiteSpace(apiKey))
                return false;

            var environment = ParseObject(environmentJson);
            return string.Equals(
                environment.GetString(RequiredSecret),
                apiKey,
                StringComparison.Ordinal);
        }

        private static MiniJson.Object ParseObject(string json)
            => MiniJson.Parse(json) as MiniJson.Object
               ?? throw new FormatException("Unity returned an invalid JSON object.");

        private static string GetStringAt(MiniJson.Object root, params string[] path)
        {
            if (root == null || path == null || path.Length == 0)
                return string.Empty;

            var current = root;
            for (var index = 0; index < path.Length - 1; index++)
            {
                current = current.GetObject(path[index]);
                if (current == null)
                    return string.Empty;
            }
            return current.GetString(path[path.Length - 1]);
        }

        private static MiniJson.Object GetOrCreateObject(
            MiniJson.Object parent,
            string propertyName)
        {
            var existing = parent.GetObject(propertyName);
            if (existing != null)
                return existing;

            var created = new MiniJson.Object();
            parent.Set(propertyName, created);
            return created;
        }

        private static async Task<string> SendJsonAsync(
            string url,
            string method,
            string json = null)
        {
            using var request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 30,
            };
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(
                    new UTF8Encoding(false).GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json;charset=utf-8");
            }

            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader(
                "AUTHORIZATION",
                "Bearer " + CloudProjectSettings.accessToken);

            var operation = request.SendWebRequest();
            if (!operation.isDone)
            {
                var completion = new TaskCompletionSource<bool>();
                operation.completed += _ => completion.TrySetResult(true);
                await completion.Task;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                var detail = string.IsNullOrWhiteSpace(request.downloadHandler?.text)
                    ? request.error
                    : request.downloadHandler.text;
                throw new InvalidOperationException(
                    "Unity Build Automation API returned HTTP " +
                    request.responseCode + ": " + SanitizeApiError(detail));
            }

            return request.downloadHandler?.text ?? string.Empty;
        }

        private static string BuildTargetsUrl() =>
            BuildProjectApiUrl() + "/buildtargets";

        private static string BuildProjectBillingPlanUrl() =>
            BuildProjectApiUrl() + "/billingplan";

        private static string BuildOperatingSystemVersionsUrl() =>
            BuildApiUrl + "/versions/operatingsystem";

        private static string BuildTargetUrl(string targetId) =>
            BuildTargetsUrl() + "/" + Uri.EscapeDataString(targetId ?? string.Empty);

        private static string BuildTargetEnvironmentUrl(string targetId) =>
            BuildTargetUrl(targetId) + "/envvars";

        private static string BuildTargetBuildsUrl(string targetId) =>
            BuildTargetUrl(targetId) + "/builds";

        private static string BuildProjectApiUrl() =>
            BuildApiUrl + "/orgs/" +
            Uri.EscapeDataString(CloudProjectSettings.organizationId ?? string.Empty) +
            "/projects/" +
            Uri.EscapeDataString(CloudProjectSettings.projectId ?? string.Empty);

        private static string NormalizeCloudPath(string value) =>
            (value ?? string.Empty).Trim().Replace('\\', '/').TrimStart('.', '/');

        private static string SanitizeApiError(string value)
        {
            var compact = (value ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();

            var localApiKey = ItchDeployCredentials.ResolveApiKey();
            if (!string.IsNullOrWhiteSpace(localApiKey))
                compact = compact.Replace(localApiKey, "[redacted]");

            var accessToken = SafeAccessToken();
            if (!string.IsNullOrWhiteSpace(accessToken))
                compact = compact.Replace(accessToken, "[redacted]");

            compact = RedactCredentialFields(compact);
            return compact.Length <= 240 ? compact : compact.Substring(0, 240) + "…";
        }

        private static string SafeAccessToken()
        {
            try { return CloudProjectSettings.accessToken; }
            catch { return null; }
        }

        internal static string RedactCredentialFields(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

            return System.Text.RegularExpressions.Regex.Replace(
                text,
                "\"(pass|password|token|secret|accessToken|apiKey|api_key)\"\\s*:\\s*\"(?:[^\"\\\\]|\\\\.)*\"",
                "\"$1\":\"[redacted]\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        internal static CloudBuildStartFailure ClassifyBuildStartFailure(string error)
        {
            return IsBillingUnavailableError(error)
                ? CloudBuildStartFailure.BillingUnavailable
                : CloudBuildStartFailure.RequestFailed;
        }

        internal static bool IsBillingUnavailableError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return false;

            var normalized = error.ToLowerInvariant();
            return normalized.Contains("billing denied") ||
                   normalized.Contains("notoptedin") ||
                   normalized.Contains("not opted in") ||
                   normalized.Contains("build minutes") ||
                   normalized.Contains("pay-as-you-go");
        }

        public static CloudBuildStartResult LoadLastBillingFailure(
            BuildTarget target)
        {
            var json = SessionState.GetString(
                BuildLastBillingFailureKey(target),
                string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var result = JsonUtility.FromJson<CloudBuildStartResult>(json);
                return result?.failure == CloudBuildStartFailure.BillingUnavailable
                    ? result
                    : null;
            }
            catch
            {
                return null;
            }
        }

        public static void RememberBuildStartResult(
            BuildTarget target,
            CloudBuildStartResult result)
        {
            SessionState.SetString(
                BuildLastBillingFailureKey(target),
                result?.failure == CloudBuildStartFailure.BillingUnavailable
                    ? JsonUtility.ToJson(result)
                    : string.Empty);
        }

        internal static string BuildLastBillingFailureKey(BuildTarget target)
        {
            var projectId = CloudProjectSettings.projectId ?? string.Empty;
            return LastBillingFailureKeyPrefix + projectId + "_" + (int)target;
        }

        private static bool TryRunGit(
            string workingDirectory,
            string arguments,
            out string stdout,
            out string stderr,
            int timeoutMilliseconds = 2000,
            bool disablePrompt = false)
        {
            stdout = string.Empty;
            stderr = string.Empty;
            try
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = arguments,
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    },
                };
                if (disablePrompt)
                    process.StartInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                if (!process.Start())
                    return false;

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try { process.Kill(); }
                    catch { }
                    process.WaitForExit(500);
                    Task.WaitAll(new Task[] { stdoutTask, stderrTask }, 500);
                    stderr = "Git command timed out.";
                    return false;
                }

                Task.WaitAll(stdoutTask, stderrTask);
                stdout = stdoutTask.Result;
                stderr = stderrTask.Result;
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                stderr = ex.Message;
                return false;
            }
        }

        private static string QuoteCommandLineArgument(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private static IEnumerable<string> SplitGitLines(string value) =>
            (value ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);

        private static string CompactGitError(string value, string fallback)
        {
            var compact = (value ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            if (string.IsNullOrWhiteSpace(compact))
                compact = fallback;
            return compact.Length <= 360 ? compact : compact.Substring(0, 360) + "…";
        }

        public static void OpenProjectSettings()
        {
            SettingsService.OpenProjectSettings(ProjectSettingsPath);
        }

        public static string DashboardConfigurationUrl =>
            BuildDashboardConfigurationUrl(
                ResolveDashboardOrganizationIdentifier(),
                CloudProjectSettings.projectId);

        internal static string BuildDashboardConfigurationUrl(
            string organizationIdentifier,
            string projectId)
        {
            if (string.IsNullOrWhiteSpace(organizationIdentifier) ||
                string.IsNullOrWhiteSpace(projectId))
                return DashboardUrl;

            return string.Format(
                DashboardProjectUrl,
                Uri.EscapeDataString(organizationIdentifier),
                Uri.EscapeDataString(projectId));
        }

        private static string ResolveDashboardOrganizationIdentifier()
        {
            return !string.IsNullOrWhiteSpace(CloudProjectSettings.organizationKey)
                ? CloudProjectSettings.organizationKey
                : CloudProjectSettings.organizationId;
        }

        public static void OpenDashboard() =>
            Application.OpenURL(DashboardConfigurationUrl);

        private static async void RunLegacySetup()
        {
            if (_legacySetupRunning)
                return;

            var config = ItchDeployConfig.Load();
            config.ci ??= new ItchDeployConfig.CiData();
            if (!IsPackageInstalled)
            {
                Debug.Log("[ItchDeploy] " + PrepareProject(
                    config,
                    config.ci.unityBuildAutomationDeploy));
                return;
            }

            if (!IsProjectLinked)
            {
                OpenProjectSettings();
                return;
            }

            _legacySetupRunning = true;
            try
            {
                _legacySetupResult = await ConfigureAndVerifyCloudAsync(
                    config,
                    config.ci.unityBuildAutomationDeploy);
                if (_legacySetupResult.failure == CloudSetupFailure.UnitySessionMissing)
                    OpenProjectSettings();
                else if (_legacySetupResult.failure == CloudSetupFailure.SourceControlMissing)
                    OpenDashboard();
            }
            finally
            {
                _legacySetupRunning = false;
            }
        }

        private static void PollPackageInstall()
        {
            if (IsPackageInstalled)
            {
                CompletePendingSetup();
                return;
            }

            if (_installRequest != null && _installRequest.IsCompleted)
            {
                if (_installRequest.Status == StatusCode.Failure)
                {
                    Debug.LogError(
                        "[ItchDeploy] Could not install Build Automation: " +
                        _installRequest.Error?.message);
                    CancelPendingSetup();
                }
                return;
            }

            var started = SessionState.GetFloat(PendingStartedKey, 0f);
            if (started > 0f && EditorApplication.timeSinceStartup - started > 180d)
            {
                Debug.LogError("[ItchDeploy] Build Automation package installation timed out.");
                CancelPendingSetup();
            }
        }

        private static void CompletePendingSetup()
        {
            CancelPendingSetup();
        }

        private static void CancelPendingSetup()
        {
            EditorApplication.update -= PollPackageInstall;
            SessionState.EraseBool(PendingSetupKey);
            SessionState.EraseFloat(PendingStartedKey);
            _installRequest = null;
        }

        private static string GetChannel(
            ItchDeployConfig.ConfigData config,
            string platform,
            string fallback)
        {
            return config.channels != null &&
                   config.channels.TryGetValue(platform, out var channel) &&
                   !string.IsNullOrWhiteSpace(channel)
                ? channel
                : fallback;
        }

        private static string ShellSingleQuote(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "'\"'\"'") + "'";
        }

        private static string NormalizeNewlines(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
        }
    }
}
