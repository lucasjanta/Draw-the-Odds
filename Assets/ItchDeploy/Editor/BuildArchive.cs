using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ItchDeploy.Editor
{
    public static class BuildArchive
    {
        public static bool AutoArchive
        {
            get => EditorPrefs.GetBool("ItchDeploy_AutoArchive", true);
            set => EditorPrefs.SetBool("ItchDeploy_AutoArchive", value);
        }

        public static string ArchiveDir
        {
            get
            {
                var d = EditorPrefs.GetString("ItchDeploy_ArchiveDir", "");
                if (string.IsNullOrEmpty(d))
                    d = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BuildArchives"));
                return d;
            }
            set => EditorPrefs.SetString("ItchDeploy_ArchiveDir", value);
        }

        public static bool IncludeSymbols
        {
            get => EditorPrefs.GetBool("ItchDeploy_ArchiveSymbols", false);
            set => EditorPrefs.SetBool("ItchDeploy_ArchiveSymbols", value);
        }

        internal static string SanitizeFileName(string value)
            => CrossPlatformFileName.Sanitize(value, "Build");

        public static string ArchiveBuild(string buildDir, BuildTarget target, string version)
        {
            if (!Directory.Exists(buildDir))
                return null;

            var archiveRoot = ArchiveDir;
            if (!Directory.Exists(archiveRoot))
                Directory.CreateDirectory(archiveRoot);

            var platformName = target.ToString();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var archiveName =
                $"{SanitizeFileName(Application.productName)}_{platformName}_" +
                $"v{SanitizeFileName(version)}_{timestamp}.zip";
            var archivePath = Path.Combine(archiveRoot, archiveName);

            var partialPath = archivePath + ".partial";

            try
            {
                if (File.Exists(archivePath))
                    File.Delete(archivePath);
                if (File.Exists(partialPath))
                    File.Delete(partialPath);

                var allFiles = Directory.GetFiles(buildDir, "*", SearchOption.AllDirectories);

                // Windows refuses to open a path of 260 characters or more through the
                // classic API this runtime uses, and reports it as a missing path - which
                // sends people looking for a file that is sitting right there.
                var tooLong = allFiles.FirstOrDefault(file => file.Length >= 260);
                if (tooLong != null)
                {
                    Debug.LogError(
                        "[BuildArchive] Cannot archive: a file path is " + tooLong.Length +
                        " characters, and Windows cannot open 260 or more. Shorten the project " +
                        "path, the product name, or the build folder template. Path: " + tooLong);
                    return null;
                }

                using (var stream = new FileStream(partialPath, FileMode.CreateNew))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    foreach (var file in allFiles)
                    {
                        if (!IncludeSymbols && file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!IncludeSymbols && file.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var relativePath = file.Substring(buildDir.Length)
                            .TrimStart('\\', '/')
                            .Replace('\\', '/');
                        var entry = archive.CreateEntry(relativePath, System.IO.Compression.CompressionLevel.Optimal);
                        using var entryStream = entry.Open();
                        using var fileStream = File.OpenRead(file);
                        fileStream.CopyTo(entryStream);
                    }
                }

                File.Move(partialPath, archivePath);
                var sizeMB = new FileInfo(archivePath).Length / (1024f * 1024f);
                Debug.Log($"[BuildArchive] Created: {archiveName} ({sizeMB:F1} MB)");
                return archivePath;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(partialPath)) File.Delete(partialPath); }
                catch (IOException) { }
                Debug.LogError($"[BuildArchive] Failed: {ex.Message}");
                return null;
            }
        }

        public static VisualElement CreateUI()
        {
            var root = new VisualElement();
            root.AddToClassList("settings-archive-card");

            var title = new Label(ItchDeployLocale._(
                "Build archives", "Архіви збірок"));
            title.AddToClassList("settings-card-title");
            root.Add(title);

            var help = new Label(ItchDeployLocale._(
                "Create ZIP archives before publishing and keep them in a separate folder.",
                "Створює ZIP-архіви перед публікацією та зберігає їх в окремій папці."));
            help.AddToClassList("settings-card-help");
            help.style.whiteSpace = WhiteSpace.Normal;
            root.Add(help);

            var autoToggleRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._(
                    "Archive automatically before publishing",
                    "Архівувати автоматично перед публікацією"),
                AutoArchive,
                out var autoToggle,
                "archive-auto-toggle");
            autoToggle.RegisterValueChangedCallback(ev => AutoArchive = ev.newValue);
            root.Add(autoToggleRow);

            var details = new VisualElement();
            details.AddToClassList("settings-card-details");
            details.AddToClassList("settings-archive-details");
            root.Add(details);

            var folderRow = new VisualElement();
            folderRow.style.flexDirection = FlexDirection.Row;
            folderRow.style.alignItems = Align.Center;
            folderRow.AddToClassList("settings-control-row");
            folderRow.AddToClassList("settings-folder-row");

            var folderField = new TextField(ItchDeployLocale._(
                "Archive folder", "Папка архівів"));
            folderField.value = ArchiveDir;
            folderField.style.flexGrow = 1;
            folderField.AddToClassList("settings-folder-field");
            folderField.RegisterValueChangedCallback(ev => ArchiveDir = ev.newValue);
            folderRow.Add(folderField);

            var browseBtn = new Button(() =>
            {
                var folder = EditorUtility.OpenFolderPanel("Select Archive Folder", ArchiveDir, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    ArchiveDir = folder;
                    folderField.value = folder;
                }
            });
            browseBtn.text = ItchDeployLocale._(
                "Choose folder",
                "Обрати папку");
            browseBtn.tooltip = ItchDeployLocale._(
                "Choose where build archives are stored.",
                "Виберіть папку для збереження архівів збірок.");
            browseBtn.AddToClassList("secondary-action");
            browseBtn.AddToClassList("settings-folder-browse-action");
            folderRow.Add(browseBtn);
            details.Add(folderRow);

            var symbolsToggleRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._(
                    "Include debug symbols",
                    "Додавати символи налагодження"),
                IncludeSymbols,
                out var symbolsToggle,
                "archive-symbols-toggle");
            symbolsToggleRow.AddToClassList("settings-detail-toggle-row");
            symbolsToggle.RegisterValueChangedCallback(ev => IncludeSymbols = ev.newValue);
            details.Add(symbolsToggleRow);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.AddToClassList("settings-action-row");
            btnRow.AddToClassList("settings-button-pair");
            btnRow.AddToClassList("settings-archive-actions");

            var openArchiveBtn = new Button(() => EditorUtility.RevealInFinder(ArchiveDir));
            openArchiveBtn.text = ItchDeployLocale._(
                "Open archives folder", "Відкрити папку архівів");
            openArchiveBtn.AddToClassList("secondary-action");
            btnRow.Add(openArchiveBtn);

            var openBuildBtn = new Button(() =>
                EditorUtility.RevealInFinder(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"))));
            openBuildBtn.text = ItchDeployLocale._(
                "Open builds folder", "Відкрити папку збірок");
            openBuildBtn.AddToClassList("secondary-action");
            openBuildBtn.AddToClassList("last");
            btnRow.Add(openBuildBtn);
            details.Add(btnRow);

            if (Directory.Exists(ArchiveDir))
            {
                var stats = new VisualElement();
                stats.AddToClassList("settings-card-stats");
                details.Add(stats);
                var files = Directory.GetFiles(ArchiveDir, "*.zip");
                var countLabel = new Label(ItchDeployLocale._(
                    "Existing archives", "Архівів") + ": " + files.Length);
                countLabel.AddToClassList("settings-muted");
                stats.Add(countLabel);

                long totalSize = 0;
                foreach (var f in files) totalSize += new FileInfo(f).Length;
                var sizeLabel = new Label(ItchDeployLocale._(
                    "Total size", "Загальний розмір") + ": " +
                    $"{(totalSize / (1024f * 1024f * 1024f)):F2} GB");
                sizeLabel.AddToClassList("settings-muted");
                stats.Add(sizeLabel);
            }

            void Refresh()
            {
                details.style.display = AutoArchive ? DisplayStyle.Flex : DisplayStyle.None;
            }

            autoToggle.RegisterValueChangedCallback(ev => Refresh());
            Refresh();

            return root;
        }
    }
}
