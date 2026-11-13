using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace ItchDeploy.Editor
{
    public static class BuildPlatformCatalog
    {
        public enum BuildReadinessIssue
        {
            None,
            UnsupportedTarget,
            MissingBuildSupport,
            MissingScriptingBackend,
            UnsupportedAndroidInputHandling,
        }

        private static readonly BuildTarget[] TargetsInternal =
        {
            BuildTarget.WebGL,
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneLinux64,
            BuildTarget.StandaloneOSX,
            BuildTarget.Android,
            BuildTarget.iOS,
        };

        public static IReadOnlyList<BuildTarget> Targets => TargetsInternal;

        public static bool IsSupported(BuildTarget target)
            => Array.IndexOf(TargetsInternal, target) >= 0;

        public static bool IsBuildSupportInstalled(BuildTarget target)
        {
            if (!IsSupported(target))
                return false;

            try
            {
                return BuildPipeline.IsBuildTargetSupported(
                    BuildPipeline.GetBuildTargetGroup(target),
                    target);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryValidateBuildSupport(BuildTarget target, out string error)
            => TryValidateBuildSupport(target, out _, out error);

        public static bool TryValidateBuildSupport(
            BuildTarget target,
            out BuildReadinessIssue issue,
            out string error)
        {
            if (!IsSupported(target))
            {
                issue = BuildReadinessIssue.UnsupportedTarget;
                error = $"The {target} build target is not supported by ItchDeploy.";
                return false;
            }

            if (!IsBuildSupportInstalled(target))
            {
                issue = BuildReadinessIssue.MissingBuildSupport;
                error =
                    $"Unity Build Support for {PlatformChannelMap.GetDisplayName(target)} is not installed. " +
                    "Add the module in Unity Hub, then restart the Editor.";
                return false;
            }

            if (!IsSelectedScriptingBackendAvailable(target, out var backend))
            {
                issue = BuildReadinessIssue.MissingScriptingBackend;
                error =
                    $"The selected {backend} scripting backend is unavailable for " +
                    $"{PlatformChannelMap.GetDisplayName(target)} in this Unity installation. " +
                    "Install matching Build Support, switch the backend in Player Settings, " +
                    "or build on a runner whose operating system matches the target.";
                return false;
            }

            issue = BuildReadinessIssue.None;
            error = string.Empty;
            return true;
        }

        public static bool TryValidateBuildConfiguration(
            BuildTarget target,
            out BuildReadinessIssue issue,
            out string error)
        {
            if (!TryValidateBuildSupport(target, out issue, out error))
                return false;

            if (target == BuildTarget.Android &&
                !IsAndroidInputHandlingSupported(GetActiveInputHandler()))
            {
                issue = BuildReadinessIssue.UnsupportedAndroidInputHandling;
                error =
                    "Android does not support Active Input Handling = Both. " +
                    "Choose Input System Package (New) or Input Manager (Old) in Player Settings before building.";
                return false;
            }

            issue = BuildReadinessIssue.None;
            error = string.Empty;
            return true;
        }

        public static bool TrySetAndroidInputHandling(
            int activeInputHandler,
            out string error)
        {
            error = string.Empty;
            if (activeInputHandler != 0 && activeInputHandler != 1)
            {
                error = "Choose Input Manager (Old) or Input System Package (New).";
                return false;
            }

            try
            {
                var playerSettings = AssetDatabase
                    .LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")
                    .FirstOrDefault();
                if (playerSettings == null)
                {
                    error = "Unity Player Settings could not be loaded.";
                    return false;
                }

                var serializedSettings = new SerializedObject(playerSettings);
                var property = serializedSettings.FindProperty("activeInputHandler");
                if (property == null)
                {
                    error = "Active Input Handling was not found in Player Settings.";
                    return false;
                }

                if (property.intValue == activeInputHandler)
                    return true;

                Undo.RecordObject(playerSettings, "Set Android Active Input Handling");
                property.intValue = activeInputHandler;
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(playerSettings);
                AssetDatabase.SaveAssets();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal static bool HasScriptingBackendVariation(
            IEnumerable<string> variationNames,
            ScriptingImplementation backend)
        {
            if (variationNames == null)
                return false;

            var marker = backend == ScriptingImplementation.IL2CPP
                ? "il2cpp"
                : "mono";
            return variationNames.Any(name =>
                string.Equals(name, marker, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_" + marker, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsAndroidInputHandlingSupported(int activeInputHandler)
            => activeInputHandler != 2;

        private static bool IsSelectedScriptingBackendAvailable(
            BuildTarget target,
            out ScriptingImplementation backend)
        {
            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            backend = PlayerSettings.GetScriptingBackend(
                NamedBuildTarget.FromBuildTargetGroup(targetGroup));

            if (target == BuildTarget.WebGL)
                return true;

            var moduleDirectoryName = target switch
            {
                BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 =>
                    "WindowsStandaloneSupport",
                BuildTarget.StandaloneLinux64 => "LinuxStandaloneSupport",
                BuildTarget.StandaloneOSX => "MacStandaloneSupport",
                BuildTarget.Android => "AndroidPlayer",
                BuildTarget.iOS => "iOSSupport",
                _ => string.Empty,
            };
            if (string.IsNullOrEmpty(moduleDirectoryName))
                return true;

            var variationsPath = Path.Combine(
                EditorApplication.applicationContentsPath,
                "PlaybackEngines",
                moduleDirectoryName,
                "Variations");
            if (!Directory.Exists(variationsPath))
                return false;

            try
            {
                return HasScriptingBackendVariation(
                    Directory.GetDirectories(variationsPath)
                        .Select(Path.GetFileName),
                    backend);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static int GetActiveInputHandler()
        {
            try
            {
                var playerSettings = AssetDatabase
                    .LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")
                    .FirstOrDefault();
                if (playerSettings == null)
                    return -1;

                var serializedSettings = new SerializedObject(playerSettings);
                return serializedSettings.FindProperty("activeInputHandler")?.intValue ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        public static bool CanDeployCurrentArtifactToItch(BuildTarget target, out string reason)
        {
            if (target == BuildTarget.iOS)
            {
                reason =
                    "Unity creates an unsigned Xcode project for iOS. Build it here, then sign and export the IPA on macOS.";
                return false;
            }

            if (target == BuildTarget.Android && EditorUserBuildSettings.buildAppBundle)
            {
                reason =
                    "Android App Bundle (AAB) is intended for Google Play and is not directly installable from itch.io. " +
                    "Select APK for itch.io deployment.";
                return false;
            }

            if (!IsSupported(target))
            {
                reason = $"The {target} build target is not supported by ItchDeploy.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static bool TryValidateDeployArtifact(
            string buildDirectory,
            BuildTarget target,
            out string error)
        {
            if (target == BuildTarget.iOS)
            {
                error =
                    "An unsigned iOS Xcode project cannot be deployed as an installable itch.io build. " +
                    "Sign and export an IPA on macOS first.";
                return false;
            }

            if (target == BuildTarget.Android)
            {
                var hasApk = HasTopLevelFile(buildDirectory, "*.apk");
                if (hasApk)
                {
                    error = string.Empty;
                    return true;
                }

                if (HasTopLevelFile(buildDirectory, "*.aab"))
                {
                    error =
                        "The selected Android build contains an AAB, which is not directly installable from itch.io. " +
                        "Build an APK for this channel.";
                    return false;
                }
            }

            if (!LooksLikeBuildOutput(buildDirectory, target))
            {
                error = $"The selected folder does not contain a complete {PlatformChannelMap.GetDisplayName(target)} build.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static string GetBuildLocation(
            string buildDirectory,
            BuildTarget target,
            string productName,
            bool androidAppBundle)
        {
            return target switch
            {
                BuildTarget.WebGL => Path.Combine(buildDirectory, "WebGL"),
                BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 =>
                    Path.Combine(buildDirectory, productName + ".exe"),
                BuildTarget.StandaloneLinux64 =>
                    Path.Combine(buildDirectory, productName + ".x86_64"),
                BuildTarget.StandaloneOSX =>
                    Path.Combine(buildDirectory, productName + ".app"),
                BuildTarget.Android =>
                    Path.Combine(buildDirectory, productName + (androidAppBundle ? ".aab" : ".apk")),
                BuildTarget.iOS => Path.Combine(buildDirectory, "iOS"),
                _ => Path.Combine(buildDirectory, productName),
            };
        }

        public static bool LooksLikeBuildOutput(string path, BuildTarget target)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return false;

            try
            {
                return target switch
                {
                    BuildTarget.WebGL => FindWebGLRoot(path) != null,
                    BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 =>
                        HasTopLevelFile(path, "*.exe"),
                    BuildTarget.StandaloneLinux64 =>
                        HasTopLevelFile(path, "*.x86_64") || HasTopLevelFile(path, "*.x86"),
                    BuildTarget.StandaloneOSX =>
                        Directory.GetDirectories(path, "*.app", SearchOption.TopDirectoryOnly).Length > 0,
                    BuildTarget.Android =>
                        HasTopLevelFile(path, "*.apk") || HasTopLevelFile(path, "*.aab"),
                    BuildTarget.iOS => LooksLikeXcodeProject(path),
                    _ => Directory.EnumerateFileSystemEntries(path).Any(),
                };
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static BuildTarget DetectBuildTarget(string path, BuildTarget fallback)
        {
            foreach (var target in TargetsInternal)
            {
                if (LooksLikeBuildOutput(path, target))
                    return target;
            }

            return fallback;
        }

        public static string FindWebGLRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return null;

            if (File.Exists(Path.Combine(path, "index.html")))
                return path;

            var conventional = Path.Combine(path, "WebGL");
            if (File.Exists(Path.Combine(conventional, "index.html")))
                return conventional;

            try
            {
                return Directory.GetDirectories(path)
                    .FirstOrDefault(directory => File.Exists(Path.Combine(directory, "index.html")));
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static bool LooksLikeXcodeProject(string path)
        {
            var project = Directory.GetDirectories(path, "*.xcodeproj", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(directory => File.Exists(Path.Combine(directory, "project.pbxproj")));
            if (project != null)
                return true;

            return Directory.Exists(Path.Combine(path, "Classes")) &&
                   Directory.Exists(Path.Combine(path, "Libraries"));
        }

        private static bool HasTopLevelFile(string path, string pattern)
            => Directory.Exists(path) &&
               Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly).Length > 0;
    }
}
