using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class ItchIOAPI
    {
        private const string ApiBase = "https://api.itch.io";
        private const string LegacyBase = "https://itch.io/api/1";
        private const int RequestTimeoutMs = 10000;

        private static string ApiKey => ItchDeployCredentials.ResolveProfileApiKey();

        public static bool HasAuth => !string.IsNullOrEmpty(ApiKey);

        public class GameInfo
        {
            public int id;
            public string title;
            public string url;
            public string classification;
            public int viewsCount;
            public int downloadsCount;
            public int purchasesCount;
            public DateTime? createdAt;
            public bool published;
            public string minPrice;
            public string shortText;
            public List<string> traits = new();
        }

        public class UserProfile
        {
            public int id;
            public string username;
            public string displayName;
            public string url;
            public bool developer;
            public bool pressUser;
            public string coverUrl;
        }

        public class DownloadStats
        {
            public int totalDownloads;
            public int todayDownloads;
            public int thisMonthDownloads;
            public string currency;
            public int earnings;
            public string earningsFormatted;
        }

        public class AccessCheck
        {
            public bool success;
            public string message;
            public UserProfile profile;
            public GameInfo game;
        }

        public static UserProfile GetProfile()
        {
            return GetProfile(ApiKey);
        }

        internal static UserProfile GetProfile(string apiKey)
        {
            var json = Get($"{ApiBase}/profile", apiKey);
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                var data = JsonUtility.FromJson<ProfileResponse>(json);
                if (data?.user == null || string.IsNullOrWhiteSpace(data.user.username))
                    return null;

                return new UserProfile
                {
                    id = data.user.id,
                    username = data.user.username,
                    displayName = data.user.display_name,
                    url = data.user.url,
                    developer = data.user.developer,
                    pressUser = data.user.press_user,
                    coverUrl = data.user.cover_url,
                };
            }
            catch { return null; }
        }

        [Serializable]
        private class ProfileResponse
        {
            public UserData user;
        }

        [Serializable]
        private class UserData
        {
            public int id;
            public string username;
            public string display_name;
            public string url;
            public bool developer;
            public bool press_user;
            public string cover_url;
        }

        public static List<GameInfo> GetMyGames()
        {
            return GetMyGames(ApiKey);
        }

        internal static List<GameInfo> GetMyGames(string apiKey)
        {
            var json = Get($"{ApiBase}/profile/games", apiKey);
            if (string.IsNullOrEmpty(json))
                json = LegacyGet("my-games", apiKey);
            if (string.IsNullOrEmpty(json)) return new List<GameInfo>();

            try
            {
                var data = JsonUtility.FromJson<GamesResponse>(json);
                if (data?.games == null) return new List<GameInfo>();

                var games = new List<GameInfo>();
                foreach (var g in data.games)
                {
                    games.Add(new GameInfo
                    {
                        id = g.id,
                        title = g.title,
                        url = g.url,
                        classification = g.classification,
                        viewsCount = g.views_count,
                        downloadsCount = g.downloads_count,
                        purchasesCount = g.purchases_count,
                        published = g.published,
                        minPrice = g.min_price?.ToString() ?? "0",
                        shortText = g.short_text ?? "",
                        traits = g.traits ?? new List<string>(),
                    });
                }
                return games;
            }
            catch { return new List<GameInfo>(); }
        }

        [Serializable]
        private class GamesResponse
        {
            public List<GameData> games;
        }

        [Serializable]
        private class GameData
        {
            public int id;
            public string title;
            public string url;
            public string classification;
            public int views_count;
            public int downloads_count;
            public int purchases_count;
            public bool published;
            public int? min_price;
            public string short_text;
            public List<string> traits;
            public string created_at;
        }

        [Serializable]
        private class DownloadsResponse
        {
            public List<DownloadEntry> downloads;
        }

        [Serializable]
        private class DownloadEntry
        {
            public string date;
            public int? count;
        }

        [Serializable]
        private class SingleGameResponse
        {
            public GameData game;
        }

        public static AccessCheck CheckConfiguredAccess(ItchDeploySettings settings)
        {
            if (settings == null)
                return new AccessCheck { message = "Deployment settings are unavailable." };
            if (!HasAuth)
                return new AccessCheck { message = "No itch.io API key is saved." };
            if (string.IsNullOrWhiteSpace(settings.itchUserName) || string.IsNullOrWhiteSpace(settings.itchGameSlug))
                return new AccessCheck { message = "Set the itch.io username and project URL first." };

            var profile = GetProfile();
            if (profile == null)
                return new AccessCheck
                {
                    message = "itch.io rejected the API key or the network request failed."
                };

            var games = GetMyGames();
            var expectedUrl = $"/{settings.itchGameSlug.Trim().Trim('/')}";
            var game = games.FirstOrDefault(g =>
                !string.IsNullOrEmpty(g.url)
                && g.url.TrimEnd('/').EndsWith(expectedUrl, StringComparison.OrdinalIgnoreCase));

            if (game == null)
            {
                return new AccessCheck
                {
                    profile = profile,
                    message = $"Connected as {profile.username}, but this key cannot see '{settings.itchGameSlug}'. " +
                              "Check the project URL or use an account with edit access."
                };
            }

            return new AccessCheck
            {
                success = true,
                profile = profile,
                game = game,
                message = $"Access confirmed: {profile.username} can publish to {game.title}. " +
                          "No build was created and nothing was uploaded."
            };
        }

        private static string Get(string url)
        {
            return Get(url, ApiKey);
        }

        private static string Get(string url, string apiKey, string safeLogUrl = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;

            try
            {
                using var wc = new TimeoutWebClient(RequestTimeoutMs);
                wc.Encoding = Encoding.UTF8;
                wc.Headers.Add("Authorization", $"Bearer {apiKey}");
                wc.Headers.Add("User-Agent", "ItchDeploy/1.0");
                return wc.DownloadString(url);
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response?.StatusCode == HttpStatusCode.Forbidden ||
                    response?.StatusCode == HttpStatusCode.Unauthorized)
                    return null;

                var displayUrl = string.IsNullOrWhiteSpace(safeLogUrl)
                    ? url
                    : safeLogUrl;
                var safeMessage = string.IsNullOrEmpty(apiKey)
                    ? ex.Message
                    : ex.Message.Replace(apiKey, "[redacted]");
                Debug.LogWarning($"[ItchIOAPI] GET {displayUrl} failed: {safeMessage}");
                return null;
            }
        }

        private static string LegacyGet(string endpoint)
        {
            return LegacyGet(endpoint, ApiKey);
        }

        private static string LegacyGet(string endpoint, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;
            return Get(
                $"{LegacyBase}/{Uri.EscapeDataString(apiKey)}/{endpoint}",
                apiKey,
                $"{LegacyBase}/[redacted]/{endpoint}");
        }

        private sealed class TimeoutWebClient : WebClient
        {
            private readonly int _timeoutMs;

            public TimeoutWebClient(int timeoutMs)
            {
                _timeoutMs = timeoutMs;
            }

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

        private static bool IsToday(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return false;
            if (DateTime.TryParse(dateStr, out var dt))
                return dt.Date == DateTime.UtcNow.Date;
            return false;
        }

        private static bool IsThisMonth(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return false;
            if (DateTime.TryParse(dateStr, out var dt))
                return dt.Year == DateTime.UtcNow.Year && dt.Month == DateTime.UtcNow.Month;
            return false;
        }

        public static void TestConnection()
        {
            var result = CheckConfiguredAccess(ItchDeploySettings.Load());
            EditorUtility.DisplayDialog("itch.io access check", result.message, "OK");
        }
    }
}
