using System;
using UnityEditor;

namespace ItchDeploy.Editor
{
    public static class ItchDeployCredentials
    {
        private const string ProfileApiKeyEnvironmentVariable = "ITCHIO_API_KEY";
        private const double CacheSeconds = 30d;

        private static string _cachedDeployKey;
        private static string _cachedProfileKey;
        private static double _deployKeyCachedAt = double.NegativeInfinity;
        private static double _profileKeyCachedAt = double.NegativeInfinity;

        public static void Invalidate()
        {
            _deployKeyCachedAt = double.NegativeInfinity;
            _profileKeyCachedAt = double.NegativeInfinity;
            _cachedDeployKey = null;
            _cachedProfileKey = null;
        }

        private static bool IsFresh(double cachedAt) =>
            EditorApplication.timeSinceStartup - cachedAt < CacheSeconds;

        public static string ResolveApiKey()
        {
            if (IsFresh(_deployKeyCachedAt))
                return _cachedDeployKey;

            _cachedDeployKey = ReadDeployKey();
            _deployKeyCachedAt = EditorApplication.timeSinceStartup;
            return _cachedDeployKey;
        }

        private static string ReadDeployKey()
        {
            var environmentKey = Environment.GetEnvironmentVariable("BUTLER_API_KEY");
            if (!string.IsNullOrWhiteSpace(environmentKey))
                return environmentKey.Trim();

            var managedButlerKey = ItchDeployOAuth.LoadManagedCredential();
            if (!string.IsNullOrWhiteSpace(managedButlerKey))
                return managedButlerKey;

            var defaultButlerKey = ItchDeployOAuth.LoadDefaultButlerCredential();
            if (!string.IsNullOrWhiteSpace(defaultButlerKey))
                return defaultButlerKey;

            return ItchDeploySecureStorage.LoadApiKey();
        }

        public static string ResolveProfileApiKey()
        {
            if (IsFresh(_profileKeyCachedAt))
                return _cachedProfileKey;

            _cachedProfileKey = ResolveProfileApiKeyFromCandidates(
                Environment.GetEnvironmentVariable(ProfileApiKeyEnvironmentVariable),
                ItchDeploySecureStorage.LoadApiKey(),
                Environment.GetEnvironmentVariable("BUTLER_API_KEY"),
                ItchDeployOAuth.LoadManagedCredential(),
                ItchDeployOAuth.LoadDefaultButlerCredential());
            _profileKeyCachedAt = EditorApplication.timeSinceStartup;
            return _cachedProfileKey;
        }

        internal static string ResolveProfileApiKeyFromCandidates(
            string profileEnvironmentKey,
            string secureApiKey,
            string butlerEnvironmentKey,
            string managedButlerKey,
            string defaultButlerKey)
        {
            var candidates = new[]
            {
                profileEnvironmentKey,
                secureApiKey,
                butlerEnvironmentKey,
                managedButlerKey,
                defaultButlerKey
            };

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                    return candidate.Trim();
            }

            return null;
        }

        public static bool HasApiKey => !string.IsNullOrWhiteSpace(ResolveApiKey());

        public static bool HasProfileApiKey =>
            !string.IsNullOrWhiteSpace(ResolveProfileApiKey());
    }
}
