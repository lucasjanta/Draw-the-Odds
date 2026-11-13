using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ItchDeploy.Editor
{
    public class ItchDeploySettings : ScriptableObject
    {
        public const string SettingsPath = "Assets/ItchDeploy/Resources/ItchDeploySettings.asset";

        public string itchUserName;
        public string itchGameSlug;
        [HideInInspector]
        public string itchOAuthClientId;
        public bool autoDeployAfterBuild = true;
        public WebGLHelper.CompressionFormat webglCompression = WebGLHelper.CompressionFormat.Brotli;

        public List<PlatformChannelEntry> channelMappings = new()
        {
            new() { buildTarget = "StandaloneWindows64", channel = "windows", active = true },
            new() { buildTarget = "StandaloneLinux64",   channel = "linux",   active = true },
            new() { buildTarget = "StandaloneOSX",       channel = "mac",     active = true },
            new() { buildTarget = "WebGL",               channel = "html5",   active = true },
            new() { buildTarget = "Android",             channel = "android", active = true },
            new() { buildTarget = "iOS",                 channel = "ios",     active = true },
        };

        public string EffectiveApiKey
        {
            get => ItchDeployCredentials.ResolveApiKey();
        }

        public bool HasApiKey => !string.IsNullOrEmpty(EffectiveApiKey);

        public static ItchDeploySettings Load()
        {
            var settings = Resources.Load<ItchDeploySettings>("ItchDeploySettings");
            if (settings == null)
            {
                settings = CreateInstance<ItchDeploySettings>();
                var dir = System.IO.Path.GetDirectoryName(SettingsPath);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(settings, SettingsPath);
                AssetDatabase.SaveAssets();
            }

            if (!settings._syncedFromConfig)
            {
                try { ItchDeployConfig.ApplyToSettings(settings, saveAsset: false); }
                catch {  }
                settings._syncedFromConfig = true;
                EditorUtility.SetDirty(settings);
            }
            return settings;
        }

        [SerializeField] private bool _syncedFromConfig;

        [System.Serializable]
        public class PlatformChannelEntry
        {
            public string buildTarget;
            public string channel;
            public string channelSuffix;
            public bool active = true;
        }
    }
}
