using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class BuildDeployer
    {
        private const double DuplicateBuildCooldownSeconds = 20d;
        private const string LastBuildFingerprintKey = "ItchDeploy_LastBuildFingerprint";
        private const string LastBuildVersionKey = "ItchDeploy_LastBuildVersion";
        private const string LastBuildDirectoryKey = "ItchDeploy_LastBuildDirectory";
        private const string LastBuildUtcTicksKey = "ItchDeploy_LastBuildUtcTicks";

        public static bool IsBuilding { get; private set; }
        public static bool IsDeploying { get; private set; }
        public static string CurrentStatus { get; private set; } = "";

        public static event Action OnBuildStart;
        public static event Action<BuildReport> OnBuildComplete;
        public static event Action<string> OnDeployStart;
        public static event Action<string, bool> OnDeployComplete;

        public static string BuildsRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));

        public static string GetBuildDirectory(BuildTarget target, string version)
        {
            var targetRoot = Path.Combine(BuildsRoot, target.ToString());
            return BuildNamer.Enabled
                ? Path.Combine(targetRoot, BuildNamer.Resolve(target.ToString(), version))
                : Path.Combine(targetRoot, version);
        }

        public static string FindMostRecentBuild(BuildTarget target)
        {
            var targetRoot = Path.Combine(BuildsRoot, target.ToString());
            if (!Directory.Exists(targetRoot))
                return null;

            if (LooksLikeBuildDirectory(targetRoot, target))
                return targetRoot;

            return Directory.GetDirectories(targetRoot)
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .FirstOrDefault(path => LooksLikeBuildDirectory(path, target));
        }

        public static bool BuildAndDeploy(
            BuildTarget target,
            string channel,
            string version,
            ItchDeploySettings settings,
            bool? deployAfterBuild = null)
        {
            if (IsBuilding || IsDeploying)
            {
                ReportBlockingError(
                    "Already building or deploying. Please wait.",
                    "Збірка або публікація вже виконується. Зачекайте.");
                return false;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                var operation = EditorApplication.isCompiling
                    ? "script compilation"
                    : "the Asset Database refresh";
                CurrentStatus = $"Waiting for {operation}...";
                Debug.LogWarning($"[ItchDeploy] Build was not started because Unity is busy with {operation}.");

                ReportBlockingError(
                    $"Unity is still busy with {operation}.\n\nWait until it finishes, then start the build again.",
                    EditorApplication.isCompiling
                        ? "Unity ще компілює скрипти.\n\nДочекайтеся завершення та запустіть збірку знову."
                        : "Unity ще оновлює базу ассетів.\n\nДочекайтеся завершення та запустіть збірку знову.");

                return false;
            }

            if (settings == null)
            {
                ReportBlockingError(
                    "Deployment settings are unavailable.",
                    "Налаштування публікації недоступні.");
                return false;
            }

            if (!BuildPlatformCatalog.TryValidateBuildConfiguration(
                    target,
                    out _,
                    out var buildSupportError))
            {
                ReportBlockingError(buildSupportError);
                return false;
            }

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                ReportBlockingError(
                    "No enabled scenes in Build Settings.",
                    "У Build Settings немає жодної увімкненої сцени.");
                return false;
            }

            if (SceneValidator.AutoValidate)
            {
                var sceneErrors = SceneValidator.Validate().Where(issue => !SceneValidator.IsWarning(issue)).ToList();
                if (sceneErrors.Count > 0)
                {
                    ReportBlockingError(
                        "Fix scene validation errors before building:\n\n" +
                        string.Join("\n", sceneErrors),
                        "Виправте помилки перевірки сцен перед збіркою:\n\n" +
                        string.Join("\n", sceneErrors));
                    return false;
                }
            }

            var shouldDeploy = deployAfterBuild ?? settings.autoDeployAfterBuild;
            if (shouldDeploy &&
                !BuildPlatformCatalog.CanDeployCurrentArtifactToItch(target, out var deploymentBlockReason))
            {
                ReportBlockingError(deploymentBlockReason);
                return false;
            }

            if (shouldDeploy && !TryValidateDeploySettings(settings, out var configurationError))
            {
                ReportBlockingError(configurationError);
                return false;
            }

            if (TryGetRecentlyCompletedBuild(
                    target,
                    channel,
                    out var recentVersion,
                    out var recentBuildDirectory))
            {
                Debug.LogWarning(
                    $"[ItchDeploy] Ignored a duplicate {target} build request received immediately after a successful build.");
                if (shouldDeploy)
                {
                    return DeployBuild(
                        recentBuildDirectory,
                        target,
                        channel,
                        recentVersion,
                        settings);
                }

                CurrentStatus = FormatSuccessfulBuildStatus(target, recentVersion);
                return true;
            }

            if (BackupManager.AutoBackup)
            {
                CurrentStatus = "Creating project backup...";
                var backupPath = BackupManager.RunBackup();
                if (string.IsNullOrWhiteSpace(backupPath))
                {
                    ReportBlockingError(
                        "The automatic project backup failed. The build was not started so the configured backup guarantee is preserved. " +
                        "Check the Console and the backup folder, then try again.");
                    return false;
                }
            }

            if (string.IsNullOrEmpty(version))
            {
                version = AutoVersion.Enabled
                    ? AutoVersion.ApplyGeneratedVersion()
                    : Application.version;
            }

            var buildDir = GetBuildDirectory(target, version);

            if (Directory.Exists(buildDir) && HasScriptingBackendMismatch(buildDir, target))
            {
                if (Application.isBatchMode)
                {
                    ReportBlockingError(
                        "The build folder contains output from a different scripting backend. " +
                        "Clean the target build directory before running batch mode again: " + buildDir);
                    return false;
                }

                var clean = EditorUtility.DisplayDialog("ItchDeploy",
                    "Build path has a different scripting backend than the current project settings.\n\n" +
                    "Path: " + buildDir + "\n\n" +
                    "Would you like to clean the build folder and rebuild?",
                    "Clean & Rebuild", "Cancel");
                if (clean)
                {
                    Directory.Delete(buildDir, true);
                    _ = Directory.CreateDirectory(buildDir);
                }
                else
                {
                    EditorUtility.DisplayDialog("ItchDeploy",
                        "Change the scripting backend in Player Settings or clean the build folder manually.",
                        "OK");
                    IsBuilding = false;
                    return false;
                }
            }

            Directory.CreateDirectory(buildDir);

            IsBuilding = true;
            CurrentStatus = $"Building {target}...";
            OnBuildStart?.Invoke();

            VersionInfoWriter.Write(version, target.ToString(), channel);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = GetBuildLocation(buildDir, target),
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                target = target,
                options = BuildOptions.None,
            };

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            }
            finally
            {
                IsBuilding = false;
            }
            OnBuildComplete?.Invoke(report);

            if (report.summary.result != BuildResult.Succeeded)
            {
                CurrentStatus = $"Build failed: {report.summary.result}";
                ReportBlockingError(
                    $"Build failed with {report.summary.totalErrors} errors.",
                    $"Збірка завершилася помилками: {report.summary.totalErrors}.");
                return false;
            }

            RememberSuccessfulBuild(target, channel, version, buildDir);

            if (!shouldDeploy)
            {
                CurrentStatus = FormatSuccessfulBuildStatus(target, version);
                return true;
            }

            return DeployBuild(buildDir, target, channel, version, settings);
        }

        public static bool DeployBuild(string buildDir, BuildTarget target, string channel, string version, ItchDeploySettings settings)
        {
            // A deploy that skips the build - publishing an existing folder - arrives here
            // without having passed through the version resolution BuildAndDeploy does, and
            // an empty version would reach butler as an empty --userversion and name the
            // archive after nothing at all.
            if (string.IsNullOrEmpty(version))
            {
                version = AutoVersion.Enabled
                    ? AutoVersion.ApplyGeneratedVersion()
                    : Application.version;
            }

            if (IsDeploying) return false;

            if (target == BuildTarget.iOS)
            {
                const string error =
                    "An unsigned iOS Xcode project cannot be deployed as an installable itch.io build. " +
                    "Sign and export an IPA on macOS first.";
                CurrentStatus = "ERROR: " + error;
                OnDeployComplete?.Invoke(error, false);
                Debug.LogError("[ItchDeploy] " + error);
                return false;
            }

            if (!TryValidateDeploySettings(settings, out var configurationError))
            {
                CurrentStatus = "ERROR: " + configurationError;
                OnDeployComplete?.Invoke(configurationError, false);
                Debug.LogError("[ItchDeploy] " + configurationError);
                return false;
            }

            buildDir = ResolveBuildDirectory(buildDir, target);
            if (string.IsNullOrEmpty(buildDir))
            {
                const string error = "Build output was not found. Build first or choose a complete build folder.";
                CurrentStatus = "ERROR: " + error;
                OnDeployComplete?.Invoke(error, false);
                Debug.LogError("[ItchDeploy] " + error);
                return false;
            }

            if (!BuildPlatformCatalog.TryValidateDeployArtifact(buildDir, target, out var artifactError))
            {
                CurrentStatus = "ERROR: " + artifactError;
                OnDeployComplete?.Invoke(artifactError, false);
                Debug.LogError("[ItchDeploy] " + artifactError);
                return false;
            }

            IsDeploying = true;
            CurrentStatus = $"Deploying {target} -> {channel}...";
            OnDeployStart?.Invoke(channel);
            var deploymentStartedAt = DateTime.UtcNow;

            try
            {
                var targetStr = PlatformChannelMap.GetChannelTarget(settings.itchUserName, settings.itchGameSlug, channel);

                if (BuildArchive.AutoArchive)
                {
                    var archivePath = BuildArchive.ArchiveBuild(buildDir, target, version);
                    if (string.IsNullOrEmpty(archivePath))
                        Debug.LogWarning("[ItchDeploy] Continuing without an archive because it could not be created.");
                }

                string result;
                string uploadDirectory = buildDir;
                if (target == BuildTarget.WebGL)
                {
                    var webglDir = FindWebGLDir(buildDir);
                    if (webglDir == null)
                    {
                        CurrentStatus = "ERROR: WebGL build not found";
                        OnDeployComplete?.Invoke("WebGL build not found", false);
                        return false;
                    }

                    uploadDirectory = webglDir;
                }

                try
                {
                    ItchTomlEditor.CopyToBuild(uploadDirectory);
                }
                catch (Exception manifestException)
                {
                    Debug.LogWarning($"[ItchDeploy] Could not copy ItchDeployManifest.toml: {manifestException.Message}");
                }

                var pushResult = ButlerBridge.PushWithProgress(
                    uploadDirectory,
                    targetStr,
                    version,
                    apiKey: settings.EffectiveApiKey);

                result = pushResult.ToString();
                var success = pushResult.Succeeded;
                CurrentStatus = success
                    ? $"Success: {version} -> {channel}"
                    : pushResult.Cancelled
                        ? "Cancelled by user"
                        : $"Failed: {result}";

                var buildSizeBytes = DirectorySize(buildDir);

                DeployHistory.AddEntry(new DeployHistory.Entry
                {
                    version = version,
                    platform = PlatformChannelMap.GetDisplayName(target),
                    channel = channel,
                    status = success ? "Success" : pushResult.Cancelled ? "Cancelled" : "Failed",
                    timestampUtc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    buildTarget = target.ToString(),
                    buildSizeBytes = buildSizeBytes,
                });

                RecordDeploymentResult(target, channel, version, buildSizeBytes, success, result, deploymentStartedAt);

                if (success && GitTagManager.AutoTag)
                    GitTagManager.CreateTag(version, channel);

                OnDeployComplete?.Invoke(result, success);

                if (success)
                    Debug.Log($"ItchDeploy: Successfully deployed {version} to {targetStr}");
                else
                    Debug.LogError($"ItchDeploy deploy failed:\n{result}");

                return success;
            }
            catch (Exception ex)
            {
                CurrentStatus = $"Exception: {ex.Message}";
                OnDeployComplete?.Invoke(ex.Message, false);
                Debug.LogError($"ItchDeploy exception: {ex}");
                return false;
            }
            finally
            {
                IsDeploying = false;
            }
        }

        public static bool DeployExistingBuild(string buildPath, BuildTarget target, string channel, string version, ItchDeploySettings settings)
        {
            if (IsDeploying) return false;
            return DeployBuild(buildPath, target, channel, version, settings);
        }

        public static string FindWebGLDir(string buildDir)
        {
            return BuildPlatformCatalog.FindWebGLRoot(buildDir);
        }

        private static string DeployBlocker(string english, string ukrainian)
            => Application.isBatchMode
                ? english
                : ItchDeployLocale._(english, ukrainian);
        public static bool TryValidateDeploySettings(ItchDeploySettings settings, out string error)
        {
            if (settings == null)
            {
                error = DeployBlocker(
                    "Deployment settings are unavailable.",
                    "Налаштування публікації недоступні.");
                return false;
            }
            if (!ButlerBridge.IsButlerAvailable)
            {
                error = DeployBlocker(
                    "butler was not found. Install it from Project settings > Butler before deploying.",
                    "Butler не знайдено. Встановіть його у «Налаштування проєкту → Butler» перед публікацією.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(settings.itchUserName) || string.IsNullOrWhiteSpace(settings.itchGameSlug))
            {
                error = DeployBlocker(
                    "Set your itch.io username and project in Project settings > itch.io project.",
                    "Вкажіть користувача та проєкт itch.io у «Налаштування проєкту → Проєкт itch.io».");
                return false;
            }
            if (!settings.HasApiKey)
            {
                error = DeployBlocker(
                    "Connect your itch.io account in Project settings, or provide BUTLER_API_KEY through the environment.",
                    "Підключіть акаунт itch.io у налаштуваннях проєкту або передайте BUTLER_API_KEY через змінну середовища.");
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static void ValidateBeforeDeploy(out List<string> warnings, out List<string> errors)
        {
            warnings = new List<string>();
            errors = new List<string>();

            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
                errors.Add("No scenes in Build Settings");
            else if (!scenes.Any(s => s.enabled))
                errors.Add("No enabled scenes");

            if (string.IsNullOrEmpty(Application.productName) ||
                Application.productName == "My Project")
                warnings.Add("Consider setting a proper Product Name");

            var platform = EditorUserBuildSettings.activeBuildTarget;
            if (!BuildPlatformCatalog.TryValidateBuildConfiguration(
                    platform,
                    out _,
                    out var buildSupportError))
                errors.Add(buildSupportError);

            if (!BuildPlatformCatalog.CanDeployCurrentArtifactToItch(platform, out var deploymentReason))
                warnings.Add(deploymentReason);

            if (!ButlerBridge.IsButlerAvailable)
                warnings.Add("butler not found. Install it or place in PATH.");
        }

        private static long DirectorySize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            try
            {
                long total = 0;
                foreach (var file in new DirectoryInfo(path)
                             .EnumerateFiles("*", SearchOption.AllDirectories))
                    total += file.Length;
                return total;
            }
            catch (IOException) { return 0; }
            catch (UnauthorizedAccessException) { return 0; }
        }

        public static bool TryGetLastBuild(
            BuildTarget target,
            string channel,
            out string version,
            out string buildDirectory)
            => TryGetRecentlyCompletedBuild(target, channel, out version, out buildDirectory);

        private static bool TryGetRecentlyCompletedBuild(
            BuildTarget target,
            string channel,
            out string version,
            out string buildDirectory)
        {
            version = SessionState.GetString(LastBuildVersionKey, string.Empty);
            buildDirectory = SessionState.GetString(
                LastBuildDirectoryKey,
                string.Empty);
            var expectedFingerprint = target + "|" + (channel ?? string.Empty);
            if (!string.Equals(
                    SessionState.GetString(LastBuildFingerprintKey, string.Empty),
                    expectedFingerprint,
                    StringComparison.Ordinal) ||
                !long.TryParse(
                    SessionState.GetString(LastBuildUtcTicksKey, string.Empty),
                    out var completedTicks))
            {
                return false;
            }

            var elapsed = new TimeSpan(DateTime.UtcNow.Ticks - completedTicks)
                .TotalSeconds;
            return elapsed >= 0d &&
                   elapsed <= DuplicateBuildCooldownSeconds &&
                   !string.IsNullOrWhiteSpace(version) &&
                   BuildPlatformCatalog.LooksLikeBuildOutput(buildDirectory, target);
        }

        private static void RememberSuccessfulBuild(
            BuildTarget target,
            string channel,
            string version,
            string buildDirectory)
        {
            SessionState.SetString(
                LastBuildFingerprintKey,
                target + "|" + (channel ?? string.Empty));
            SessionState.SetString(LastBuildVersionKey, version ?? string.Empty);
            SessionState.SetString(
                LastBuildDirectoryKey,
                buildDirectory ?? string.Empty);
            SessionState.SetString(
                LastBuildUtcTicksKey,
                DateTime.UtcNow.Ticks.ToString());
        }

        internal static string GetBuildLocation(string buildDirectory, BuildTarget target)
        {
            var productName = SanitizeFileName(Application.productName);
            return BuildPlatformCatalog.GetBuildLocation(
                buildDirectory,
                target,
                productName,
                EditorUserBuildSettings.buildAppBundle);
        }

        private static string SanitizeFileName(string value)
            => CrossPlatformFileName.Sanitize(value, "Game");

        private static void RecordDeploymentResult(
            BuildTarget target,
            string channel,
            string version,
            long buildSizeBytes,
            bool success,
            string butlerResult,
            DateTime deploymentStartedAt)
        {
            var elapsedSeconds = Math.Max(0, (DateTime.UtcNow - deploymentStartedAt).TotalSeconds);
            var sizeMb = buildSizeBytes / (1024d * 1024d);
            BuildStatistics.RecordBuild(target.ToString(), version, elapsedSeconds, sizeMb, success);

            if (!ItchDeployReport.AutoGenerate)
                return;

            try
            {
                var errors = success || string.IsNullOrWhiteSpace(butlerResult)
                    ? new List<string>()
                    : new List<string> { butlerResult };
                ItchDeployReport.SaveReport(target.ToString(), channel, version, elapsedSeconds, sizeMb, success, errors);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItchDeploy] Could not save the deployment report: {ex.Message}");
            }
        }

        private static string ResolveBuildDirectory(string path, BuildTarget target)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return null;

            if (LooksLikeBuildDirectory(path, target))
                return path;

            return Directory.GetDirectories(path)
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .FirstOrDefault(candidate => LooksLikeBuildDirectory(candidate, target));
        }

        private static bool LooksLikeBuildDirectory(string path, BuildTarget target)
        {
            return BuildPlatformCatalog.LooksLikeBuildOutput(path, target);
        }

        private static bool HasScriptingBackendMismatch(string buildDir, BuildTarget target)
        {
            var backendPath = Path.Combine(buildDir, "build_info.json");
            if (!File.Exists(backendPath))
            {
                var exeFiles = Directory.GetFiles(buildDir, "*.exe", SearchOption.TopDirectoryOnly);
                if (exeFiles.Length == 0) return false;
                backendPath = exeFiles[0];
                if (!File.Exists(backendPath)) return false;
            }

            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            var currentBackend = PlayerSettings.GetScriptingBackend(namedTarget);
            var dataDir = Path.Combine(buildDir, $"{Application.productName}_Data");
            if (Directory.Exists(dataDir))
            {
                var monoDlls = Directory.GetFiles(dataDir, "*.dll", SearchOption.TopDirectoryOnly);
                var il2cppDlls = Directory.GetFiles(dataDir, "*.dll", SearchOption.AllDirectories);
                bool hasIl2Cpp = il2cppDlls.Any(d => d.Contains("il2cpp") || d.Contains("Il2Cpp"));
                bool hasMono = monoDlls.Length > 0;

                if (currentBackend == ScriptingImplementation.IL2CPP && hasMono && !hasIl2Cpp)
                    return true;
                if (currentBackend == ScriptingImplementation.Mono2x && hasIl2Cpp)
                    return true;
            }
            return false;
        }

        internal static string FormatSuccessfulBuildStatus(
            BuildTarget target,
            string version)
            => $"Build succeeded: {version} ({PlatformChannelMap.GetDisplayName(target)})";

        private static void ReportBlockingError(string english, string ukrainian = null)
        {
            var localized = string.IsNullOrEmpty(ukrainian)
                ? english
                : ItchDeployLocale._(english, ukrainian);

            CurrentStatus = "ERROR: " + localized;
            Debug.LogError("[ItchDeploy] " + english);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("ItchDeploy", localized, "OK");
        }
    }
}
