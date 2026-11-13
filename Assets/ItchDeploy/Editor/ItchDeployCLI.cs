using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class ItchDeployCLI
    {
        public static void Deploy()
        {
            var args = Environment.GetCommandLineArgs();
            if (HasArg(args, "-itchApiKey"))
            {
                Fail(
                    "-itchApiKey is intentionally unsupported because command-line secrets are visible " +
                    "to other processes. Set BUTLER_API_KEY in the environment instead.",
                    2);
                return;
            }

            if (!TryResolveTarget(args, out var target)) return;

            var settings = ItchDeploySettings.Load();
            ItchDeployConfig.ApplyToSettings(settings, saveAsset: false);

            var originalUser = settings.itchUserName;
            var originalGame = settings.itchGameSlug;
            try
            {
                settings.itchUserName = GetArg(args, "-itchUser") ?? settings.itchUserName;
                settings.itchGameSlug = GetArg(args, "-itchGame") ?? settings.itchGameSlug;

                var channel = GetArg(args, "-itchChannel");
                if (string.IsNullOrWhiteSpace(channel))
                    channel = ResolveChannel(target, settings);

                var version = GetArg(args, "-itchVersion");

                var buildPath = GetArg(args, "-itchBuildPath");
                var succeeded = string.IsNullOrWhiteSpace(buildPath)
                    ? BuildDeployer.BuildAndDeploy(
                        target,
                        channel,
                        version,
                        settings,
                        deployAfterBuild: true)
                    : BuildDeployer.DeployExistingBuild(
                        buildPath,
                        target,
                        channel,
                        version,
                        settings);

                Exit(succeeded ? 0 : 1);
            }
            catch (Exception ex)
            {
                Fail(ex.ToString(), 1);
            }
            finally
            {
                settings.itchUserName = originalUser;
                settings.itchGameSlug = originalGame;
            }
        }

        public static void Build()
        {
            var args = new List<string>(Environment.GetCommandLineArgs())
            {
                "-buildOnly",
            };
            Exit(CLIPipeline.Execute(args.ToArray()));
        }

        public static void Status()
        {
            var args = Environment.GetCommandLineArgs();
            if (!TryResolveTarget(args, out var target)) return;

            var settings = ItchDeploySettings.Load();
            var channel = GetArg(args, "-itchChannel");
            if (string.IsNullOrWhiteSpace(channel))
                channel = ResolveChannel(target, settings);

            if (!BuildDeployer.TryValidateDeploySettings(settings, out var error))
            {
                Fail(error, 1);
                return;
            }

            var destination = PlatformChannelMap.GetChannelTarget(
                settings.itchUserName,
                settings.itchGameSlug,
                channel);
            var result = ButlerBridge.Status(destination, settings.EffectiveApiKey);
            var failed = string.IsNullOrWhiteSpace(result) ||
                         result.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         result.IndexOf("EXIT_CODE:", StringComparison.OrdinalIgnoreCase) >= 0;

            if (failed) Debug.LogError("[ItchDeploy CLI] " + result);
            else Debug.Log("[ItchDeploy CLI] " + result);
            Exit(failed ? 1 : 0);
        }

        public static void Validate()
        {
            BuildDeployer.ValidateBeforeDeploy(out var warnings, out var errors);
            foreach (var warning in warnings)
                Debug.LogWarning("[ItchDeploy CLI] " + warning);
            foreach (var error in errors)
                Debug.LogError("[ItchDeploy CLI] " + error);

            Debug.Log($"[ItchDeploy CLI] Validation: {errors.Count} errors, {warnings.Count} warnings.");
            Exit(errors.Count == 0 ? 0 : 1);
        }

        private static bool TryResolveTarget(string[] args, out BuildTarget target)
        {
            var value = GetArg(args, "-itchTarget");
            if (string.IsNullOrWhiteSpace(value))
            {
                target = EditorUserBuildSettings.activeBuildTarget;
                return true;
            }

            if (Enum.TryParse(value, ignoreCase: true, out target) && target != BuildTarget.NoTarget)
                return true;

            Fail($"Unknown Unity build target '{value}'.", 2);
            return false;
        }

        private static string ResolveChannel(BuildTarget target, ItchDeploySettings settings)
        {
            var mapping = settings.channelMappings?.FirstOrDefault(entry =>
                entry.active &&
                string.Equals(entry.buildTarget, target.ToString(), StringComparison.OrdinalIgnoreCase));
            if (mapping != null && !string.IsNullOrWhiteSpace(mapping.channel))
                return mapping.channel + (mapping.channelSuffix ?? string.Empty);
            return PlatformChannelMap.GetDefaultChannel(target);
        }

        private static bool HasArg(string[] args, string name)
            => args.Any(arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase));

        private static string GetArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private static void Fail(string message, int exitCode)
        {
            Debug.LogError("[ItchDeploy CLI] " + message);
            Exit(exitCode);
        }

        private static void Exit(int exitCode)
        {
            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }
    }
}
