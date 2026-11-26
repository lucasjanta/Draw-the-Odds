using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace GamePix.Editor
{
    public static class Updater
    {
        private static readonly string gpxBackend = "https://rt.gamepix.com/unity/plugin/get?unity={0}&plugin={1}";
        public static readonly string defaultUrl = "https://partners.gamepix.com/sdk/doc/unity-plugin";

        private static readonly string title = "Plugin update";
        private static readonly string errorMessage = "GamePix update service is unavailable.\nMore info in console";
        private static readonly string newVersionMessage = "A new version of GamePix Unity3D plugin is available\n" +
                                                           "<b>Please remove old version of GamePix Unity3D plugin before update</b>";

        private static readonly string newVersionButton = "Download";
        private static readonly string newVersionButtonCancel = "Cancel";

        public static void TryUpdatePlugin(Action onUseLatest)
        {
            string url = string.Format(gpxBackend, ApplicationInfo.unityVersion, ApplicationInfo.pluginVersion);
            UnityWebRequest request = UnityWebRequest.Get(url);
            UnityWebRequestAsyncOperation requestOperation = request.SendWebRequest();

            requestOperation.completed += operation =>
            {
#if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isHttpError || request.isNetworkError || !string.IsNullOrEmpty(request.error))
#endif
                {
                    Debug.LogError(errorMessage + ": " + request.error);
                    MessageWindow.Open(title, MessageStatus.Error, "Update check failed", errorMessage);
                    return;
                }

                string responseText = request.downloadHandler.text;
                try
                {
                    PluginUpdateResponse response = JsonUtility.FromJson<PluginUpdateResponse>(responseText);
                    if (response != null)
                    {
                        if (response.lastVersion)
                        {
                            // SAFETY: AsyncOperation.completed runs inside the player loop, where
                            // BuildPipeline.BuildPlayer fails; delayCall runs the callback outside it.
                            EditorApplication.delayCall += () => onUseLatest();
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(response.url))
                        {
                            Debug.LogWarning(newVersionMessage + ": " + response.url);
                            MessageWindow.Open(title, MessageStatus.Info, "Update available", newVersionMessage,
                                               null, newVersionButton, () => Application.OpenURL(response.url),
                                               newVersionButtonCancel);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("Can't parse GamePix update service response: " + responseText + " Details: " + ex);
                }
            };
        }

        [Serializable]
        private class PluginUpdateResponse
        {
            public bool lastVersion;
            public string url;
        }
    }
}