using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ItchDeploy.Editor
{
    public static class GitTagManager
    {
        public static bool AutoTag
        {
            get => EditorPrefs.GetBool("ItchDeploy_AutoTag", false);
            set => EditorPrefs.SetBool("ItchDeploy_AutoTag", value);
        }

        public static bool PushTags
        {
            get => EditorPrefs.GetBool("ItchDeploy_PushTags", true);
            set => EditorPrefs.SetBool("ItchDeploy_PushTags", value);
        }

        private static string ProjectRoot =>
            System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));

        public static string TagPrefix
        {
            get => EditorPrefs.GetString("ItchDeploy_TagPrefix", "v");
            set => EditorPrefs.SetString("ItchDeploy_TagPrefix", value);
        }

        internal static string SanitizeRefName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new System.Text.StringBuilder(value.Length);
            foreach (var character in value.Trim())
            {
                if (char.IsWhiteSpace(character) ||
                    character < 0x20 ||
                    character == '~' || character == '^' || character == ':' ||
                    character == '?' || character == '*' || character == '[' ||
                    character == '\\' || character == '"' || character == '\'')
                {
                    builder.Append('-');
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString().Trim('-', '.', '/');
        }

        public static string CreateTag(string version, string channel)
        {
            var tagName = SanitizeRefName($"{TagPrefix}{version}-{channel}");
            if (string.IsNullOrEmpty(tagName))
            {
                Debug.LogWarning("[GitTag] Skipped: the version and channel produced an empty tag name.");
                return null;
            }

            var message = $"Release {version} ({channel})";

            try
            {
                var psi = new ProcessStartInfo("git", "tag -a " +
                    ButlerBridge.QuoteArgument(tagName) + " -m " +
                    ButlerBridge.QuoteArgument(message))
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = ProjectRoot
                };

                using var proc = Process.Start(psi);

                var outputTask = proc.StandardOutput.ReadToEndAsync();
                var errorTask = proc.StandardError.ReadToEndAsync();
                if (!proc.WaitForExit(5000))
                {
                    try { proc.Kill(); } catch (InvalidOperationException) { }
                    Debug.LogWarning("[GitTag] Failed: git tag timed out.");
                    return null;
                }

                var error = errorTask.GetAwaiter().GetResult();

                if (proc.ExitCode == 0)
                {
                    Debug.Log($"[GitTag] Created tag: {tagName}");

                    if (PushTags)
                    {
                        var pushPsi = new ProcessStartInfo("git",
                            "push origin " + ButlerBridge.QuoteArgument(tagName))
                        {
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            WorkingDirectory = ProjectRoot
                        };
                        using var pushProc = Process.Start(pushPsi);
                        var pushOutputTask = pushProc.StandardOutput.ReadToEndAsync();
                        var pushErrorTask = pushProc.StandardError.ReadToEndAsync();
                        if (!pushProc.WaitForExit(10000))
                        {
                            try { pushProc.Kill(); } catch (InvalidOperationException) { }
                            Debug.LogWarning($"[GitTag] Tag {tagName} was created locally but the push timed out.");
                        }
                        else if (pushProc.ExitCode == 0)
                            Debug.Log($"[GitTag] Pushed tag: {tagName}");
                        else
                            Debug.LogWarning(
                                $"[GitTag] Tag {tagName} was created locally but could not be pushed: " +
                                pushErrorTask.GetAwaiter().GetResult().Trim());
                    }

                    return tagName;
                }
                else
                {
                    Debug.LogWarning($"[GitTag] Failed: {error}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitTag] Error: {ex.Message}");
                return null;
            }
        }
    }
}
