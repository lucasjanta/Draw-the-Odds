using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ItchDeploy.Editor
{
    public static class VersionInfoWriter
    {
        private const string AssetPath = "Assets/ItchDeploy/Resources/ItchDeployVersionInfo.asset";

        public static bool Enabled
        {
            get => EditorPrefs.GetBool("ItchDeploy_VersionInfo", true);
            set => EditorPrefs.SetBool("ItchDeploy_VersionInfo", value);
        }

        public static void Write(string version, string platform, string channel)
        {
            if (!Enabled) return;

            var dir = "Assets/ItchDeploy/Resources";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var info = AssetDatabase.LoadAssetAtPath<VersionInfoData>(AssetPath);
            if (info == null)
            {
                if (File.Exists(AssetPath) && !AssetDatabase.DeleteAsset(AssetPath))
                {
                    Debug.LogError($"[VersionInfo] Could not replace invalid asset at {AssetPath}.");
                    return;
                }

                info = ScriptableObject.CreateInstance<VersionInfoData>();
                AssetDatabase.CreateAsset(info, AssetPath);
            }

            info.version = version;
            info.platform = platform;
            info.channel = channel;
            info.buildDate = System.DateTime.Now.ToString("O");
            info.productName = Application.productName;
            info.unityVersion = Application.unityVersion;

            EditorUtility.SetDirty(info);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VersionInfo] Written: {version} ({platform}/{channel})");
        }

        public static VisualElement CreateUI()
        {
            var root = new VisualElement();
            root.AddToClassList("settings-version-info-card");

            var title = new Label(ItchDeployLocale._(
                "Runtime version information", "Інформація про версію в грі"));
            title.AddToClassList("settings-card-title");
            root.Add(title);

            var help = new Label(ItchDeployLocale._(
                "Creates a ScriptableObject that the game can read to display its build version.",
                "Створює ScriptableObject, з якого гра може прочитати та показати версію збірки."));
            help.AddToClassList("settings-card-help");
            help.style.whiteSpace = WhiteSpace.Normal;
            root.Add(help);

            var toggleRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._(
                    "Write version data to Resources",
                    "Записувати дані версії в Resources"),
                Enabled,
                out var toggle,
                "version-info-toggle");
            toggle.RegisterValueChangedCallback(ev => Enabled = ev.newValue);
            root.Add(toggleRow);

            var details = new VisualElement();
            details.AddToClassList("settings-card-details");
            details.AddToClassList("settings-version-info-details");
            root.Add(details);

            var statusLabel = new Label();
            statusLabel.AddToClassList("settings-muted");
            details.Add(statusLabel);

            var selectBtn = new Button();
            selectBtn.text = ItchDeployLocale._(
                "Select in Project", "Показати у вікні Project");
            selectBtn.AddToClassList("settings-wide-action");
            selectBtn.clicked += () =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<VersionInfoData>(AssetPath);
                if (asset) Selection.activeObject = asset;
            };
            details.Add(selectBtn);

            void Refresh()
            {
                var enabled = Enabled;
                var exists = File.Exists(Path.Combine(Application.dataPath, "..", AssetPath));
                details.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
                statusLabel.text = ItchDeployLocale._("Status", "Стан") + ": " +
                    (exists
                        ? ItchDeployLocale._("asset exists", "asset створено")
                        : ItchDeployLocale._(
                            "will be created during the build",
                            "буде створено під час збірки"));
                selectBtn.style.display = (enabled && exists) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            toggle.RegisterValueChangedCallback(ev => Refresh());
            Refresh();

            return root;
        }
    }
}
