using System;
using System.Net;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    [InitializeOnLoad]
    public static class ButlerUpdater
    {
        private const string VersionCheckKey = "ItchDeploy_LastVersionCheck";
        private static bool _checkedThisSession;

        static ButlerUpdater()
        {
            EditorApplication.update += DelayedCheck;
        }

        private static void DelayedCheck()
        {
            EditorApplication.update -= DelayedCheck;

            if (!PluginUpdater.AutoCheck) return;
            StartCheck(interactive: false);
        }

        public static void ManualCheck() => StartCheck(interactive: true);

        private static void StartCheck(bool interactive)
        {
            if (!ButlerBridge.IsButlerAvailable)
            {
                if (interactive)
                    EditorUtility.DisplayDialog(
                        "ItchDeploy",
                        "butler is not installed yet.",
                        "OK");
                return;
            }

            if (!interactive)
            {
                if (_checkedThisSession) return;
                _checkedThisSession = true;
                if (!string.IsNullOrEmpty(SessionState.GetString(VersionCheckKey, "")))
                    return;
            }

            System.Threading.Tasks.Task.Run(() =>
            {
                var local = GetLocalVersion();
                var remote = FetchLatestVersion();
                EditorApplication.delayCall += () => Report(local, remote, interactive);
            });
        }

        private static void Report(string localVersion, string remoteVersion, bool interactive)
        {
            if (string.IsNullOrEmpty(localVersion))
            {
                if (interactive)
                    EditorUtility.DisplayDialog(
                        "ItchDeploy", "Could not determine the installed butler version.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(remoteVersion))
            {
                if (interactive)
                    EditorUtility.DisplayDialog(
                        "ItchDeploy", "Could not reach itch.io to check for a newer butler.", "OK");
                return;
            }

            SessionState.SetString(VersionCheckKey, DateTime.Now.ToString("yyyy-MM-dd"));
            var comparison = CompareVersions(localVersion, remoteVersion);

            if (comparison < 0)
            {
                Debug.Log($"[ItchDeploy] butler update available: {localVersion} -> {remoteVersion}");
                if (interactive &&
                    EditorUtility.DisplayDialog("ItchDeploy Update",
                        $"New butler version available:\nCurrent: {localVersion}\nLatest: {remoteVersion}\n\nDownload now?",
                        "Download", "Later"))
                {
                    ButlerInstaller.TryAutoInstall(force: true, version: remoteVersion);
                }

                return;
            }

            if (!interactive) return;

            EditorUtility.DisplayDialog(
                "ItchDeploy Update",
                comparison == 0
                    ? $"butler is up to date: v{localVersion}"
                    : $"Local butler is newer than remote:\nLocal: v{localVersion}\nRemote: v{remoteVersion}",
                "OK");
        }

        public static string GetLocalVersion()
        {
            try
            {
                var result = ButlerBridge.RunButler("version", 10000);
                var match = Regex.Match(result, @"v?(\d+\.\d+\.\d+)");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch
            {
                return null;
            }
        }

        public static string FetchLatestVersion()
        {
            try
            {
                using var wc = new TimeoutWebClient(10000);
                wc.Headers.Add("User-Agent", "ItchDeploy/" + ItchDeployVersion.Version);
                try
                {
                    var result = wc.DownloadString("https://broth.itch.zone/butler/version");
                    var match = Regex.Match(result, @"v?(\d+\.\d+\.\d+)");
                    if (match.Success) return match.Groups[1].Value;
                }
                catch { }

                using var wc2 = new TimeoutWebClient(10000);
                wc2.Headers.Add("User-Agent", "ItchDeploy/" + ItchDeployVersion.Version);
                wc2.Headers.Add("Accept", "application/vnd.github+json");
                var json = wc2.DownloadString("https://api.github.com/repos/itchio/butler/releases/latest");
                var tagMatch = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?(\\d+\\.\\d+\\.\\d+)\"");
                if (tagMatch.Success) return tagMatch.Groups[1].Value;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private sealed class TimeoutWebClient : WebClient
        {
            private readonly int _timeoutMs;

            public TimeoutWebClient(int timeoutMs) => _timeoutMs = timeoutMs;

            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                if (request == null) return null;
                request.Timeout = _timeoutMs;
                if (request is HttpWebRequest httpRequest)
                    httpRequest.ReadWriteTimeout = _timeoutMs;
                return request;
            }
        }

        private static int CompareVersions(string a, string b)
        {
            var partsA = a.Split('.');
            var partsB = b.Split('.');

            for (int i = 0; i < Math.Max(partsA.Length, partsB.Length); i++)
            {
                int va = i < partsA.Length && int.TryParse(partsA[i], out var x) ? x : 0;
                int vb = i < partsB.Length && int.TryParse(partsB[i], out var y) ? y : 0;
                if (va != vb) return va.CompareTo(vb);
            }
            return 0;
        }
    }
}
