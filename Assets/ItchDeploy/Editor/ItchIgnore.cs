using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ItchDeploy.Editor
{
    public static class ItchIgnore
    {
        public static string IgnorePath =>
            Path.Combine(Application.dataPath, "..", ".itchignore");

        public static bool Exists => File.Exists(IgnorePath);

        public static bool Enabled
        {
            get => EditorPrefs.GetBool("ItchDeploy_ItchIgnore", true);
            set => EditorPrefs.SetBool("ItchDeploy_ItchIgnore", value);
        }

        // Unity names these folders after what should happen to them. The generated
        // workflow deletes them before publishing; a local deploy has to exclude them
        // too, or a project without an .itchignore uploads its debug symbols and, for
        // an IL2CPP build, several times the size of the game itself.
        internal static readonly string[] UnityDoNotShipFolders =
        {
            "*_BackUpThisFolder_ButDontShipItWithYourGame",
            "*_BurstDebugInformation_DoNotShip",
            "*_IL2CppBackUpThisFolder_ButDontShipItWithYourGame",
        };

        public static string GetButlerIgnoreArguments()
        {
            var arguments = new List<string>();
            foreach (var folder in UnityDoNotShipFolders)
                arguments.Add("--ignore " + ButlerBridge.QuoteArgument(folder));

            if (!Enabled || !File.Exists(IgnorePath))
                return string.Join(" ", arguments);

            var unsupported = new List<string>();
            foreach (var line in File.ReadAllLines(IgnorePath))
            {
                var pattern = line.Trim();
                if (string.IsNullOrEmpty(pattern) || pattern.StartsWith("#"))
                    continue;

                var butlerPattern = ToButlerPattern(pattern);
                if (butlerPattern == null)
                {
                    unsupported.Add(pattern);
                    continue;
                }

                var argument = "--ignore " + ButlerBridge.QuoteArgument(butlerPattern);
                if (!arguments.Contains(argument))
                    arguments.Add(argument);
            }

            if (unsupported.Count > 0)
            {
                Debug.LogWarning(
                    "[ItchDeploy] These .itchignore patterns cannot be passed to butler and were skipped: " +
                    string.Join(", ", unsupported) +
                    ". butler matches a single path component, so a pattern containing '/' never matches. " +
                    "Use just the file or folder name instead.");
            }

            return string.Join(" ", arguments);
        }

        internal static string ToButlerPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return null;

            var normalized = pattern.Trim().Replace('\\', '/').TrimEnd('/');
            if (normalized.Length == 0 || normalized.Contains("/"))
                return null;

            return normalized;
        }

        public static VisualElement CreateUI()
        {
            var root = new VisualElement();
            root.AddToClassList("settings-itchignore-card");

            var title = new Label(".itchignore");
            title.AddToClassList("settings-card-title");
            root.Add(title);

            var help = new Label(ItchDeployLocale._(
                "File patterns excluded from publishing. The rules are passed to butler on every upload.",
                "Шаблони файлів, які не потрапляють у публікацію. Правила передаються butler під час кожного завантаження."));
            help.AddToClassList("settings-card-help");
            help.style.whiteSpace = WhiteSpace.Normal;
            root.Add(help);

            var toggleRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._(
                    "Use .itchignore",
                    "Використовувати .itchignore"),
                Enabled,
                out var toggle,
                "itchignore-toggle");
            toggle.RegisterValueChangedCallback(ev => Enabled = ev.newValue);
            root.Add(toggleRow);

            if (Exists)
            {
                var pathLabel = new Label(ItchDeployLocale._(
                    "File", "Файл") + ": " + IgnorePath);
                pathLabel.AddToClassList("settings-muted");
                pathLabel.AddToClassList("settings-itchignore-path");
                root.Add(pathLabel);

                var openBtn = new Button(() => EditorUtility.OpenWithDefaultApp(IgnorePath));
                openBtn.text = ItchDeployLocale._(
                    "Open in text editor", "Відкрити в текстовому редакторі");
                openBtn.AddToClassList("secondary-action");
                openBtn.AddToClassList("settings-wide-secondary");
                openBtn.AddToClassList("settings-itchignore-file-action");
                root.Add(openBtn);
            }
            else
            {
                var createBtn = new Button(CreateDefault);
                createBtn.text = ItchDeployLocale._(
                    "Create default .itchignore",
                    "Створити стандартний .itchignore");
                createBtn.AddToClassList("settings-wide-action");
                createBtn.AddToClassList("settings-itchignore-file-action");
                root.Add(createBtn);
            }

            var patternsLabel = new Label(ItchDeployLocale._(
                "Common patterns", "Типові шаблони"));
            patternsLabel.AddToClassList("settings-inline-heading");
            patternsLabel.AddToClassList("settings-itchignore-patterns-title");
            root.Add(patternsLabel);

            var textArea = new TextField();
            textArea.value = ItchDeployLocale._(
                    "# .itchignore - files excluded from publishing",
                    "# .itchignore — файли, виключені з публікації") + "\n" +
                "*.pdb\n*.mdb\n*.log\n*.tmp\n" +
                "*_BackUpThisFolder_ButDontShipItWithYourGame/\n" +
                "*_BurstDebugInformation_DoNotShip/\n" +
                "__pycache__/\n.git/\nThumbs.db\n*.blend1\n*.bak\n";
            textArea.isReadOnly = true;
            textArea.multiline = true;
            textArea.style.minHeight = 120;
            textArea.style.fontSize = 9;
            textArea.style.whiteSpace = WhiteSpace.Normal;
            textArea.AddToClassList("settings-code-area");
            root.Add(textArea);

            return root;
        }

        public static void CreateDefault()
        {
            var content =
                "# .itchignore - files excluded from itch.io deployment\n" +
                "# Lines starting with # are comments\n" +
                "\n" +
                "*.pdb\n" +
                "*.mdb\n" +
                "*.log\n" +
                "*.tmp\n" +
                "*_BackUpThisFolder_ButDontShipItWithYourGame/\n" +
                "*_BurstDebugInformation_DoNotShip/\n" +
                "__pycache__/\n" +
                ".git/\n" +
                ".gitignore\n" +
                ".itchignore\n" +
                "Thumbs.db\n" +
                "*.blend1\n" +
                "*.bak\n" +
                "*.orig\n";

            File.WriteAllText(IgnorePath, content);
            AssetDatabase.Refresh();
            Debug.Log("[ItchDeploy] .itchignore created");
        }
    }
}
