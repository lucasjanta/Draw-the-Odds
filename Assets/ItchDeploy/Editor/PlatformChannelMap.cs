using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class PlatformChannelMap
    {
        public static string GetDefaultChannel(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows64 => "windows",
                BuildTarget.StandaloneWindows => "windows32",
                BuildTarget.StandaloneLinux64 => "linux",
                BuildTarget.StandaloneOSX => "mac",
                BuildTarget.WebGL => "html5",
                BuildTarget.Android => "android",
                BuildTarget.iOS => "ios",
                BuildTarget.WSAPlayer => "uwp",
                _ => target.ToString().ToLowerInvariant(),
            };
        }

        public static string GetChannelTarget(string user, string game, string channel)
        {
            return $"{user}/{game}:{channel}";
        }

        public static string ResolveChannel(ItchDeploySettings settings, BuildTarget target)
        {
            var channel = GetDefaultChannel(target);
            var mapping = settings?.channelMappings?.Find(m => m.buildTarget == target.ToString());
            if (mapping != null && !string.IsNullOrEmpty(mapping.channel))
                channel = mapping.channel + (mapping.channelSuffix ?? string.Empty);
            return channel;
        }

        public static string GetDisplayName(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows64 => "Windows 64-bit",
                BuildTarget.StandaloneWindows => "Windows 32-bit",
                BuildTarget.StandaloneLinux64 => "Linux 64-bit",
                BuildTarget.StandaloneOSX => "macOS",
                BuildTarget.WebGL => "WebGL (HTML5)",
                BuildTarget.Android => "Android",
                BuildTarget.iOS => "iOS",
                BuildTarget.WSAPlayer => "UWP",
                _ => target.ToString(),
            };
        }
    }
}
