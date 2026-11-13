using System;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ItchDeploy.Editor
{
    [InitializeOnLoad]
    public class DeployWatcher : IPostprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public static bool WatcherEnabled
        {
            get => EditorPrefs.GetBool("ItchDeploy_WatcherEnabled", false);
            set => EditorPrefs.SetBool("ItchDeploy_WatcherEnabled", value);
        }

        public static bool AutoDeployOnBuild
        {
            get => EditorPrefs.GetBool("ItchDeploy_AutoDeployOnBuild", false);
            set => EditorPrefs.SetBool("ItchDeploy_AutoDeployOnBuild", value);
        }

        public static string WatchedBuildPath
        {
            get => EditorPrefs.GetString("ItchDeploy_WatchedPath", "");
            set => EditorPrefs.SetString("ItchDeploy_WatchedPath", value);
        }

        private static FileSystemWatcher _fsWatcher;
        private static DateTime _lastDeployTime = DateTime.MinValue;
        private static readonly SynchronizationContext UnityContext = SynchronizationContext.Current;

        static DeployWatcher()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopFileWatcher;
            EditorApplication.quitting += StopFileWatcher;
            if (WatcherEnabled)
                StartFileWatcher();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!AutoDeployOnBuild) return;

            if (BuildDeployer.IsBuilding) return;
            if (report.summary.result != BuildResult.Succeeded) return;

            var target = report.summary.platform;
            if (!BuildPlatformCatalog.CanDeployCurrentArtifactToItch(
                    target,
                    out var buildOnlyReason))
            {
                Debug.Log($"[ItchDeploy] Auto-deploy skipped for {target}: {buildOnlyReason}");
                return;
            }

            var settings = ItchDeploySettings.Load();
            if (string.IsNullOrEmpty(settings.itchUserName) ||
                string.IsNullOrEmpty(settings.itchGameSlug) ||
                !settings.HasApiKey)
            {
                Debug.LogWarning("[ItchDeploy] Auto-deploy skipped: credentials not configured");
                return;
            }

            var channel = GetDefaultChannel(target, settings);
            var outputDir = report.summary.outputPath;

            if (string.IsNullOrEmpty(outputDir)) return;

            var buildDir = target == BuildTarget.WebGL
                ? Path.GetDirectoryName(outputDir)
                : Path.GetDirectoryName(outputDir);

            if (string.IsNullOrEmpty(buildDir) || !Directory.Exists(buildDir))
            {
                buildDir = Path.GetDirectoryName(report.summary.outputPath);
            }

            Debug.Log($"[ItchDeploy] Auto-deploying {target} -> {channel} (v{Application.version})");

            if (target == BuildTarget.WebGL)
                WebGLHelper.SetCompression(settings.webglCompression);

            BuildDeployer.DeployBuild(buildDir ?? Application.dataPath, target, channel, Application.version, settings);
        }

        public static void StartFileWatcher()
        {
            StopFileWatcher();

            var path = WatchedBuildPath;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                path = Path.Combine(Application.dataPath, "..", "Builds");
                if (!Directory.Exists(path))
                {
                    Debug.LogWarning("[ItchDeploy] Watched path not found, watcher not started");
                    return;
                }
                WatchedBuildPath = path;
            }

            try
            {
                _fsWatcher = new FileSystemWatcher(path, "*.*")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                };

                _fsWatcher.Changed += OnBuildChanged;
                _fsWatcher.Created += OnBuildChanged;
                _fsWatcher.Deleted += OnBuildChanged;

                Debug.Log($"[ItchDeploy] File watcher started on: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItchDeploy] Failed to start file watcher: {ex.Message}");
            }
        }

        public static void StopFileWatcher()
        {
            if (_fsWatcher != null)
            {
                _fsWatcher.EnableRaisingEvents = false;
                _fsWatcher.Changed -= OnBuildChanged;
                _fsWatcher.Created -= OnBuildChanged;
                _fsWatcher.Deleted -= OnBuildChanged;
                _fsWatcher.Dispose();
                _fsWatcher = null;
            }
        }

        private static void OnBuildChanged(object sender, FileSystemEventArgs e)
        {
            if ((DateTime.UtcNow - _lastDeployTime).TotalSeconds < 30) return;
            if (!LooksLikeBuildArtifact(e.Name)) return;

            var folder = Path.GetDirectoryName(e.FullPath);
            if (folder == null) return;

            _lastDeployTime = DateTime.UtcNow;
            var parentDir = new DirectoryInfo(folder).Parent?.FullName ?? folder;
            RunOnMainThread(() => DeployWatchedFolder(parentDir));
        }

        internal static bool LooksLikeBuildArtifact(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            if (string.Equals(Path.GetFileName(fileName), "index.html", StringComparison.OrdinalIgnoreCase))
                return true;

            var extension = Path.GetExtension(fileName);
            return extension switch
            {
                ".exe" or ".so" or ".dylib" or ".data" or ".wasm" or ".x86_64" or ".apk" or ".aab" => true,
                _ => false,
            };
        }

        private static void DeployWatchedFolder(string parentDir)
        {
            if (BuildDeployer.IsBuilding || BuildDeployer.IsDeploying) return;

            var settings = ItchDeploySettings.Load();
            if (string.IsNullOrEmpty(settings.itchUserName) ||
                string.IsNullOrEmpty(settings.itchGameSlug))
                return;

            var target = DetectBuildTarget(parentDir);
            if (!BuildPlatformCatalog.CanDeployCurrentArtifactToItch(target, out _))
                return;

            var channel = GetDefaultChannel(target, settings);
            Debug.Log($"[ItchDeploy] File change detected, deploying {target} -> {channel}");
            BuildDeployer.DeployExistingBuild(parentDir, target, channel, Application.version, settings);
        }

        private static void RunOnMainThread(Action action)
        {
            if (UnityContext != null)
                UnityContext.Post(_ => action(), null);
            else
                EditorApplication.delayCall += () => action();
        }

        private static BuildTarget DetectBuildTarget(string dir)
        {
            return BuildPlatformCatalog.DetectBuildTarget(
                dir,
                EditorUserBuildSettings.activeBuildTarget);
        }

        private static string GetDefaultChannel(BuildTarget target, ItchDeploySettings settings)
            => PlatformChannelMap.ResolveChannel(settings, target);
    }
}
