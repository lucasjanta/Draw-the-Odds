using System;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class CLIPipeline
    {
        internal sealed class Options
        {
            public BuildTarget target;
            public string channel;
            public string version;
            public bool deploy = true;
            public bool autoFix;
            public bool archive;
            public bool notify;
        }

        public static void Run()
        {
            var exitCode = Execute(Environment.GetCommandLineArgs());
            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        internal static int Execute(string[] args)
        {
            if (!TryParseArguments(args, out var options, out var parseError))
            {
                Debug.LogError("[ItchDeploy CLI] " + parseError + "\n" + Usage);
                return 2;
            }

            var settings = ItchDeploySettings.Load();
            ItchDeployConfig.ApplyToSettings(settings, saveAsset: false);

            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildPipeline.GetBuildTargetGroup(options.target),
                    options.target))
            {
                Debug.LogError(
                    $"[ItchDeploy CLI] Build support for {options.target} is not installed in this Unity Editor.");
                return 3;
            }

            if (options.autoFix)
                BuildFixer.AutoFix();

            var previousArchive = BuildArchive.AutoArchive;
            try
            {
                BuildArchive.AutoArchive = options.archive || previousArchive;
                Debug.Log(
                    $"[ItchDeploy CLI] {(options.deploy ? "Build + deploy" : "Build only")}: " +
                    $"{options.target} -> {options.channel}, version " +
                    $"{(string.IsNullOrEmpty(options.version) ? "(from project settings)" : options.version)}");

                var succeeded = BuildDeployer.BuildAndDeploy(
                    options.target,
                    options.channel,
                    options.version,
                    settings,
                    deployAfterBuild: options.deploy);

                if (!succeeded)
                {
                    Debug.LogError(
                        "[ItchDeploy CLI] Pipeline failed. See the preceding Unity and butler logs.");
                    return 1;
                }

                // Archiving normally happens on the way to publishing. A build-only run
                // never reaches that step, so an explicit -archive would quietly do
                // nothing - the one outcome a flag must never have.
                if (options.archive && !options.deploy)
                    ArchiveCompletedBuild(options);

                if (options.notify)
                {
                    NotificationManager.SendAll(
                        $"CLI: {Application.productName} v{options.version} " +
                        (options.deploy ? $"deployed to {options.channel}" : "built"),
                        true);
                }

                Debug.Log("[ItchDeploy CLI] Pipeline complete.");
                return 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItchDeploy CLI] Unhandled failure: {ex}");
                return 1;
            }
            finally
            {
                BuildArchive.AutoArchive = previousArchive;
            }
        }

        internal static bool TryParseArguments(
            string[] args,
            out Options options,
            out string error)
        {
            var config = ItchDeployConfig.Load();
            var targetName = config.defaultTarget;
            var channel = string.Empty;
            string version = null;
            var deploy = true;
            var autoFix = false;
            var archive = false;
            var notify = false;

            args ??= Array.Empty<string>();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg.ToLowerInvariant())
                {
                    case "-platform":
                    case "-itchtarget":
                        if (!TryReadValue(args, ref i, arg, out targetName, out error))
                        {
                            options = null;
                            return false;
                        }
                        break;
                    case "-channel":
                    case "-itchchannel":
                        if (!TryReadValue(args, ref i, arg, out channel, out error))
                        {
                            options = null;
                            return false;
                        }
                        break;
                    case "-version":
                    case "-itchversion":
                        if (!TryReadValue(args, ref i, arg, out version, out error))
                        {
                            options = null;
                            return false;
                        }
                        break;
                    case "-buildonly":
                    case "-no-deploy":
                        deploy = false;
                        break;
                    case "-deploy":
                        deploy = true;
                        break;
                    case "-autofix":
                        autoFix = true;
                        break;
                    case "-archive":
                        archive = true;
                        break;
                    case "-notify":
                        notify = true;
                        break;
                }
            }

            if (!Enum.TryParse(targetName, ignoreCase: true, out BuildTarget target) ||
                target == BuildTarget.NoTarget)
            {
                options = null;
                error = $"Unknown Unity build target '{targetName}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(channel))
            {
                channel = config.channels.TryGetValue(target.ToString(), out var configured)
                    ? configured
                    : PlatformChannelMap.GetDefaultChannel(target);
            }

            if (string.IsNullOrWhiteSpace(channel))
            {
                options = null;
                error = $"No itch.io channel is configured for {target}.";
                return false;
            }

            // An absent -itchVersion is not an error: the deployer then applies automatic
            // versioning, or the project version. TryReadValue already rejects the flag
            // when it is present without a value.

            options = new Options
            {
                target = target,
                channel = channel.Trim(),
                version = version?.Trim(),
                deploy = deploy,
                autoFix = autoFix,
                archive = archive,
                notify = notify,
            };
            error = string.Empty;
            return true;
        }

        private static void ArchiveCompletedBuild(Options options)
        {
            if (!BuildDeployer.TryGetLastBuild(
                    options.target,
                    options.channel,
                    out var version,
                    out var buildDirectory) ||
                string.IsNullOrEmpty(buildDirectory))
            {
                Debug.LogWarning(
                    "[ItchDeploy CLI] -archive was requested but the finished build could not " +
                    "be located, so no archive was written.");
                return;
            }

            var archivePath = BuildArchive.ArchiveBuild(buildDirectory, options.target, version);
            if (string.IsNullOrEmpty(archivePath))
            {
                Debug.LogError("[ItchDeploy CLI] -archive was requested but the archive failed.");
                return;
            }

            Debug.Log("[ItchDeploy CLI] Archived: " + archivePath);
        }

        private static bool TryReadValue(
            string[] args,
            ref int index,
            string option,
            out string value,
            out string error)
        {
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                value = string.Empty;
                error = $"Option {option} requires a value.";
                return false;
            }

            value = args[++index];
            error = string.Empty;
            return true;
        }

        private const string Usage =
            "Usage: Unity -batchmode -quit -projectPath <path> " +
            "-executeMethod CLIPipeline.Run -- " +
            "[-platform StandaloneWindows64] [-channel windows] [-version 1.2.3] " +
            "[-buildOnly] [-autofix] [-archive] [-notify]. " +
            "Provide BUTLER_API_KEY through the environment; never pass secrets on the command line.";
    }
}
