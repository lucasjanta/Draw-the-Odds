using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class BuildFixer
    {
        public class FixResult
        {
            public List<string> autoFixed = new();
            public List<string> manualRequired = new();
            public bool hasErrors => manualRequired.Count > 0;
            public bool hasWarnings => autoFixed.Count > 0 || warnings.Count > 0;
            public List<string> warnings = new();
        }

        public static void AutoFix()
        {
            var result = AnalyzeAndFix(autoFix: true);
            if (result.autoFixed.Count > 0)
                Debug.Log($"[ItchDeploy] Auto-fixed: {string.Join(", ", result.autoFixed)}");
            if (result.manualRequired.Count > 0)
            {
                var msg = "Manual fixes required:\n" + string.Join("\n", result.manualRequired);
                EditorUtility.DisplayDialog("ItchDeploy - Manual Fixes Needed", msg, "OK");
            }
            if (result.autoFixed.Count > 0 && result.manualRequired.Count == 0)
                EditorUtility.DisplayDialog("ItchDeploy", $"All issues auto-fixed!\n{string.Join("\n", result.autoFixed)}", "OK");
        }

        public static FixResult AnalyzeAndFix(bool autoFix = false)
        {
            var result = new FixResult();

            CheckScenes(result, autoFix);
            CheckProductName(result, autoFix);
            CheckCompanyName(result, autoFix);
            CheckIcon(result, autoFix);
            CheckWebGLCompression(result, autoFix);
            CheckAndroidKeystore(result, autoFix);
            CheckMacCameraDescription(result, autoFix);
            CheckScriptingBackend(result, autoFix);
            CheckColorSpace(result, autoFix);
            CheckDefaultOrientation(result, autoFix);
            CheckBuildTarget(result, autoFix);

            return result;
        }

        private static void CheckScenes(FixResult r, bool fix)
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || !scenes.Any(s => s.enabled))
            {
                r.manualRequired.Add("No enabled scenes in Build Settings. Add scenes in File > Build Settings.");
                return;
            }

            var enabled = scenes.Where(s => s.enabled).ToList();
            if (enabled.Count == 1 && enabled[0].path.Contains("SampleScene"))
            {
                r.warnings.Add("Only SampleScene is in build. If this is intentional, ignore.");
            }
        }

        private static void CheckProductName(FixResult r, bool fix)
        {
            if (string.IsNullOrEmpty(Application.productName) || Application.productName == "My Project")
            {
                r.warnings.Add($"Product Name is '{Application.productName}'. Set release metadata explicitly.");
            }
        }

        private static void CheckCompanyName(FixResult r, bool fix)
        {
            if (PlayerSettings.companyName == "DefaultCompany")
            {
                r.warnings.Add("Company Name is 'DefaultCompany'. Set the real publisher name explicitly.");
            }
        }

        private static void CheckIcon(FixResult r, bool fix)
        {
            var defaultTarget = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Unknown);
            var icons = PlayerSettings.GetIcons(defaultTarget, IconKind.Any);
            if (icons == null || icons.Length == 0 || icons.All(i => i == null))
            {
                r.warnings.Add("No application icon is configured. Choose a project-specific icon before release.");
            }
        }

        private static void CheckWebGLCompression(FixResult r, bool fix)
        {
            var isWebGL = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;
            if (!isWebGL) return;

            var compression = WebGLHelper.GetCurrentCompression();
            if (compression == WebGLHelper.CompressionFormat.Disabled)
            {
                if (fix)
                {
                    WebGLHelper.SetCompression(WebGLHelper.CompressionFormat.Brotli);
                    r.autoFixed.Add("WebGL compression set to Brotli");
                }
                else
                {
                    r.warnings.Add("WebGL compression is Disabled - builds will be large. Enable Brotli.");
                }
            }
        }

        private static void CheckAndroidKeystore(FixResult r, bool fix)
        {
            var isAndroid = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
            if (!isAndroid) return;

            if (!PlayerSettings.Android.useCustomKeystore)
            {
                if (!fix)
                    r.warnings.Add("Android: No custom keystore set. Releases won't be signed.");
            }
        }

        private static void CheckMacCameraDescription(FixResult r, bool fix)
        {
            var isMac = EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSX;
            if (!isMac) return;

            if (string.IsNullOrEmpty(PlayerSettings.macOS.cameraUsageDescription))
            {
                r.warnings.Add("macOS camera usage description is empty. Add an accurate reason if the game uses the camera.");
            }
        }

        private static void CheckScriptingBackend(FixResult r, bool fix)
        {
            var groups = new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android, BuildTargetGroup.iOS };
            foreach (var group in groups)
            {
                var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
                var backend = PlayerSettings.GetScriptingBackend(namedTarget);
                if (backend == ScriptingImplementation.Mono2x)
                {
                    r.warnings.Add($"{group}: Mono backend selected. Review IL2CPP requirements for the target; the plugin will not change backends automatically.");
                }
            }
        }

        private static void CheckColorSpace(FixResult r, bool fix)
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                r.warnings.Add("Color space is Gamma. Changing it affects rendering, so review it manually for this project.");
            }
        }

        private static void CheckDefaultOrientation(FixResult r, bool fix)
        {
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation)
            {
                r.warnings.Add($"Default orientation is {PlayerSettings.defaultInterfaceOrientation}. Verify this is intentional for mobile builds.");
            }
        }

        private static void CheckBuildTarget(FixResult r, bool fix)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            if (!BuildPlatformCatalog.IsSupported(target))
            {
                r.manualRequired.Add($"Current build target ({target}) is not supported by ItchDeploy.");
                return;
            }

            if (!BuildPlatformCatalog.IsBuildSupportInstalled(target))
                r.manualRequired.Add(
                    $"Install Unity Build Support for {PlatformChannelMap.GetDisplayName(target)} in Unity Hub.");

            if (!BuildPlatformCatalog.CanDeployCurrentArtifactToItch(target, out var reason))
                r.warnings.Add(reason);
        }
    }
}
