using UnityEditor;
using UnityEngine;

namespace GamePix.Editor
{
    public class BuildWindow
    {
        private static readonly string buildDirectory = "html5";
        private static readonly string latestVersionMessage = "The latest GamePix Unity3D plugin version is used";

        [MenuItem("GamePix/Build and Run", false, -1)]
        static void Build()
        {
            Build(true, AssetsCompression.Uncompressed);
        }

        [MenuItem("GamePix/Safe builds/Build and Run (No override textures\\sounds)", false, 2)]
        static void DefaultResourceBuild()
        {
            Build(true, AssetsCompression.NoOverride);
        }
        
        [MenuItem("GamePix/Safe builds/Build and Run (Compress textures\\sounds)", false, 3)]
        static void CompressedResourceBuild()
        {
            Build(true, AssetsCompression.Compressed);
        }

        private static void Build(bool safeMode, AssetsCompression assetsCompression)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL)) {
                const string heading = "Can't build the game";
                const string message = "Unity WebGL Platform is not installed.";
                Debug.LogError(heading + ". " + message);
                MessageWindow.Open("Build", MessageStatus.Error, heading, message);
                return;
            }

            string emscriptenEditorVersion = Emscripten.editorVersion;
            string emscriptenPluginVersion = Emscripten.pluginVersion;
            
            if (emscriptenEditorVersion != emscriptenPluginVersion)
            {
                string message = "This plugin version doesn't support Unity " + ApplicationInfo.unityVersion + ".";
                string detail = "Emscripten: plugin " + emscriptenPluginVersion + ", editor " + emscriptenEditorVersion;
                Debug.LogError(message + " " + detail + ". Check GamePix plugin for Unity: " + Updater.defaultUrl);
                MessageWindow.Open("Build", MessageStatus.Error, "Can't build the game", message, detail,
                                   "Download plugin", () => Application.OpenURL(Updater.defaultUrl), "Close");
                return;
            }
            Updater.TryUpdatePlugin(() =>
            {
                UnityConfigurator.SetWebGLSettings(safeMode, assetsCompression);
                Builder.BuildArchive(buildDirectory, assetsCompression, DataFileCompression.Uncompressed, true);
            });
        }

        [MenuItem("GamePix/Check update", false, 2)]
        static void CheckUpdate()
        {
            Updater.TryUpdatePlugin(() =>
            {
                Debug.Log(latestVersionMessage);
                MessageWindow.Open("Plugin update", MessageStatus.Success, "You’re up to date",
                                   "The latest version is installed.", "Version " + ApplicationInfo.pluginVersion);
            });
        }
    }
}