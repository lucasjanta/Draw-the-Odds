using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    [InitializeOnLoad]
    public static class ScheduledBuilds
    {
        private static double _nextCheck;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool("ItchDeploy_Scheduler_Enabled", false);
            set
            {
                EditorPrefs.SetBool("ItchDeploy_Scheduler_Enabled", value);
                if (value) StartScheduler();
                else StopScheduler();
            }
        }

        public static int IntervalHours
        {
            get => EditorPrefs.GetInt("ItchDeploy_Scheduler_Interval", 24);
            set => EditorPrefs.SetInt("ItchDeploy_Scheduler_Interval", Mathf.Max(1, value));
        }

        public static int LastBuildHour
        {
            get => EditorPrefs.GetInt("ItchDeploy_Scheduler_LastHour", 0);
            set => EditorPrefs.SetInt("ItchDeploy_Scheduler_LastHour", value);
        }

        public static int LastBuildMinute
        {
            get => EditorPrefs.GetInt("ItchDeploy_Scheduler_LastMin", 0);
            set => EditorPrefs.SetInt("ItchDeploy_Scheduler_LastMin", value);
        }

        public static string LastBuildDate
        {
            get => EditorPrefs.GetString("ItchDeploy_Scheduler_LastDate", "");
            set => EditorPrefs.SetString("ItchDeploy_Scheduler_LastDate", value);
        }

        public static bool BuildOnIdle
        {
            get => EditorPrefs.GetBool("ItchDeploy_Scheduler_Idle", true);
            set => EditorPrefs.SetBool("ItchDeploy_Scheduler_Idle", value);
        }

        public static string TargetPlatform
        {
            get => EditorPrefs.GetString("ItchDeploy_Scheduler_Platform", "StandaloneWindows64");
            set => EditorPrefs.SetString("ItchDeploy_Scheduler_Platform", value);
        }

        static ScheduledBuilds()
        {
            if (Enabled)
                StartScheduler();
        }

        public static void StartScheduler()
        {
            EditorApplication.update -= SchedulerUpdate;
            EditorApplication.update += SchedulerUpdate;
            _nextCheck = EditorApplication.timeSinceStartup + 60;
            Debug.Log("[ItchDeploy] Scheduler started");
        }

        public static void StopScheduler()
        {
            EditorApplication.update -= SchedulerUpdate;
            Debug.Log("[ItchDeploy] Scheduler stopped");
        }

        private static void SchedulerUpdate()
        {
            if (!Enabled) return;
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 60;

            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (BuildDeployer.IsBuilding || BuildDeployer.IsDeploying)
                return;

            var now = DateTime.Now;
            var lastDate = LastBuildDate;

            if (!string.IsNullOrEmpty(lastDate) && DateTime.TryParse(lastDate, out var lastDt))
            {
                if ((now - lastDt).TotalHours < IntervalHours)
                    return;
            }

            if (BuildOnIdle && EditorApplication.isFocused)
                return;

            if (!TryResolveTargetPlatform(out var target))
            {
                Enabled = false;
                Debug.LogError(
                    $"[ItchDeploy] Scheduler stopped: '{TargetPlatform}' is not a supported build target.");
                return;
            }

            var settings = ItchDeploySettings.Load();
            var deployAfterBuild = settings.autoDeployAfterBuild &&
                                   BuildPlatformCatalog.CanDeployCurrentArtifactToItch(
                                       target,
                                       out _);
            if (deployAfterBuild &&
                (string.IsNullOrEmpty(settings.itchUserName) ||
                 string.IsNullOrEmpty(settings.itchGameSlug)))
                return;
            var channel = PlatformChannelMap.ResolveChannel(settings, target);

            Debug.Log($"[ItchDeploy] Scheduled build: {TargetPlatform} -> {channel}");

            if (target == BuildTarget.WebGL)
                WebGLHelper.SetCompression(settings.webglCompression);

            LastBuildDate = now.ToString("yyyy-MM-dd HH:mm");
            LastBuildHour = now.Hour;
            LastBuildMinute = now.Minute;

            BuildDeployer.BuildAndDeploy(
                target,
                channel,
                null,
                settings,
                deployAfterBuild);
        }

        internal static bool TryResolveTargetPlatform(out BuildTarget target)
        {
            target = BuildTarget.StandaloneWindows64;
            if (!Enum.TryParse(TargetPlatform, ignoreCase: false, out BuildTarget parsed))
                return false;
            if (!BuildPlatformCatalog.IsSupported(parsed))
                return false;

            target = parsed;
            return true;
        }
    }
}
