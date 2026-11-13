using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace ItchDeploy.Editor
{
    [InitializeOnLoad]
    public static class AutoVersion
    {
        private const double GitPreviewCacheSeconds = 15d;
        private static string _cachedGitArguments;
        private static string _cachedGitResult;
        private static double _cachedGitAt = -1d;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool("ItchDeploy_AutoVersion", false);
            set => EditorPrefs.SetBool("ItchDeploy_AutoVersion", value);
        }

        public static VersionScheme Scheme
        {
            get => (VersionScheme)EditorPrefs.GetInt("ItchDeploy_VersionScheme", 0);
            set => EditorPrefs.SetInt("ItchDeploy_VersionScheme", (int)value);
        }

        public enum VersionScheme
        {
            GitCommitCount,
            GitDescribe,
            DateBased,
            Manual
        }

        public static string GetVersion()
        {
            switch (Scheme)
            {
                case VersionScheme.GitCommitCount:
                    return GetGitCommitCount();
                case VersionScheme.GitDescribe:
                    return GetGitDescribe();
                case VersionScheme.DateBased:
                    return GetDateVersion();
                default:
                    return Application.version;
            }
        }

        public static string GetGitCommitCount()
        {
            var count = RunGit("rev-list --count HEAD");
            if (!int.TryParse(count, out var commitCount) || commitCount < 0)
                return Application.version;

            var parts = PlayerSettings.bundleVersion.Split('.');
            var baseVersion = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : "1.0";
            return $"{baseVersion}.{commitCount}";
        }

        public static string GetGitDescribe()
        {
            var description = RunGit("describe --tags --always --dirty");
            return string.IsNullOrWhiteSpace(description)
                ? Application.version
                : description;
        }

        public static string GetDateVersion()
        {
            var now = DateTime.Now;
            return $"{now.Year}.{now.Month:D2}.{now.Day:D2}.{now.Hour:D2}{now.Minute:D2}";
        }

        public static void ApplyToPlayerSettings()
        {
            ApplyGeneratedVersion();
        }

        public static string ApplyGeneratedVersion()
        {
            var version = GetVersion();
            PlayerSettings.bundleVersion = version;
            EditorUtility.SetDirty(ItchDeploySettings.Load());
            AssetDatabase.SaveAssets();
            Debug.Log($"[AutoVersion] Applied: {version}");
            return version;
        }

        private static string RunGit(string arguments)
        {
            var now = EditorApplication.timeSinceStartup;
            if (string.Equals(_cachedGitArguments, arguments, StringComparison.Ordinal) &&
                now - _cachedGitAt < GitPreviewCacheSeconds)
                return _cachedGitResult;

            try
            {
                var projectRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
                var psi = new ProcessStartInfo("git", arguments)
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = projectRoot
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return null;

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(5000))
                {
                    process.Kill();
                    _cachedGitArguments = arguments;
                    _cachedGitResult = null;
                    _cachedGitAt = now;
                    Debug.LogWarning(
                        $"[AutoVersion] git {arguments} timed out.");
                    return null;
                }

                var output = outputTask.GetAwaiter().GetResult().Trim();
                if (process.ExitCode == 0)
                {
                    _cachedGitArguments = arguments;
                    _cachedGitResult = output;
                    _cachedGitAt = now;
                    return output;
                }

                var error = errorTask.GetAwaiter().GetResult().Trim();
                _cachedGitArguments = arguments;
                _cachedGitResult = null;
                _cachedGitAt = now;
                Debug.LogWarning(
                    $"[AutoVersion] git {arguments} failed: {error}");
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[AutoVersion] git {arguments} failed: {exception.Message}");
                return null;
            }
        }

        public static VisualElement CreateUI()
        {
            var root = new VisualElement();
            root.AddToClassList("settings-auto-version-card");

            var title = new Label(ItchDeployLocale._(
                "Automatic versioning", "Автоматичне версіонування"));
            title.AddToClassList("settings-card-title");
            root.Add(title);

            var help = new Label(ItchDeployLocale._(
                "Generate build versions from Git or the current date and apply them to PlayerSettings before building.",
                "Формує версію збірки з Git або поточної дати та застосовує її до PlayerSettings перед збіркою."));
            help.AddToClassList("settings-card-help");
            help.style.whiteSpace = WhiteSpace.Normal;
            root.Add(help);

            var enableToggleRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._(
                    "Enable automatic versioning",
                    "Увімкнути автоматичне версіонування"),
                Enabled,
                out var enableToggle,
                "auto-version-toggle");
            enableToggle.tooltip = ItchDeployLocale._(
                "Update PlayerSettings.bundleVersion automatically before each build.",
                "Автоматично оновлювати PlayerSettings.bundleVersion перед кожною збіркою.");
            enableToggleRow.tooltip = enableToggle.tooltip;
            enableToggle.RegisterValueChangedCallback(ev => Enabled = ev.newValue);
            root.Add(enableToggleRow);

            var schemeRow = new VisualElement();
            schemeRow.AddToClassList("settings-card-details");
            schemeRow.AddToClassList("settings-auto-version-details");
            root.Add(schemeRow);

            TextField previewField = null;
            var schemeChoices = new System.Collections.Generic.List<string>
            {
                ItchDeployLocale._("Git commit count", "Кількість Git-комітів"),
                "Git describe",
                ItchDeployLocale._("Current date", "Поточна дата"),
                ItchDeployLocale._("Manual", "Вручну")
            };
            var schemeField = new ItchDeployChoiceField(
                ItchDeployLocale._("Scheme", "Схема"),
                schemeChoices,
                Mathf.Clamp((int)Scheme, 0, schemeChoices.Count - 1));
            schemeField.tooltip = ItchDeployLocale._(
                "Choose where the next build version comes from.",
                "Виберіть джерело версії наступної збірки.");
            schemeField.style.flexGrow = 1;
            schemeField.AddToClassList("settings-auto-version-scheme");
            schemeField.SelectionChanged += (_, _) =>
            {
                Scheme = (VersionScheme)schemeField.index;
                if (Enabled && previewField != null)
                    previewField.SetValueWithoutNotify(GetVersion());
            };
            schemeRow.Add(schemeField);

            var previewLabel = new Label(ItchDeployLocale._(
                "Preview", "Попередній перегляд"));
            previewLabel.AddToClassList("settings-inline-heading");
            previewLabel.AddToClassList("settings-auto-version-preview-title");
            schemeRow.Add(previewLabel);

            previewField = new TextField(ItchDeployLocale._(
                "Next version", "Наступна версія"));
            previewField.SetValueWithoutNotify(
                Enabled ? GetVersion() : Application.version);
            previewField.isReadOnly = true;
            previewField.tooltip = ItchDeployLocale._(
                "The version that will be written to PlayerSettings.",
                "Версія, яку буде записано у PlayerSettings.");
            previewField.AddToClassList("settings-auto-version-preview-field");
            schemeRow.Add(previewField);

            var applyBtn = new Button(ApplyToPlayerSettings);
            applyBtn.text = ItchDeployLocale._(
                "Apply now", "Застосувати зараз");
            applyBtn.AddToClassList("settings-wide-action");
            applyBtn.AddToClassList("settings-auto-version-apply");
            schemeRow.Add(applyBtn);

            void Refresh(bool regeneratePreview)
            {
                var isEnabled = Enabled;
                schemeRow.style.display = isEnabled ? DisplayStyle.Flex : DisplayStyle.None;
                if (isEnabled && regeneratePreview)
                    previewField.SetValueWithoutNotify(GetVersion());
            }

            enableToggle.RegisterValueChangedCallback(ev => Refresh(true));
            Refresh(false);

            return root;
        }
    }
}
