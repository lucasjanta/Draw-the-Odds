using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ItchDeploy.Editor
{
    public static class BuildNamer
    {
        private const string DefaultTemplate = "{project}_{platform}_{version}";

        public static string Template
        {
            get
            {
                var value = EditorPrefs.GetString(
                    "ItchDeploy_BuildNamer",
                    DefaultTemplate);
                return string.IsNullOrWhiteSpace(value) ? DefaultTemplate : value;
            }
            set => EditorPrefs.SetString("ItchDeploy_BuildNamer", value);
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool("ItchDeploy_BuildNamer_Enabled", false);
            set => EditorPrefs.SetBool("ItchDeploy_BuildNamer_Enabled", value);
        }

        public static string Resolve(string platform, string version)
        {
            if (!Enabled) return platform;

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return Template
                .Replace("{project}", Application.productName)
                .Replace("{platform}", platform)
                .Replace("{version}", version)
                .Replace("{timestamp}", timestamp)
                .Replace("{date}", DateTime.Now.ToString("yyyyMMdd"))
                .Replace("{time}", DateTime.Now.ToString("HHmmss"))
                .Replace("{year}", DateTime.Now.Year.ToString());
        }

        public static string GetBuildFolder(string platform, string version)
        {
            var buildsDir = Path.Combine(Application.dataPath, "..", "Builds");
            var folder = Enabled ? Resolve(platform, version) : version;
            return Path.Combine(buildsDir, platform, folder);
        }

        public static string GetPreviewPath(string platform, string version)
        {
            var folder = Enabled ? Resolve(platform, version) : version;
            return Path.Combine("Builds", platform, folder).Replace('\\', '/');
        }

        public static void Preview()
        {
            var active = EditorUserBuildSettings.activeBuildTarget;
            var folder = GetBuildFolder(active.ToString(), Application.version);
            Debug.Log($"[BuildNamer] Preview: {folder}");
            EditorUtility.DisplayDialog("BuildNamer Preview",
                $"Template: {Template}\n\n" +
                $"Resolved: {Resolve(active.ToString(), Application.version)}\n" +
                $"Full path: {folder}", "OK");
        }

        public static VisualElement CreateUI()
        {
            var root = new VisualElement();
            root.AddToClassList("settings-build-namer-card");

            var title = new Label(ItchDeployLocale._(
                "Build folder name", "Назва папки збірки"));
            title.AddToClassList("settings-card-title");
            root.Add(title);

            var help = new Label(ItchDeployLocale._(
                "Naming template for build folders. Variables: {project}, {platform}, {version}, {timestamp}, {date}, {time}, {year}.",
                "Шаблон назви папок збірки. Змінні: {project}, {platform}, {version}, {timestamp}, {date}, {time}, {year}."));
            help.AddToClassList("settings-card-help");
            help.style.whiteSpace = WhiteSpace.Normal;
            root.Add(help);

            var toggleRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._(
                    "Use custom naming",
                    "Використовувати власний шаблон"),
                Enabled,
                out var toggle,
                "build-namer-custom-toggle");
            root.Add(toggleRow);

            var details = new VisualElement();
            details.name = "build-namer-template-details";
            details.AddToClassList("settings-card-details");
            root.Add(details);

            var templateField = new TextField(ItchDeployLocale._(
                "Template", "Шаблон"));
            templateField.name = "build-namer-template";
            templateField.SetValueWithoutNotify(Template);
            details.Add(templateField);

            var preview = new VisualElement();
            preview.AddToClassList("settings-build-preview");
            root.Add(preview);

            var previewCaption = new Label(ItchDeployLocale._(
                "Effective build folder",
                "Папка збірки"));
            previewCaption.AddToClassList("settings-build-preview-caption");
            preview.Add(previewCaption);

            var previewValue = new Label
            {
                name = "build-namer-preview-value",
            };
            previewValue.AddToClassList("settings-build-preview-value");
            preview.Add(previewValue);

            void UpdatePreview()
            {
                templateField.SetValueWithoutNotify(Template);
                details.style.display = Enabled ? DisplayStyle.Flex : DisplayStyle.None;
                var active = EditorUserBuildSettings.activeBuildTarget;
                previewValue.text = GetPreviewPath(
                    active.ToString(),
                    Application.version);
            }

            toggle.RegisterValueChangedCallback(ev =>
            {
                Enabled = ev.newValue;
                UpdatePreview();
            });
            templateField.RegisterValueChangedCallback(ev =>
            {
                Template = ev.newValue;
                UpdatePreview();
            });
            UpdatePreview();

            return root;
        }
    }
}
