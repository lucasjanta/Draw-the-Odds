using System;
using System.Net;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    [InitializeOnLoad]
    public static class PluginUpdater
    {
        private const string VersionUrl = ItchDeployProject.LatestRelease;
        private const string PrefKey = "ItchDeploy_LastUpdateCheck";

        public static bool AutoCheck
        {
            get => EditorPrefs.GetBool("ItchDeploy_AutoUpdateCheck", false);
            set => EditorPrefs.SetBool("ItchDeploy_AutoUpdateCheck", value);
        }

        public static string LatestVersion
        {
            get => EditorPrefs.GetString("ItchDeploy_LatestVersion", "");
            set => EditorPrefs.SetString("ItchDeploy_LatestVersion", value);
        }

        public static string ChangelogText
        {
            get => EditorPrefs.GetString("ItchDeploy_UpdateChangelog", "");
            set => EditorPrefs.SetString("ItchDeploy_UpdateChangelog", value);
        }

        public static bool HasUpdate
        {
            get
            {
                if (string.IsNullOrEmpty(LatestVersion)) return false;
                return IsNewer(LatestVersion, ItchDeployVersion.Version);
            }
        }

        static PluginUpdater()
        {
            if (AutoCheck)
                EditorApplication.update += DelayedCheck;
        }

        private static void DelayedCheck()
        {
            EditorApplication.update -= DelayedCheck;

            var lastCheck = EditorPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(lastCheck) && DateTime.TryParse(lastCheck, out var dt))
            {
                if ((DateTime.Now - dt).TotalHours < 24)
                    return;
            }

            CheckForUpdates(false);
        }

        public static void CheckForUpdates(bool interactive)
        {
            Task.Run(() => Fetch()).ContinueWith(task =>
            {
                var outcome = task.Status == TaskStatus.RanToCompletion
                    ? task.Result
                    : new FetchOutcome { Error = "the request failed" };
                EditorApplication.delayCall += () => Report(outcome, interactive);
            });
        }

        private sealed class FetchOutcome
        {
            public string Tag;
            public string Body;
            public string Error;
        }

        private static FetchOutcome Fetch()
        {
            try
            {
                using var wc = new TimeoutWebClient(10000);
                wc.Headers.Add("User-Agent", "ItchDeploy/" + ItchDeployVersion.Version);
                wc.Headers.Add("Accept", "application/vnd.github+json");
                var json = wc.DownloadString(VersionUrl);
                return new FetchOutcome { Tag = ExtractTag(json), Body = ExtractBody(json) };
            }
            catch (Exception ex)
            {
                return new FetchOutcome { Error = ex.Message };
            }
        }

        private static void Report(FetchOutcome outcome, bool interactive)
        {
            if (!string.IsNullOrEmpty(outcome.Error) || string.IsNullOrEmpty(outcome.Tag))
            {
                if (interactive)
                    EditorUtility.DisplayDialog(
                        "ItchDeploy",
                        $"Update check failed: {outcome.Error ?? "no release was returned"}",
                        "OK");
                return;
            }

            LatestVersion = outcome.Tag.TrimStart('v');
            ChangelogText = outcome.Body ?? string.Empty;
            EditorPrefs.SetString(PrefKey, DateTime.Now.ToString("O"));

            if (HasUpdate)
            {
                Debug.Log($"[ItchDeploy] Version {LatestVersion} is available (installed: {ItchDeployVersion.Version}).");
                if (interactive &&
                    EditorUtility.DisplayDialog(
                        "ItchDeploy Update",
                        $"New version available: {LatestVersion}\n\n{Truncate(outcome.Body, 300)}",
                        "Open releases", "Later"))
                {
                    Application.OpenURL(ItchDeployProject.Releases);
                }
            }
            else if (interactive)
            {
                EditorUtility.DisplayDialog(
                    "ItchDeploy",
                    $"You have the latest version: {ItchDeployVersion.Version}",
                    "OK");
            }
        }

        private sealed class TimeoutWebClient : WebClient
        {
            private readonly int _timeoutMs;

            public TimeoutWebClient(int timeoutMs) => _timeoutMs = timeoutMs;

            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                if (request == null)
                    return null;
                request.Timeout = _timeoutMs;
                if (request is HttpWebRequest httpRequest)
                    httpRequest.ReadWriteTimeout = _timeoutMs;
                return request;
            }
        }

        private static string ExtractTag(string json)
        {
            try { return JsonUtility.FromJson<ReleaseData>(json)?.tag_name; }
            catch { return null; }
        }

        private static string ExtractBody(string json)
        {
            try { return JsonUtility.FromJson<ReleaseData>(json)?.body; }
            catch { return null; }
        }

        private static bool IsNewer(string latest, string current)
        {
            if (Version.TryParse(latest, out var l) && Version.TryParse(current, out var c))
                return l > c;

            return latest != current;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s[..max] + "...";
        }

        [Serializable]
        private class ReleaseData
        {
            public string tag_name;
            public string body;
            public string html_url;
        }
    }
}
