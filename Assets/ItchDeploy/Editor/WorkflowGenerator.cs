using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class WorkflowGenerator
    {
        public enum Trigger
        {
            PushMain,
            OnTag,
            Manual,
        }

        public sealed class Options
        {
            public Trigger trigger = Trigger.PushMain;
            public bool deploy = true;
            public bool createRelease = true;
            public IReadOnlyList<string> platforms;
            public string workflowFileName = "build-and-deploy.yml";
        }

        private static readonly HashSet<string> SupportedPlatforms = new(
            BuildPlatformCatalog.Targets.Select(target => target.ToString()),
            StringComparer.Ordinal);

        public static string WorkflowsDir => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", ".github", "workflows"));

        public static string Generate(Options options = null)
        {
            options ??= new Options();
            var data = ItchDeployConfig.Load();
            var yaml = GenerateYaml(options, data);
            var problems = ValidateYaml(yaml);
            if (problems.Count > 0)
                throw new InvalidOperationException(
                    "Generated workflow failed validation:\n- " + string.Join("\n- ", problems));

            Directory.CreateDirectory(WorkflowsDir);
            var fileName = Path.GetFileName(options.workflowFileName);
            if (string.IsNullOrWhiteSpace(fileName) ||
                (!fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) &&
                 !fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Workflow file name must end in .yml or .yaml.");
            }

            var path = Path.Combine(WorkflowsDir, fileName);
            File.WriteAllText(path, yaml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssetDatabase.Refresh();
            return path;
        }

        public static string GenerateYaml(
            Options options = null,
            ItchDeployConfig.ConfigData data = null)
        {
            options ??= new Options();
            data ??= ItchDeployConfig.Load();
            var platforms = ResolvePlatforms(options, data);
            ValidateConfiguration(data, platforms);

            var sb = new StringBuilder(16_384);
            L(sb, 0, "name: ItchDeploy CI/CD");
            L(sb);
            AppendTriggers(sb, options, platforms);
            L(sb);
            L(sb, 0, "permissions:");
            L(sb, 1, "contents: read");
            L(sb);
            L(sb, 0, "concurrency:");
            L(sb, 1, "group: ${{ github.workflow }}-${{ github.ref }}");
            L(sb, 1, "cancel-in-progress: true");
            L(sb);
            L(sb, 0, "env:");
            L(sb, 1, "UNITY_VERSION: " + YamlQuote(data.unityVersion));
            L(sb, 1, "ITCH_USER: " + YamlQuote(data.itchUser));
            L(sb, 1, "ITCH_GAME: " + YamlQuote(data.itchGame));
            L(sb, 1, "BUILD_NAME: " + YamlQuote(
                string.IsNullOrWhiteSpace(data.buildName) ? data.itchGame : data.buildName));
            L(sb);
            L(sb, 0, "jobs:");

            AppendPrepareJob(sb, data, options, platforms);
            AppendSecretPreflightJob(sb);
            AppendTestJob(sb);
            AppendBuildJob(sb, data, options);
            if (options.createRelease)
                AppendReleaseJob(sb);
            AppendNotifyJob(sb, data, options);

            return sb.ToString();
        }

        public static List<string> ValidateYaml(string yaml)
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(yaml))
            {
                problems.Add("Workflow is empty.");
                return problems;
            }

            yaml = yaml.Replace("\r\n", "\n");

            if (!yaml.Contains("\njobs:\n", StringComparison.Ordinal))
                problems.Add("Missing root jobs mapping.");
            if (!yaml.Contains("\n  test:\n", StringComparison.Ordinal))
                problems.Add("Missing test job.");
            if (!yaml.Contains("\n  build:\n", StringComparison.Ordinal))
                problems.Add("Missing build job.");
            if (yaml.Contains("${ needs.", StringComparison.Ordinal))
                problems.Add("Malformed GitHub expression detected.");
            if (yaml.Contains("BUTLER_API_KEY:", StringComparison.Ordinal) &&
                !yaml.Contains("${{ secrets.BUTLER_API_KEY }}", StringComparison.Ordinal))
            {
                problems.Add("BUTLER_API_KEY must come from GitHub Secrets.");
            }
            if (yaml.Contains("${{ secrets.ANDROID_KEYSTORE_NAME }}", StringComparison.Ordinal))
            {
                problems.Add("The Android keystore file name must not be stored as a secret.");
            }
            if (yaml.Contains("ANDROID_KEYSTORE_BASE64", StringComparison.Ordinal) &&
                !yaml.Contains("androidKeystoreName: user.keystore", StringComparison.Ordinal))
            {
                problems.Add("Android CI signing needs a stable decoded keystore file name.");
            }
            if (!yaml.Contains("build_matrix: ${{ steps.matrix.outputs.build_matrix }}", StringComparison.Ordinal) ||
                !yaml.Contains("runs-on: ${{ matrix.runner }}", StringComparison.Ordinal))
            {
                problems.Add("Build targets must use the platform-specific runner matrix.");
            }

            return problems;
        }

        private static void AppendTriggers(
            StringBuilder sb,
            Options options,
            IReadOnlyList<string> platforms)
        {
            L(sb, 0, "on:");
            if (options.trigger == Trigger.PushMain)
            {
                L(sb, 1, "push:");
                L(sb, 2, "branches: [ main ]");
                L(sb, 2, "tags: [ 'v*' ]");
                L(sb, 2, "paths-ignore:");
                L(sb, 3, "- 'docs/**'");
                L(sb, 3, "- '**/*.md'");
                L(sb, 1, "pull_request:");
                L(sb, 2, "branches: [ main ]");
            }
            else if (options.trigger == Trigger.OnTag)
            {
                L(sb, 1, "push:");
                L(sb, 2, "tags: [ 'v*' ]");
            }

            L(sb, 1, "workflow_dispatch:");
            L(sb, 2, "inputs:");
            L(sb, 3, "platform:");
            L(sb, 4, "description: Platform to build");
            L(sb, 4, "required: true");
            L(sb, 4, "type: choice");
            L(sb, 4, "default: All");
            L(sb, 4, "options:");
            L(sb, 5, "- All");
            foreach (var platform in platforms)
                L(sb, 5, "- " + platform);
            L(sb, 3, "deploy:");
            L(sb, 4, "description: Publish successful builds to itch.io");
            L(sb, 4, "required: true");
            L(sb, 4, "type: boolean");
            L(sb, 4, "default: " + (options.deploy ? "true" : "false"));
            L(sb, 3, "release:");
            L(sb, 4, "description: Create or update a GitHub release");
            L(sb, 4, "required: true");
            L(sb, 4, "type: boolean");
            L(sb, 4, "default: false");
        }

        private static void AppendPrepareJob(
            StringBuilder sb,
            ItchDeployConfig.ConfigData data,
            Options options,
            IReadOnlyList<string> platforms)
        {
            var matrixJson = "[" + string.Join(",", platforms.Select(platform =>
                "{\"platform\":" + YamlJsonString(platform) +
                ",\"runner\":" + YamlJsonString(GetRunnerForPlatform(platform)) + "}")) + "]";
            var defaultTarget = platforms.Contains(data.defaultTarget)
                ? data.defaultTarget
                : platforms[0];

            L(sb, 1, "prepare:");
            L(sb, 2, "name: Prepare release");
            L(sb, 2, "runs-on: ubuntu-latest");
            L(sb, 2, "outputs:");
            L(sb, 3, "platforms: ${{ steps.matrix.outputs.platforms }}");
            L(sb, 3, "build_matrix: ${{ steps.matrix.outputs.build_matrix }}");
            L(sb, 3, "version: ${{ steps.version.outputs.version }}");
            L(sb, 3, "ios_version: ${{ steps.version.outputs.ios_version }}");
            L(sb, 3, "version_code: ${{ steps.version.outputs.version_code }}");
            L(sb, 3, "should_deploy: ${{ steps.flags.outputs.should_deploy }}");
            L(sb, 3, "should_release: ${{ steps.flags.outputs.should_release }}");
            L(sb, 2, "steps:");
            L(sb, 3, "- name: Checkout history");
            L(sb, 4, "uses: actions/checkout@v6");
            L(sb, 4, "with:");
            L(sb, 5, "fetch-depth: 0");
            L(sb, 3, "- name: Resolve platform matrix");
            L(sb, 4, "id: matrix");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "CONFIGURED='" + matrixJson + "'");
            L(sb, 5, "REQUESTED=\"${{ inputs.platform || 'All' }}\"");
            L(sb, 5, "if [[ \"$GITHUB_EVENT_NAME\" == \"pull_request\" ]]; then");
            L(sb, 6, "BUILD_MATRIX=$(jq -c --arg platform '" + defaultTarget + "' '[.[] | select(.platform == $platform)]' <<<\"$CONFIGURED\")");
            L(sb, 5, "elif [[ \"$REQUESTED\" != \"All\" ]]; then");
            L(sb, 6, "BUILD_MATRIX=$(jq -c --arg platform \"$REQUESTED\" '[.[] | select(.platform == $platform)]' <<<\"$CONFIGURED\")");
            L(sb, 5, "else");
            L(sb, 6, "BUILD_MATRIX=\"$CONFIGURED\"");
            L(sb, 5, "fi");
            L(sb, 5, "if [[ \"$(jq 'length' <<<\"$BUILD_MATRIX\")\" == \"0\" ]]; then echo '::error::Requested platform is not configured'; exit 1; fi");
            L(sb, 5, "PLATFORMS=$(jq -c '[.[].platform]' <<<\"$BUILD_MATRIX\")");
            L(sb, 5, "BUILD_MATRIX_OBJECT=$(jq -c '{include: .}' <<<\"$BUILD_MATRIX\")");
            L(sb, 5, "echo \"platforms=$PLATFORMS\" >> \"$GITHUB_OUTPUT\"");
            L(sb, 5, "echo \"build_matrix=$BUILD_MATRIX_OBJECT\" >> \"$GITHUB_OUTPUT\"");
            L(sb, 3, "- name: Resolve version");
            L(sb, 4, "id: version");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "if [[ \"$GITHUB_REF\" == refs/tags/v* ]]; then");
            L(sb, 6, "VERSION=\"${GITHUB_REF_NAME#v}\"");
            L(sb, 5, "else");
            L(sb, 6, "BASE=$(awk '/bundleVersion:/ { print $2; exit }' ProjectSettings/ProjectSettings.asset)");
            L(sb, 6, "BASE=${BASE:-0.1.0}");
            L(sb, 6, "VERSION=\"${BASE}.${GITHUB_RUN_NUMBER}-${GITHUB_SHA::7}\"");
            L(sb, 5, "fi");
            L(sb, 5, "IOS_VERSION=\"1.0.${GITHUB_RUN_NUMBER}\"");
            L(sb, 5, "echo \"version=$VERSION\" >> \"$GITHUB_OUTPUT\"");
            L(sb, 5, "echo \"ios_version=$IOS_VERSION\" >> \"$GITHUB_OUTPUT\"");
            L(sb, 5, "echo \"version_code=$GITHUB_RUN_NUMBER\" >> \"$GITHUB_OUTPUT\"");
            L(sb, 3, "- name: Resolve publish flags");
            L(sb, 4, "id: flags");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "SHOULD_DEPLOY=false");
            if (options.deploy)
            {
                L(sb, 5, "if [[ \"$GITHUB_EVENT_NAME\" != \"pull_request\" ]]; then");
                L(sb, 6, "if [[ \"$GITHUB_EVENT_NAME\" == \"workflow_dispatch\" ]]; then");
                L(sb, 7, "SHOULD_DEPLOY=\"${{ inputs.deploy }}\"");
                L(sb, 6, "else");
                L(sb, 7, "SHOULD_DEPLOY=true");
                L(sb, 6, "fi");
                L(sb, 5, "fi");
            }
            L(sb, 5, "SHOULD_RELEASE=false");
            if (options.createRelease)
            {
                L(sb, 5, "if [[ \"$GITHUB_REF\" == refs/tags/v* ]]; then");
                L(sb, 6, "SHOULD_RELEASE=true");
                L(sb, 5, "elif [[ \"$GITHUB_EVENT_NAME\" == \"workflow_dispatch\" ]]; then");
                L(sb, 6, "SHOULD_RELEASE=\"${{ inputs.release }}\"");
                L(sb, 5, "fi");
            }
            L(sb, 5, "echo \"should_deploy=$SHOULD_DEPLOY\" >> \"$GITHUB_OUTPUT\"");
            L(sb, 5, "echo \"should_release=$SHOULD_RELEASE\" >> \"$GITHUB_OUTPUT\"");
            L(sb);
        }

        private static void AppendSecretPreflightJob(StringBuilder sb)
        {
            L(sb, 1, "preflight:");
            L(sb, 2, "name: Validate required secrets");
            L(sb, 2, "needs: prepare");
            L(sb, 2, "runs-on: ubuntu-latest");
            L(sb, 2, "env:");
            L(sb, 3, "UNITY_LICENSE_VALUE: ${{ secrets.UNITY_LICENSE }}");
            L(sb, 3, "BUTLER_API_KEY_VALUE: ${{ secrets.BUTLER_API_KEY }}");
            L(sb, 3, "ANDROID_KEYSTORE_BASE64_VALUE: ${{ secrets.ANDROID_KEYSTORE_BASE64 }}");
            L(sb, 3, "ANDROID_KEYSTORE_PASS_VALUE: ${{ secrets.ANDROID_KEYSTORE_PASS }}");
            L(sb, 3, "ANDROID_KEYALIAS_NAME_VALUE: ${{ secrets.ANDROID_KEYALIAS_NAME }}");
            L(sb, 3, "ANDROID_KEYALIAS_PASS_VALUE: ${{ secrets.ANDROID_KEYALIAS_PASS }}");
            L(sb, 2, "steps:");
            L(sb, 3, "- name: Check build and deployment credentials");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "MISSING=()");
            L(sb, 5, "[[ -n \"$UNITY_LICENSE_VALUE\" ]] || MISSING+=(UNITY_LICENSE)");
            L(sb, 5, "if [[ \"${{ needs.prepare.outputs.should_deploy }}\" == \"true\" ]]; then");
            L(sb, 6, "[[ -n \"$BUTLER_API_KEY_VALUE\" ]] || MISSING+=(BUTLER_API_KEY)");
            L(sb, 5, "fi");
            L(sb, 5, "if [[ '${{ needs.prepare.outputs.platforms }}' == *'\"Android\"'* ]]; then");
            L(sb, 6, "[[ -n \"$ANDROID_KEYSTORE_BASE64_VALUE\" ]] || MISSING+=(ANDROID_KEYSTORE_BASE64)");
            L(sb, 6, "[[ -n \"$ANDROID_KEYSTORE_PASS_VALUE\" ]] || MISSING+=(ANDROID_KEYSTORE_PASS)");
            L(sb, 6, "[[ -n \"$ANDROID_KEYALIAS_NAME_VALUE\" ]] || MISSING+=(ANDROID_KEYALIAS_NAME)");
            L(sb, 6, "[[ -n \"$ANDROID_KEYALIAS_PASS_VALUE\" ]] || MISSING+=(ANDROID_KEYALIAS_PASS)");
            L(sb, 5, "fi");
            L(sb, 5, "if (( ${#MISSING[@]} )); then");
            L(sb, 6, "echo \"::error::Missing required repository secrets: ${MISSING[*]}\"");
            L(sb, 6, "exit 1");
            L(sb, 5, "fi");
            L(sb, 5, "echo 'Required repository secrets are configured.'");
            L(sb);
        }

        private static void AppendTestJob(StringBuilder sb)
        {
            L(sb, 1, "test:");
            L(sb, 2, "name: Unity tests");
            L(sb, 2, "needs: [prepare, preflight]");
            L(sb, 2, "runs-on: ubuntu-latest");
            L(sb, 2, "timeout-minutes: 45");
            L(sb, 2, "permissions:");
            L(sb, 3, "contents: read");
            L(sb, 3, "checks: write");
            L(sb, 2, "steps:");
            L(sb, 3, "- name: Checkout");
            L(sb, 4, "uses: actions/checkout@v6");
            L(sb, 4, "with:");
            L(sb, 5, "lfs: true");
            AppendFreeDiskSpaceStep(sb);
            L(sb, 3, "- name: Cache Unity Library");
            L(sb, 4, "uses: actions/cache@v4");
            L(sb, 4, "with:");
            L(sb, 5, "path: |");
            L(sb, 6, "Library");
            L(sb, 6, "!Library/PackageCache");
            L(sb, 5, "key: Library-tests-${{ env.UNITY_VERSION }}-${{ hashFiles('Assets/**', 'Packages/**', 'ProjectSettings/**') }}");
            L(sb, 5, "restore-keys: |");
            L(sb, 6, "Library-tests-${{ env.UNITY_VERSION }}-");
            L(sb, 3, "- name: Run EditMode tests");
            L(sb, 4, "id: tests");
            L(sb, 4, "uses: game-ci/unity-test-runner@v4");
            AppendUnityLicenseEnvironment(sb, 4);
            L(sb, 4, "with:");
            L(sb, 5, "unityVersion: ${{ env.UNITY_VERSION }}");
            L(sb, 5, "testMode: editmode");
            L(sb, 5, "githubToken: ${{ github.token }}");
            L(sb, 5, "checkName: ItchDeploy EditMode tests");
            L(sb, 3, "- name: Upload test results");
            L(sb, 4, "if: always()");
            L(sb, 4, "continue-on-error: true");
            L(sb, 4, "uses: actions/upload-artifact@v4");
            L(sb, 4, "with:");
            L(sb, 5, "name: Test-results");
            L(sb, 5, "path: ${{ steps.tests.outputs.artifactsPath }}");
            L(sb, 5, "retention-days: 7");
            L(sb);
        }

        private static void AppendBuildJob(
            StringBuilder sb,
            ItchDeployConfig.ConfigData data,
            Options options)
        {
            L(sb, 1, "build:");
            L(sb, 2, "name: Build ${{ matrix.platform }}");
            L(sb, 2, "needs: [prepare, test]");
            L(sb, 2, "runs-on: ${{ matrix.runner }}");
            L(sb, 2, "timeout-minutes: 90");
            L(sb, 2, "strategy:");
            L(sb, 3, "fail-fast: false");
            L(sb, 3, "max-parallel: 4");
            L(sb, 3, "matrix: ${{ fromJson(needs.prepare.outputs.build_matrix) }}");
            L(sb, 2, "steps:");
            L(sb, 3, "- name: Checkout");
            L(sb, 4, "uses: actions/checkout@v6");
            L(sb, 4, "with:");
            L(sb, 5, "lfs: true");
            AppendFreeDiskSpaceStep(sb);
            L(sb, 3, "- name: Cache Unity Library");
            L(sb, 4, "uses: actions/cache@v4");
            L(sb, 4, "with:");
            L(sb, 5, "path: |");
            L(sb, 6, "Library");
            L(sb, 6, "!Library/PackageCache");
            L(sb, 5, "key: Library-${{ matrix.platform }}-${{ env.UNITY_VERSION }}-${{ hashFiles('Assets/**', 'Packages/**', 'ProjectSettings/**') }}");
            L(sb, 5, "restore-keys: |");
            L(sb, 6, "Library-${{ matrix.platform }}-${{ env.UNITY_VERSION }}-");
            L(sb, 3, "- name: Enable Android CI signing");
            L(sb, 4, "if: matrix.platform == 'Android'");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "python3 - <<'PY'");
            L(sb, 5, "from pathlib import Path");
            L(sb, 5, "path = Path('ProjectSettings/ProjectSettings.asset')");
            L(sb, 5, "text = path.read_text(encoding='utf-8')");
            L(sb, 5, "if 'androidUseCustomKeystore: 1' not in text:");
            L(sb, 6, "old = 'androidUseCustomKeystore: 0'");
            L(sb, 6, "if old not in text:");
            L(sb, 7, "raise SystemExit('androidUseCustomKeystore was not found in Player Settings')");
            L(sb, 6, "path.write_text(text.replace(old, 'androidUseCustomKeystore: 1', 1), encoding='utf-8')");
            L(sb, 5, "PY");
            L(sb, 3, "- name: Build Unity project");
            L(sb, 4, "uses: game-ci/unity-builder@v4");
            AppendUnityLicenseEnvironment(sb, 4);
            L(sb, 4, "with:");
            L(sb, 5, "targetPlatform: ${{ matrix.platform }}");
            L(sb, 5, "unityVersion: ${{ env.UNITY_VERSION }}");
            L(sb, 5, "buildName: ${{ env.BUILD_NAME }}");
            L(sb, 5, "buildsPath: build");
            L(sb, 5, "versioning: Custom");
            L(sb, 5, "version: ${{ matrix.platform == 'iOS' && needs.prepare.outputs.ios_version || needs.prepare.outputs.version }}");
            L(sb, 5, "androidVersionCode: ${{ needs.prepare.outputs.version_code }}");
            L(sb, 5, "androidExportType: androidPackage");

            L(sb, 5, "androidKeystoreName: user.keystore");
            L(sb, 5, "androidKeystoreBase64: ${{ secrets.ANDROID_KEYSTORE_BASE64 }}");
            L(sb, 5, "androidKeystorePass: ${{ secrets.ANDROID_KEYSTORE_PASS }}");
            L(sb, 5, "androidKeyaliasName: ${{ secrets.ANDROID_KEYALIAS_NAME }}");
            L(sb, 5, "androidKeyaliasPass: ${{ secrets.ANDROID_KEYALIAS_PASS }}");
            L(sb, 5, "allowDirtyBuild: true");
            if (data.ci.disableBurstOnRunners)
                L(sb, 5, "customParameters: --burst-disable-compilation");
            L(sb, 3, "- name: Remove Unity do-not-ship directories");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "if [[ \"$RUNNER_OS\" == \"Linux\" ]]; then FIND_CMD=(sudo find); else FIND_CMD=(find); fi");
            L(sb, 5, "\"${FIND_CMD[@]}\" \"build/${{ matrix.platform }}\" -type d \\");
            L(sb, 6, "\\( -name '*_BackUpThisFolder_ButDontShipItWithYourGame' -o -name '*_BurstDebugInformation_DoNotShip' \\) \\");
            L(sb, 6, "-prune -exec rm -rf {} +");
            if (options.deploy)
                AppendPublishSteps(sb, data);
            L(sb, 3, "- name: Upload build artifact");
            L(sb, 4, "if: needs.prepare.outputs.should_release == 'true'");
            L(sb, 4, "uses: actions/upload-artifact@v4");
            L(sb, 4, "with:");
            L(sb, 5, "name: Build-${{ matrix.platform }}-${{ needs.prepare.outputs.version }}");
            L(sb, 5, "path: build/${{ matrix.platform }}");
            L(sb, 5, "if-no-files-found: error");
            L(sb, 5, "retention-days: 1");
            L(sb, 5, "compression-level: 0");
            L(sb);
        }

        private static void AppendFreeDiskSpaceStep(StringBuilder sb)
        {
            L(sb, 3, "- name: Free runner disk space");
            L(sb, 4, "if: runner.os == 'Linux'");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "sudo rm -rf /usr/share/dotnet /usr/local/lib/android /opt/ghc \\");
            L(sb, 6, "/usr/local/share/boost /opt/hostedtoolcache/CodeQL");
            L(sb, 5, "sudo apt-get clean");
            L(sb, 5, "docker system prune --all --force || true");
            L(sb, 5, "df -h /");
        }

        private static void AppendPublishSteps(StringBuilder sb, ItchDeployConfig.ConfigData data)
        {
            L(sb, 3, "- name: Install butler");
            L(sb, 4, "if: needs.prepare.outputs.should_deploy == 'true' && matrix.platform != 'iOS'");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "case \"$RUNNER_OS\" in");
            L(sb, 6, "Linux) BUTLER_CHANNEL=linux-amd64 ;;");
            L(sb, 6, "Windows) BUTLER_CHANNEL=windows-amd64 ;;");
            L(sb, 6, "macOS) if [[ \"$(uname -m)\" == \"arm64\" ]]; then BUTLER_CHANNEL=darwin-arm64; else BUTLER_CHANNEL=darwin-amd64; fi ;;");
            L(sb, 6, "*) echo \"::error::Unsupported runner OS $RUNNER_OS\"; exit 1 ;;");
            L(sb, 5, "esac");
            L(sb, 5, "mkdir -p \"$RUNNER_TEMP/butler\"");
            L(sb, 5, "curl --fail --location --retry 3 --output \"$RUNNER_TEMP/butler.zip\" \\");
            L(sb, 6, "\"https://broth.itch.zone/butler/$BUTLER_CHANNEL/LATEST/archive/default\"");
            L(sb, 5, "if [[ \"$RUNNER_OS\" == \"Windows\" ]]; then");
            L(sb, 6, "powershell -NoProfile -Command \"Expand-Archive -Force -Path '$RUNNER_TEMP/butler.zip' -DestinationPath '$RUNNER_TEMP/butler'\"");
            L(sb, 6, "BUTLER=\"$RUNNER_TEMP/butler/butler.exe\"");
            L(sb, 5, "else");
            L(sb, 6, "unzip -q -o \"$RUNNER_TEMP/butler.zip\" -d \"$RUNNER_TEMP/butler\"");
            L(sb, 6, "BUTLER=\"$RUNNER_TEMP/butler/butler\"");
            L(sb, 6, "chmod +x \"$BUTLER\"");
            L(sb, 5, "fi");
            L(sb, 5, "echo \"BUTLER=$BUTLER\" >> \"$GITHUB_ENV\"");
            L(sb, 5, "\"$BUTLER\" version");
            L(sb, 3, "- name: Validate build output and resolve channel");
            L(sb, 4, "if: needs.prepare.outputs.should_deploy == 'true' && matrix.platform != 'iOS'");
            L(sb, 4, "id: publish");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "SOURCE=\"build/${{ matrix.platform }}\"");
            L(sb, 5, "if [[ \"${{ matrix.platform }}\" == \"WebGL\" ]]; then");
            L(sb, 6, "INDEX=$(find \"$SOURCE\" -type f -name index.html -print -quit)");
            L(sb, 6, "if [[ -z \"$INDEX\" ]]; then echo '::error::WebGL index.html was not found'; exit 1; fi");
            L(sb, 6, "SOURCE=$(dirname \"$INDEX\")");
            L(sb, 5, "fi");
            L(sb, 5, "if [[ -z \"$(find \"$SOURCE\" -type f -print -quit)\" ]]; then");
            L(sb, 6, "echo '::error::Build output is empty'; exit 1");
            L(sb, 5, "fi");
            L(sb, 5, "if [[ \"${{ matrix.platform }}\" == \"Android\" ]] && find \"$SOURCE\" -type f -name '*.aab' -print -quit | grep -q .; then");
            L(sb, 6, "echo '::error::Android App Bundles cannot be installed from itch.io. Build an APK for this channel.'; exit 1");
            L(sb, 5, "fi");
            L(sb, 5, "case \"${{ matrix.platform }}\" in");
            foreach (var pair in data.channels)
                L(sb, 6, pair.Key + ") CHANNEL=" + ShellQuote(pair.Value) + " ;;");
            L(sb, 6, "*) echo '::error::No itch.io channel mapping for ${{ matrix.platform }}'; exit 1 ;;");
            L(sb, 5, "esac");
            L(sb, 5, "echo \"source=$SOURCE\" >> \"$GITHUB_OUTPUT\"");
            L(sb, 5, "echo \"channel=$CHANNEL\" >> \"$GITHUB_OUTPUT\"");
            L(sb, 3, "- name: Publish with butler");
            L(sb, 4, "if: needs.prepare.outputs.should_deploy == 'true' && matrix.platform != 'iOS'");
            L(sb, 4, "env:");
            L(sb, 5, "BUTLER_API_KEY: ${{ secrets.BUTLER_API_KEY }}");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "if [[ -z \"$BUTLER_API_KEY\" ]]; then echo '::error::Missing BUTLER_API_KEY secret'; exit 1; fi");
            L(sb, 5, "IGNORE_ARGS=()");
            L(sb, 5, "if [[ -f .itchignore ]]; then");
            L(sb, 6, "while IFS= read -r RAW_PATTERN || [[ -n \"$RAW_PATTERN\" ]]; do");
            L(sb, 7, "PATTERN=\"${RAW_PATTERN#\"${RAW_PATTERN%%[![:space:]]*}\"}\"");
            L(sb, 7, "PATTERN=\"${PATTERN%\"${PATTERN##*[![:space:]]}\"}\"");
            L(sb, 7, "[[ -z \"$PATTERN\" || \"$PATTERN\" == \\#* ]] && continue");
            L(sb, 7, "PATTERN=\"${PATTERN%/}\"");
            L(sb, 7, "if [[ -z \"$PATTERN\" || \"$PATTERN\" == */* ]]; then");
            L(sb, 8, "echo \"::warning::Skipping .itchignore pattern '$PATTERN': butler matches a single path component\"");
            L(sb, 8, "continue");
            L(sb, 7, "fi");
            L(sb, 7, "IGNORE_ARGS+=(--ignore \"$PATTERN\")");
            L(sb, 6, "done < .itchignore");
            L(sb, 5, "fi");
            L(sb, 5, "\"$BUTLER\" push \"${IGNORE_ARGS[@]}\" \"${{ steps.publish.outputs.source }}\" \\");
            L(sb, 6, "\"${ITCH_USER}/${ITCH_GAME}:${{ steps.publish.outputs.channel }}\" \\");
            L(sb, 6, "--userversion \"${{ needs.prepare.outputs.version }}\" \\");
            L(sb, 6, "--fix-permissions --if-changed");
        }
        private static void AppendReleaseJob(StringBuilder sb)
        {
            L(sb, 1, "github_release:");
            L(sb, 2, "name: GitHub release");
            L(sb, 2, "needs: [prepare, build]");
            L(sb, 2, "if: needs.prepare.outputs.should_release == 'true' && needs.build.result == 'success'");
            L(sb, 2, "runs-on: ubuntu-latest");
            L(sb, 2, "permissions:");
            L(sb, 3, "contents: write");
            L(sb, 2, "steps:");
            L(sb, 3, "- name: Checkout plugin source");
            L(sb, 4, "uses: actions/checkout@v6");
            L(sb, 3, "- name: Download build artifacts");
            L(sb, 4, "uses: actions/download-artifact@v5");
            L(sb, 4, "with:");
            L(sb, 5, "pattern: Build-*-${{ needs.prepare.outputs.version }}");
            L(sb, 5, "path: release-input");
            L(sb, 3, "- name: Package release files");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "mkdir -p release-files");
            L(sb, 5, "find release-input -mindepth 1 -maxdepth 1 -type d -print0 | while IFS= read -r -d '' dir; do");
            L(sb, 6, "name=$(basename \"$dir\")");
            L(sb, 6, "(cd \"$dir\" && zip -qr \"../../release-files/${name}.zip\" .)");
            L(sb, 5, "done");
            L(sb, 5, "test -n \"$(find release-files -name '*.zip' -print -quit)\"");
            L(sb, 3, "- name: Package ItchDeploy plugin");
            L(sb, 4, "env:");
            L(sb, 5, "VERSION: ${{ needs.prepare.outputs.version }}");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "mkdir -p plugin-release/ItchDeploy");
            L(sb, 5, "(cd Assets/ItchDeploy && tar --exclude='Editor/Tests' --exclude='Editor/Tests.meta' -cf - Editor Runtime Editor.meta Runtime.meta package.json package.json.meta) | (cd plugin-release/ItchDeploy && tar -xf -)");
            L(sb, 5, "for DOC in README.md CHANGELOG.md LICENSE SECURITY.md; do");
            L(sb, 6, "[[ -f \"$DOC\" ]] && cp \"$DOC\" plugin-release/ || true");
            L(sb, 5, "done");
            L(sb, 5, "[[ -d docs ]] && cp -R docs plugin-release/ || true");
            L(sb, 5, "(cd plugin-release && zip -qr \"../release-files/ItchDeploy-v${VERSION}.zip\" .)");
            L(sb, 5, "(cd release-files && sha256sum \"ItchDeploy-v${VERSION}.zip\" > \"ItchDeploy-v${VERSION}.zip.sha256\")");
            L(sb, 3, "- name: Create or update release");
            L(sb, 4, "env:");
            L(sb, 5, "GH_TOKEN: ${{ github.token }}");
            L(sb, 5, "VERSION: ${{ needs.prepare.outputs.version }}");
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "if [[ \"$GITHUB_REF\" == refs/tags/* ]]; then TAG=\"$GITHUB_REF_NAME\"; PRERELEASE=; else TAG=\"v$VERSION\"; PRERELEASE=--prerelease; fi");
            L(sb, 5, "if gh release view \"$TAG\" >/dev/null 2>&1; then");
            L(sb, 6, "gh release upload \"$TAG\" release-files/* --clobber");
            L(sb, 5, "else");
            L(sb, 6, "gh release create \"$TAG\" release-files/* --title \"$TAG\" --generate-notes $PRERELEASE");
            L(sb, 5, "fi");
            L(sb);
        }

        private static void AppendNotifyJob(
            StringBuilder sb,
            ItchDeployConfig.ConfigData data,
            Options options)
        {
            var needs = new List<string> { "prepare", "test", "build" };
            if (options.createRelease) needs.Add("github_release");

            L(sb, 1, "notify:");
            L(sb, 2, "name: Notify");
            L(sb, 2, "needs: [" + string.Join(", ", needs) + "]");
            L(sb, 2, "if: always()");
            L(sb, 2, "runs-on: ubuntu-latest");
            L(sb, 2, "steps:");
            L(sb, 3, "- name: Send Discord notification");
            L(sb, 4, "env:");
            L(sb, 5, "DISCORD_WEBHOOK: ${{ secrets.DISCORD_WEBHOOK_URL }}");
            L(sb, 5, "BUILD_RESULT: ${{ needs.build.result }}");
            L(sb, 5, "DEPLOY_RESULT: ${{ needs.build.result }}");
            L(sb, 5, "VERSION: ${{ needs.prepare.outputs.version }}");
            L(sb, 5, "NOTIFY_SUCCESS: " + data.notify.notifyOnSuccess.ToString().ToLowerInvariant());
            L(sb, 5, "NOTIFY_FAILURE: " + data.notify.notifyOnFailure.ToString().ToLowerInvariant());
            L(sb, 4, "shell: bash");
            L(sb, 4, "run: |");
            L(sb, 5, "set -euo pipefail");
            L(sb, 5, "[[ -z \"$DISCORD_WEBHOOK\" ]] && exit 0");
            L(sb, 5, "if [[ \"$BUILD_RESULT\" == \"success\" && ( \"$DEPLOY_RESULT\" == \"success\" || \"$DEPLOY_RESULT\" == \"skipped\" ) ]]; then");
            L(sb, 6, "STATUS=Success; COLOR=3066993; [[ \"$NOTIFY_SUCCESS\" == \"true\" ]] || exit 0");
            L(sb, 5, "else");
            L(sb, 6, "STATUS=Failed; COLOR=15158332; [[ \"$NOTIFY_FAILURE\" == \"true\" ]] || exit 0");
            L(sb, 5, "fi");
            L(sb, 5, "URL=\"https://github.com/${{ github.repository }}/actions/runs/${{ github.run_id }}\"");
            L(sb, 5, "PAYLOAD=$(jq -n --arg title \"ItchDeploy $STATUS\" --arg version \"$VERSION\" --arg url \"$URL\" --argjson color \"$COLOR\" '{embeds:[{title:$title,description:(\"Version: \"+$version),url:$url,color:$color}]}')");
            L(sb, 5, "curl --fail --silent --show-error -H 'Content-Type: application/json' -d \"$PAYLOAD\" \"$DISCORD_WEBHOOK\"");
        }

        private static void AppendUnityLicenseEnvironment(StringBuilder sb, int indent)
        {
            L(sb, indent, "env:");
            L(sb, indent + 1, "UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}");
            L(sb, indent + 1, "UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}");
            L(sb, indent + 1, "UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}");
            L(sb, indent + 1, "UNITY_SERIAL: ${{ secrets.UNITY_SERIAL }}");
        }

        private static List<string> ResolvePlatforms(
            Options options,
            ItchDeployConfig.ConfigData data)
        {
            var requested = options.platforms?.Where(platform =>
                    !string.IsNullOrWhiteSpace(platform))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var platforms = requested is { Count: > 0 }
                ? requested
                : data.channels.Keys.Distinct(StringComparer.Ordinal).ToList();

            if (requested is not { Count: > 0 } && !data.ci.buildIos)
                platforms.RemoveAll(platform => platform == "iOS");

            if (platforms.Count == 0)
                platforms.Add(data.defaultTarget);

            var order = new[]
            {
                "StandaloneWindows64",
                "StandaloneLinux64",
                "StandaloneOSX",
                "WebGL",
                "Android",
                "iOS",
            };
            platforms.Sort((left, right) =>
            {
                var leftIndex = Array.IndexOf(order, left);
                var rightIndex = Array.IndexOf(order, right);
                if (leftIndex < 0) leftIndex = int.MaxValue;
                if (rightIndex < 0) rightIndex = int.MaxValue;
                return leftIndex.CompareTo(rightIndex);
            });
            return platforms;
        }

        internal static string GetRunnerForPlatform(string platform)
        {
            return platform switch
            {
                "StandaloneWindows64" => "windows-2022",
                "StandaloneOSX" => "macos-latest",
                _ => "ubuntu-latest",
            };
        }

        private static void ValidateConfiguration(
            ItchDeployConfig.ConfigData data,
            IReadOnlyCollection<string> platforms)
        {
            if (!IsItchSlug(data.itchUser))
                throw new InvalidOperationException("itch_user must be a lowercase itch.io slug.");
            if (!IsItchSlug(data.itchGame))
                throw new InvalidOperationException("itch_game must be a lowercase itch.io slug.");
            if (string.IsNullOrWhiteSpace(data.unityVersion))
                throw new InvalidOperationException("unity_version is required.");

            if (!IsUnityVersion(data.unityVersion))
                throw new InvalidOperationException(
                    $"unity_version '{data.unityVersion}' is not a Unity version such as 6000.0.62f1.");

            var buildName = string.IsNullOrWhiteSpace(data.buildName)
                ? data.itchGame
                : data.buildName;
            if (!IsSafeBuildName(buildName))
                throw new InvalidOperationException(
                    $"build_name '{buildName}' must be a plain file name: letters, digits, dot, dash or underscore.");

            foreach (var platform in platforms)
            {
                if (!SupportedPlatforms.Contains(platform))
                    throw new InvalidOperationException($"Unsupported GitHub Actions platform '{platform}'.");
                if (!string.Equals(platform, "iOS", StringComparison.Ordinal) &&
                    (!data.channels.TryGetValue(platform, out var channel) || !IsItchSlug(channel)))
                    throw new InvalidOperationException($"A valid itch.io channel is required for {platform}.");
            }
        }

        private static bool IsItchSlug(string value)
            => !string.IsNullOrWhiteSpace(value) &&
               Regex.IsMatch(value, "^[a-z0-9][a-z0-9-]*$");

        internal static bool IsUnityVersion(string value)
            => !string.IsNullOrWhiteSpace(value) &&
               Regex.IsMatch(value, @"^\d+\.\d+\.\d+[abfpx]?\d*$");

        internal static bool IsSafeBuildName(string value)
            => !string.IsNullOrWhiteSpace(value) &&
               Regex.IsMatch(value, @"^[A-Za-z0-9][A-Za-z0-9._-]*$");

        private static string YamlQuote(string value)
            => "\"" + (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"") + "\"";

        private static string YamlJsonString(string value)
            => YamlQuote(value);

        private static string ShellQuote(string value)
            => "'" + (value ?? string.Empty).Replace("'", "'\"'\"'") + "'";

        private static void L(StringBuilder sb, int indent = 0, string value = "")
            => sb.Append(' ', indent * 2).Append(value).Append('\n');
    }
}
