using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class ButlerInstaller
    {
        public enum Status { NotInstalled, Downloading, Installed, Error }

        public static Status CurrentStatus { get; private set; } = Status.NotInstalled;
        public static float Progress { get; private set; }
        public static string StatusMessage { get; private set; } = "";
        public static string LocalButlerPath { get; private set; }

        public static string StorageRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ItchDeploy", "butler");

        public static bool TryAutoInstall(bool force = false, string version = null)
        {
            if (CurrentStatus == Status.Downloading) return false;

            var platform = GetPlatform();
            var exeName = platform == "windows" ? "butler.exe" : "butler";
            var destDir = Path.Combine(StorageRoot, platform);
            var destPath = Path.Combine(destDir, exeName);

            if (!force && File.Exists(destPath))
            {
                LocalButlerPath = destPath;
                CurrentStatus = Status.Installed;
                return true;
            }

            DownloadButler(platform, destDir, destPath, version);
            return false;
        }

        private static string GetPlatform()
        {
#if UNITY_EDITOR_WIN
            return "windows";
#elif UNITY_EDITOR_OSX
            return "darwin";
#else
            return "linux";
#endif
        }

        private static void DownloadButler(string platform, string destDir, string destPath, string version)
        {
            CurrentStatus = Status.Downloading;
            Progress = 0f;
            EditorApplication.update += ShowProgressWhileDownloading;

            if (string.IsNullOrEmpty(version))
                version = ButlerUpdater.FetchLatestVersion();

            if (string.IsNullOrEmpty(version))
            {
                CurrentStatus = Status.Error;
                StatusMessage = "Could not fetch latest version";
                EditorApplication.update -= ShowProgressWhileDownloading;
                Debug.LogError("[ButlerInstaller] Failed to get latest butler version");
                return;
            }

            StatusMessage = $"Downloading butler v{version}...";

            var url = platform switch
            {
                "windows" => $"https://github.com/itchio/butler/releases/download/v{version}/butler-windows-amd64.zip",
                "darwin" => $"https://github.com/itchio/butler/releases/download/v{version}/butler-darwin-amd64.zip",
                "linux" => $"https://github.com/itchio/butler/releases/download/v{version}/butler-linux-amd64.zip",
                _ => throw new NotSupportedException($"Platform {platform} not supported"),
            };

            var zipPath = Path.Combine(Application.temporaryCachePath, "butler-download.zip");

            try
            {
                Directory.CreateDirectory(destDir);

                using var wc = new WebClient();
                wc.DownloadProgressChanged += (s, e) =>
                {
                    Progress = e.ProgressPercentage / 100f;
                    StatusMessage = $"Downloading butler... {e.ProgressPercentage}%";
                };

                wc.DownloadFileCompleted += (s, e) =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        EditorApplication.update -= ShowProgressWhileDownloading;
                        EditorUtility.ClearProgressBar();

                        if (e.Error != null)
                        {
                            CurrentStatus = Status.Error;
                            StatusMessage = $"Download failed: {e.Error.Message}";
                            return;
                        }

                        if (e.Cancelled)
                        {
                            CurrentStatus = Status.Error;
                            StatusMessage = "Download cancelled";
                            return;
                        }

                        ExtractAndInstall(zipPath, destDir, destPath);
                    };
                };

                wc.DownloadFileAsync(new Uri(url), zipPath);
            }
            catch (Exception ex)
            {
                CurrentStatus = Status.Error;
                StatusMessage = $"Error: {ex.Message}";
                EditorApplication.update -= ShowProgressWhileDownloading;
                EditorUtility.ClearProgressBar();
            }
        }

        private static void ExtractAndInstall(string zipPath, string destDir, string destPath)
        {
            try
            {
                StatusMessage = "Extracting butler...";
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, recursive: true);
                Directory.CreateDirectory(destDir);

                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, destDir, overwriteFiles: true);

                var files = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                var extracted = false;
                foreach (var f in files)
                {
                    var name = Path.GetFileName(f);
                    if (name == "butler.exe" || name == "butler")
                    {
                        File.Copy(f, destPath, overwrite: true);
                        extracted = true;
                        break;
                    }
                }

                File.Delete(zipPath);

                if (!extracted)
                {
                    CurrentStatus = Status.Error;
                    StatusMessage = "The downloaded archive did not contain a butler executable.";
                    EditorApplication.update -= ShowProgressWhileDownloading;
                    EditorUtility.ClearProgressBar();
                    Debug.LogError("[ButlerInstaller] " + StatusMessage);
                    return;
                }

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{destPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(5000);
#endif

                LocalButlerPath = destPath;
                EditorUtility.ClearProgressBar();
                ButlerBridge.RefreshPath();

                var version = ButlerBridge.Run("version", 10000);
                var match = System.Text.RegularExpressions.Regex.Match(
                    version.Output ?? string.Empty, @"v?(\d+\.\d+\.\d+)");
                if (!version.Succeeded || !match.Success)
                {
                    CurrentStatus = Status.Error;
                    StatusMessage = "The downloaded butler could not be started."
#if UNITY_EDITOR_OSX
                        + " itch.io publishes butler only as an Intel binary, so Apple Silicon Macs need Rosetta 2:" +
                        " run 'softwareupdate --install-rosetta' and install again."
#endif
                        ;
                    LocalButlerPath = null;
                    EditorApplication.update -= ShowProgressWhileDownloading;
                    Debug.LogError($"[ButlerInstaller] {StatusMessage}\n{version}");
                    return;
                }

                CurrentStatus = Status.Installed;
                StatusMessage = "butler installed!";
                Debug.Log($"[ButlerInstaller] Installed butler {match.Groups[1].Value} to: {destPath}");

                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                CurrentStatus = Status.Error;
                StatusMessage = $"Extraction failed: {ex.Message}";
                EditorApplication.update -= ShowProgressWhileDownloading;
                EditorUtility.ClearProgressBar();
            }
        }

        private static void ShowProgressWhileDownloading()
        {
            if (CurrentStatus == Status.Downloading)
                EditorUtility.DisplayProgressBar("ItchDeploy", $"Downloading butler... {(int)(Progress * 100)}%", Progress);
        }

        public static void DeleteInstalled()
        {
            var platform = GetPlatform();
            var dir = Path.Combine(StorageRoot, platform);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
                ButlerBridge.RefreshPath();
            }
            CurrentStatus = Status.NotInstalled;
            LocalButlerPath = null;
        }
    }
}
