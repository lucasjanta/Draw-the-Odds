using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class ItchTomlEditor
    {
        private static string _sourcePath;
        private static string _rawContent = "";
        private static Vector2 _scrollPos;

        public static string SourcePath
        {
            get
            {
                if (string.IsNullOrEmpty(_sourcePath))
                    _sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ItchDeployManifest.toml"));
                return _sourcePath;
            }
        }

        public static bool Exists => File.Exists(SourcePath);

        public static void Load()
        {
            _rawContent = Exists ? File.ReadAllText(SourcePath) : GenerateDefault();
        }

        public static void Save()
        {
            File.WriteAllText(SourcePath, _rawContent);
            AssetDatabase.Refresh();
        }

        public static string GenerateDefault()
        {
            return "# Optional launch manifest for the itch.io desktop app.\n" +
                   "# It does NOT configure the itch.io page description, categories, cover, or screenshots.\n" +
                   "# Most Unity builds work without this file. Add it only when you need a custom launch action.\n" +
                   "# The manifest is copied into the root of each published build.\n" +
                   "\n" +
                   "# [[actions]]\n" +
                   "# name = \"play\"\n" +
                   "# path = \"YourGame.exe\"\n" +
                   "\n" +
                   "# [[prereqs]]\n" +
                   "# name = \"vcredist-2015-x64\"\n";
        }

        public static bool CopyToBuild(string buildDirectory)
        {
            if (!Exists || string.IsNullOrWhiteSpace(buildDirectory) || !Directory.Exists(buildDirectory))
                return false;

            File.Copy(SourcePath, Path.Combine(buildDirectory, ".itch.toml"), true);
            return true;
        }
    }
}
