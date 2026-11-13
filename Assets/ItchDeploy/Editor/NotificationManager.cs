using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using System.Net;

namespace ItchDeploy.Editor
{
    public static class NotificationManager
    {
        public static string DiscordWebhook
        {
            get => GetSecret("DiscordWebhook", "ItchDeploy_DiscordWebhook");
            set => SetSecret("DiscordWebhook", "ItchDeploy_DiscordWebhook", value);
        }

        public static string SlackWebhook
        {
            get => GetSecret("SlackWebhook", "ItchDeploy_SlackWebhook");
            set => SetSecret("SlackWebhook", "ItchDeploy_SlackWebhook", value);
        }

        public static string TelegramToken
        {
            get => GetSecret("TelegramToken", "ItchDeploy_TelegramToken");
            set => SetSecret("TelegramToken", "ItchDeploy_TelegramToken", value);
        }

        public static string TelegramChatId
        {
            get => GetSecret("TelegramChatId", "ItchDeploy_TelegramChatId");
            set => SetSecret("TelegramChatId", "ItchDeploy_TelegramChatId", value);
        }

        public static bool NotifyOnSuccess
        {
            get => EditorPrefs.GetBool("ItchDeploy_NotifyOnSuccess", false);
            set => EditorPrefs.SetBool("ItchDeploy_NotifyOnSuccess", value);
        }

        public static bool NotifyOnFailure
        {
            get => EditorPrefs.GetBool("ItchDeploy_NotifyOnFailure", true);
            set => EditorPrefs.SetBool("ItchDeploy_NotifyOnFailure", value);
        }

        public static void SendAllInBackground(string message, bool success)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                string result;
                try { result = SendAll(message, success); }
                catch (Exception ex) { result = "ERROR: " + ex.Message; }

                if (result.StartsWith("Skipped", StringComparison.Ordinal) ||
                    result == "No notifiers")
                    return;

                EditorApplication.delayCall += () =>
                    Debug.Log("[ItchDeploy] Notifications: " + result);
            });
        }

        public static string SendAll(string message, bool success)
        {
            if (success && !NotifyOnSuccess) return "Skipped (success off)";
            if (!success && !NotifyOnFailure) return "Skipped (failure off)";

            var results = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(DiscordWebhook))
                results.Add(SendDiscord(message, success));

            if (!string.IsNullOrEmpty(SlackWebhook))
                results.Add(SendSlack(message, success));

            if (!string.IsNullOrEmpty(TelegramToken) && !string.IsNullOrEmpty(TelegramChatId))
                results.Add(SendTelegram(message));

            return results.Count > 0 ? string.Join(" | ", results) : "No notifiers";
        }

        public static string SendDiscord(string message, bool success)
        {
            try
            {
                var color = success ? 3066993 : 15158332;
                var status = success ? "Success" : "Failed";
                var project = Application.productName;
                var target = EditorUserBuildSettings.activeBuildTarget.ToString();
                var version = Application.version;

                var json = $@"{{
  ""embeds"": [{{
    ""title"": ""ItchDeploy: {status}"",
    ""description"": ""{EscapeJson(message)}"",
    ""color"": {color},
    ""fields"": [
      {{""name"": ""Project"", ""value"": ""{EscapeJson(project)}"", ""inline"": true}},
      {{""name"": ""Platform"", ""value"": ""{EscapeJson(target)}"", ""inline"": true}},
      {{""name"": ""Version"", ""value"": ""{EscapeJson(version)}"", ""inline"": true}}
    ],
    ""timestamp"": ""{DateTime.UtcNow:o}""
  }}]
}}";

                using var wc = new TimeoutWebClient(10000);
                wc.Headers.Add("Content-Type", "application/json");
                wc.UploadString(DiscordWebhook, json);
                return "Discord OK";
            }
            catch (Exception ex)
            {
                return $"Discord ERROR: {ex.Message}";
            }
        }

        public static string SendSlack(string message, bool success)
        {
            try
            {
                var status = success ? "Success" : "Failed";
                var json = $@"{{""text"": ""ItchDeploy: *{status}* - {EscapeJson(Application.productName)} v{Application.version}\n{EscapeJson(message)}""}}";

                using var wc = new TimeoutWebClient(10000);
                wc.Headers.Add("Content-Type", "application/json");
                wc.UploadString(SlackWebhook, json);
                return "Slack OK";
            }
            catch (Exception ex)
            {
                return $"Slack ERROR: {ex.Message}";
            }
        }

        public static string SendTelegram(string message)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{TelegramToken}/sendMessage";
                var text = $"ItchDeploy: {message}\nProject: {Application.productName}\nTarget: {EditorUserBuildSettings.activeBuildTarget}";

                var json = $@"{{""chat_id"": ""{TelegramChatId}"", ""text"": ""{EscapeJson(text)}""}}";

                using var wc = new TimeoutWebClient(10000);
                wc.Headers.Add("Content-Type", "application/json");
                wc.UploadString(url, json);
                return "Telegram OK";
            }
            catch (Exception ex)
            {
                return $"Telegram ERROR: {ex.Message}";
            }
        }

        private static string EscapeJson(string s)
        {
            var builder = new StringBuilder();
            foreach (var character in s ?? string.Empty)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:

                        if (character < 0x20)
                            builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else
                            builder.Append(character);
                        break;
                }
            }

            return builder.ToString();
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

        private static string GetSecret(string slot, string legacyPrefKey)
        {
            var value = ItchDeploySecureStorage.LoadNotificationSecret(slot);
            if (!string.IsNullOrEmpty(value) || !EditorPrefs.HasKey(legacyPrefKey))
                return value ?? string.Empty;

            value = EditorPrefs.GetString(legacyPrefKey, string.Empty);
            EditorPrefs.DeleteKey(legacyPrefKey);
            ItchDeploySecureStorage.SaveNotificationSecret(slot, value);
            return value;
        }

        private static void SetSecret(string slot, string legacyPrefKey, string value)
        {
            EditorPrefs.DeleteKey(legacyPrefKey);
            ItchDeploySecureStorage.SaveNotificationSecret(slot, value);
        }
    }
}
