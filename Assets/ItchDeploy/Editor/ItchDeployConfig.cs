using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("ItchDeploy.Editor.Tests")]

namespace ItchDeploy.Editor
{
    [InitializeOnLoad]
    public static class ItchDeployConfig
    {
        public static string FilePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".itchdeploy.json"));

        public static bool Exists => File.Exists(FilePath);

        private static ConfigData _cached;
        private static long _cachedWriteTimeUtcTicks;

        internal static void InvalidateCache()
        {
            _cached = null;
            _cachedWriteTimeUtcTicks = 0;
        }

        static ItchDeployConfig()
        {
            EditorApplication.delayCall += () =>
            {
                try { SyncToSettingsIfNewer(); }
                catch (Exception ex) { Debug.LogWarning($"[ItchDeploy] Config sync skipped: {ex.Message}"); }
            };
        }

        public static ConfigData Load()
        {
            if (!File.Exists(FilePath))
            {
                _cached = ConfigData.Default();
                _cachedWriteTimeUtcTicks = 0;
                return _cached;
            }

            var writeTicks = GetWriteTicks(FilePath);
            if (_cached != null && writeTicks == _cachedWriteTimeUtcTicks)
                return _cached;

            try
            {
                var json = File.ReadAllText(FilePath);
                _cached = Parse(json) ?? ConfigData.Default();
                _cachedWriteTimeUtcTicks = writeTicks;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItchDeploy] Could not read {FilePath}: {ex.Message}");
                _cached = ConfigData.Default();
                _cachedWriteTimeUtcTicks = 0;
            }
            return _cached;
        }

        private static long GetWriteTicks(string path)
        {
            try { return new FileInfo(path).LastWriteTimeUtc.Ticks; }
            catch { return 0; }
        }

        public static void Save(ConfigData data)
        {
            var json = ToJson(data);
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(FilePath, json);
            _cached = data;
            _cachedWriteTimeUtcTicks = GetWriteTicks(FilePath);
        }

        public static void WriteFromSettings(ItchDeploySettings settings)
        {
            if (settings == null) return;

            var data = Load(); 
            data.itchUser = settings.itchUserName ?? data.itchUser;
            data.itchGame = settings.itchGameSlug ?? data.itchGame;
            data.autoDeploy = settings.autoDeployAfterBuild;
            data.webglCompression = settings.webglCompression.ToString().ToLowerInvariant();

            data.channels.Clear();
            foreach (var mapping in settings.channelMappings)
                if (mapping.active && !string.IsNullOrEmpty(mapping.buildTarget))
                    data.channels[mapping.buildTarget] = string.IsNullOrEmpty(mapping.channel)
                        ? mapping.buildTarget
                        : mapping.channel;

            Save(data);
        }

        public static void ApplyToSettings(ItchDeploySettings settings, bool saveAsset = true)
        {
            if (settings == null) return;
            var data = Load();

            var changed = false;
            if (!string.IsNullOrEmpty(data.itchUser) && settings.itchUserName != data.itchUser)
            { settings.itchUserName = data.itchUser; changed = true; }
            if (!string.IsNullOrEmpty(data.itchGame) && settings.itchGameSlug != data.itchGame)
            { settings.itchGameSlug = data.itchGame; changed = true; }
            if (settings.autoDeployAfterBuild != data.autoDeploy)
            { settings.autoDeployAfterBuild = data.autoDeploy; changed = true; }

            var compression = ParseCompression(data.webglCompression);
            if (settings.webglCompression != compression)
            { settings.webglCompression = compression; changed = true; }

            if (data.channels != null && data.channels.Count > 0)
            {
                settings.channelMappings = BuildMappings(data.channels);
                changed = true;
            }

            if (changed && saveAsset)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        public static void SyncToSettingsIfNewer()
        {
            if (!Exists) return;
            var settings = ItchDeploySettings.Load();
            if (!IsConfigNewerThanSettings(settings)) return;
            ApplyToSettings(settings);
        }

        private static bool IsConfigNewerThanSettings(ItchDeploySettings settings)
        {
            try
            {
                var jsonTime = new FileInfo(FilePath).LastWriteTimeUtc;
                var assetPath = ItchDeploySettings.SettingsPath;
                if (!File.Exists(assetPath)) return true;
                var assetTime = new FileInfo(assetPath).LastWriteTimeUtc;
                return jsonTime > assetTime;
            }
            catch { return false; }
        }

        private static List<ItchDeploySettings.PlatformChannelEntry> BuildMappings(Dictionary<string, string> channels)
        {
            var list = new List<ItchDeploySettings.PlatformChannelEntry>();
            foreach (var kv in channels)
            {
                list.Add(new ItchDeploySettings.PlatformChannelEntry
                {
                    buildTarget = kv.Key,
                    channel = kv.Value,
                    channelSuffix = string.Empty,
                    active = true,
                });
            }
            return list;
        }

        private static WebGLHelper.CompressionFormat ParseCompression(string value)
        {
            if (string.IsNullOrEmpty(value)) return WebGLHelper.CompressionFormat.Brotli;
            return value.ToLowerInvariant() switch
            {
                "disabled" or "none" or "0" => WebGLHelper.CompressionFormat.Disabled,
                "gzip" or "1" => WebGLHelper.CompressionFormat.Gzip,
                "brotli" or "2" => WebGLHelper.CompressionFormat.Brotli,
                _ => WebGLHelper.CompressionFormat.Brotli,
            };
        }

        public sealed class ConfigData
        {
            public string itchUser = string.Empty;
            public string itchGame = string.Empty;
            public string unityVersion = Application.unityVersion;
            public string defaultTarget = "StandaloneWindows64";
            public string defaultChannel = "windows";
            public bool autoDeploy = true;
            public string webglCompression = "brotli";
            public string versioning = "semantic";
            public string buildName = "game";
            public readonly Dictionary<string, string> channels = new()
            {
                { "StandaloneWindows64", "windows" },
                { "StandaloneLinux64", "linux" },
                { "StandaloneOSX", "mac" },
                { "WebGL", "html5" },
                { "Android", "android" },
                { "iOS", "ios" },
            };
            public CiData ci = new CiData();
            public NotifyData notify = new NotifyData();

            public static ConfigData Default() => new ConfigData();
        }

        public sealed class NotifyData
        {
            public bool notifyOnSuccess = true;
            public bool notifyOnFailure = true;
        }

        public sealed class CiData
        {
            public string githubTrigger = "push_main";
            public bool githubDeploy = true;
            public bool unityBuildAutomationDeploy = true;
            public bool disableBurstOnRunners;
            public bool buildIos;
        }

        internal static string Serialize(ConfigData data) => ToJson(data);

        internal static ConfigData Deserialize(string json) => Parse(json);

        private static string ToJson(ConfigData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            data.ci ??= new CiData();
            data.notify ??= new NotifyData();

            var sb = new StringBuilder();
            sb.AppendLine("{");
            AppendProperty(sb, "itch_user", data.itchUser, 1);
            AppendProperty(sb, "itch_game", data.itchGame, 1);
            AppendProperty(sb, "unity_version", data.unityVersion, 1);
            AppendProperty(sb, "build_name", data.buildName, 1);
            AppendProperty(sb, "default_target", data.defaultTarget, 1);
            AppendProperty(sb, "default_channel", data.defaultChannel, 1);
            AppendProperty(sb, "auto_deploy", data.autoDeploy, 1);
            AppendProperty(sb, "webgl_compression", data.webglCompression, 1);
            AppendProperty(sb, "versioning", data.versioning, 1);

            sb.Append("  \"channels\": {");
            if (data.channels == null || data.channels.Count == 0)
            {
                sb.AppendLine("},");
            }
            else
            {
                sb.AppendLine();
                var i = 0;
                foreach (var kv in data.channels)
                {
                    sb.Append("    ");
                    AppendQuoted(sb, kv.Key);
                    sb.Append(": ");
                    AppendQuoted(sb, kv.Value);
                    sb.AppendLine(++i < data.channels.Count ? "," : "");
                }
                sb.AppendLine("  },");
            }

            sb.AppendLine("  \"ci\": {");
            AppendProperty(sb, "github_trigger", data.ci.githubTrigger, 2);
            AppendProperty(sb, "github_deploy", data.ci.githubDeploy, 2);
            AppendProperty(
                sb,
                "unity_build_automation_deploy",
                data.ci.unityBuildAutomationDeploy,
                2);
            AppendProperty(
                sb,
                "disable_burst_on_runners",
                data.ci.disableBurstOnRunners,
                2);
            AppendProperty(sb, "build_ios", data.ci.buildIos, 2, trailingComma: false);
            sb.AppendLine("  },");

            sb.AppendLine("  \"notify\": {");
            AppendProperty(sb, "notify_on_success", data.notify.notifyOnSuccess, 2);
            AppendProperty(sb, "notify_on_failure", data.notify.notifyOnFailure, 2, trailingComma: false);
            sb.AppendLine("  }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void AppendProperty(
            StringBuilder sb,
            string key,
            string value,
            int indent,
            bool trailingComma = true)
        {
            sb.Append(' ', indent * 2);
            AppendQuoted(sb, key);
            sb.Append(": ");
            AppendQuoted(sb, value ?? string.Empty);
            sb.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendProperty(
            StringBuilder sb,
            string key,
            bool value,
            int indent,
            bool trailingComma = true)
        {
            sb.Append(' ', indent * 2);
            AppendQuoted(sb, key);
            sb.Append(": ");
            sb.Append(value ? "true" : "false");
            sb.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendQuoted(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private static ConfigData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var data = ConfigData.Default();
            data.channels.Clear();

            var root = MiniJson.Parse(json) as MiniJson.Object;
            if (root == null) return data;

            data.itchUser = root.GetString("itch_user", data.itchUser);
            data.itchGame = root.GetString("itch_game", data.itchGame);
            data.unityVersion = root.GetString("unity_version", data.unityVersion);
            data.buildName = root.GetString("build_name", data.itchGame);
            data.defaultTarget = root.GetString("default_target", data.defaultTarget);
            data.defaultChannel = root.GetString("default_channel", data.defaultChannel);
            data.autoDeploy = root.GetBool("auto_deploy", data.autoDeploy);
            data.webglCompression = root.GetString("webgl_compression", data.webglCompression);
            data.versioning = root.GetString("versioning", data.versioning);

            var channels = root.GetObject("channels");
            if (channels != null)
            {
                foreach (var key in channels.Keys)
                    data.channels[key] = channels.GetString(key, key);
            }

            var ci = root.GetObject("ci");
            if (ci != null)
            {
                data.ci.githubTrigger = ci.GetString(
                    "github_trigger",
                    data.ci.githubTrigger);
                data.ci.githubDeploy = ci.GetBool(
                    "github_deploy",
                    data.ci.githubDeploy);
                data.ci.unityBuildAutomationDeploy = ci.GetBool(
                    "unity_build_automation_deploy",
                    data.ci.unityBuildAutomationDeploy);
                data.ci.disableBurstOnRunners = ci.GetBool(
                    "disable_burst_on_runners",
                    data.ci.disableBurstOnRunners);
                data.ci.buildIos = ci.GetBool("build_ios", data.ci.buildIos);
            }

            var notify = root.GetObject("notify");
            if (notify != null)
            {
                data.notify.notifyOnSuccess = notify.GetBool("notify_on_success", true);
                data.notify.notifyOnFailure = notify.GetBool("notify_on_failure", true);
            }

            return data;
        }
    }
}
