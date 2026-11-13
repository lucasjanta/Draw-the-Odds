using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ItchDeploy.Editor
{
    public static class BackupManager
    {
        public readonly struct BackupStats
        {
            public BackupStats(int count, long totalBytes)
            {
                Count = count;
                TotalBytes = totalBytes;
            }

            public int Count { get; }
            public long TotalBytes { get; }
        }

        private static readonly HashSet<string> ExcludedRootDirectories = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "Library",
            "Temp",
            "obj",
            "Logs",
            "Builds",
            "BuildArchives",
            "BuildReports",
            "Backups",
            ".git",
            ".vs",
            ".idea"
        };

        public static bool AutoBackup
        {
            get => EditorPrefs.GetBool("ItchDeploy_AutoBackup", false);
            set => EditorPrefs.SetBool("ItchDeploy_AutoBackup", value);
        }

        public static string BackupDir
        {
            get
            {
                var d = EditorPrefs.GetString("ItchDeploy_BackupDir", "");
                if (string.IsNullOrEmpty(d))
                    d = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backups"));
                return d;
            }
            set => EditorPrefs.SetString("ItchDeploy_BackupDir", value);
        }

        public static int MaxBackups
        {
            get => EditorPrefs.GetInt("ItchDeploy_MaxBackups", 5);
            set => EditorPrefs.SetInt("ItchDeploy_MaxBackups", Mathf.Max(1, value));
        }

        public static string RunBackup()
        {
            var projectDir = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectDir)) return null;

            try
            {
                var backupPath = CreateBackup(
                    projectDir,
                    BackupDir,
                    Application.productName,
                    MaxBackups);
                var sizeMB = new FileInfo(backupPath).Length / (1024f * 1024f);
                Debug.Log(
                    $"[BackupManager] Created: {Path.GetFileName(backupPath)} ({sizeMB:F1} MB)");
                return backupPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BackupManager] Failed: {ex.Message}");
                return null;
            }
        }

        public static string CreateBackup(
            string projectDirectory,
            string backupDirectory,
            string projectName,
            int maxBackups)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory))
                throw new ArgumentException("Project directory is required.", nameof(projectDirectory));
            if (string.IsNullOrWhiteSpace(backupDirectory))
                throw new ArgumentException("Backup directory is required.", nameof(backupDirectory));

            var projectRoot = NormalizeDirectory(projectDirectory);
            var backupRoot = NormalizeDirectory(backupDirectory);
            if (!Directory.Exists(projectRoot))
                throw new DirectoryNotFoundException(
                    $"Project directory does not exist: {projectRoot}");

            Directory.CreateDirectory(backupRoot);

            var safeProjectName = SanitizeFileName(projectName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var backupPath = GetUniqueBackupPath(
                backupRoot,
                $"{safeProjectName}_{timestamp}");
            var partialPath = Path.Combine(
                backupRoot,
                $".{Path.GetFileName(backupPath)}.{Guid.NewGuid():N}.partial");

            var sourceFiles = EnumerateSourceFiles(projectRoot, backupRoot).ToList();

            try
            {
                using (var stream = new FileStream(
                           partialPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           64 * 1024,
                           FileOptions.SequentialScan))
                using (var archive = new ZipArchive(
                           stream,
                           ZipArchiveMode.Create,
                           leaveOpen: false))
                {
                    foreach (var file in sourceFiles)
                    {
                        var relativePath = GetRelativeArchivePath(projectRoot, file);
                        var entry = archive.CreateEntry(
                            relativePath,
                            System.IO.Compression.CompressionLevel.Fastest);
                        using var entryStream = entry.Open();
                        using var sourceStream = new FileStream(
                            file,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete,
                            64 * 1024,
                            FileOptions.SequentialScan);
                        sourceStream.CopyTo(entryStream);
                    }
                }

                File.Move(partialPath, backupPath);
                CleanOldBackups(
                    backupRoot,
                    safeProjectName,
                    Mathf.Max(1, maxBackups));
                return backupPath;
            }
            catch
            {
                TryDeletePartial(partialPath);
                throw;
            }
        }

        private static IEnumerable<string> EnumerateSourceFiles(
            string projectRoot,
            string backupRoot)
        {
            var stack = new Stack<string>();
            stack.Push(projectRoot);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                var files = Directory.GetFiles(current);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    if (PathsEqual(projectRoot, backupRoot) &&
                        (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                         file.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    yield return file;
                }

                var directories = Directory.GetDirectories(current);
                Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
                for (var i = directories.Length - 1; i >= 0; i--)
                {
                    var directory = NormalizeDirectory(directories[i]);
                    if (ShouldSkipDirectory(projectRoot, backupRoot, directory))
                        continue;

                    stack.Push(directory);
                }
            }
        }

        private static bool ShouldSkipDirectory(
            string projectRoot,
            string backupRoot,
            string directory)
        {
            var info = new DirectoryInfo(directory);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;

            if (!PathsEqual(projectRoot, backupRoot) &&
                IsSameOrChildPath(directory, backupRoot))
                return true;

            var parent = NormalizeDirectory(info.Parent?.FullName ?? string.Empty);
            return PathsEqual(parent, projectRoot) &&
                   ExcludedRootDirectories.Contains(info.Name);
        }

        private static string GetRelativeArchivePath(
            string projectRoot,
            string file)
        {
            var rootWithSeparator = projectRoot + Path.DirectorySeparatorChar;
            return file.Substring(rootWithSeparator.Length)
                .Replace('\\', '/');
        }

        private static string NormalizeDirectory(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(left, right, PathComparison);
        }

        private static bool IsSameOrChildPath(string path, string root)
        {
            if (PathsEqual(path, root))
                return true;

            var rootWithSeparator = root + Path.DirectorySeparatorChar;
            return path.StartsWith(rootWithSeparator, PathComparison);
        }

        private static StringComparison PathComparison =>
            Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        private static string SanitizeFileName(string value)
            => CrossPlatformFileName.Sanitize(value, "UnityProject");

        private static string GetUniqueBackupPath(
            string backupRoot,
            string baseName)
        {
            var path = Path.Combine(backupRoot, baseName + ".zip");
            for (var suffix = 2; File.Exists(path); suffix++)
                path = Path.Combine(backupRoot, $"{baseName}_{suffix}.zip");
            return path;
        }

        private static void TryDeletePartial(string partialPath)
        {
            try
            {
                if (File.Exists(partialPath))
                    File.Delete(partialPath);
            }
            catch (Exception cleanupError)
            {
                Debug.LogWarning(
                    $"[BackupManager] Could not remove partial backup: {cleanupError.Message}");
            }
        }

        private static void CleanOldBackups(
            string backupRoot,
            string safeProjectName,
            int maxBackups)
        {
            var files = Directory
                .GetFiles(backupRoot, $"{safeProjectName}_*.zip")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (files.Length <= maxBackups) return;

            foreach (var file in files.Skip(maxBackups))
            {
                try
                {
                    file.Delete();
                    Debug.Log($"[BackupManager] Removed old backup: {file.Name}");
                }
                catch (Exception cleanupError)
                {
                    Debug.LogWarning(
                        $"[BackupManager] Could not remove old backup {file.Name}: " +
                        cleanupError.Message);
                }
            }
        }

        public static BackupStats GetBackupStats(string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory) ||
                !Directory.Exists(backupDirectory))
                return new BackupStats(0, 0);

            try
            {
                var count = 0;
                long totalBytes = 0;
                foreach (var path in Directory.EnumerateFiles(
                             backupDirectory,
                             "*",
                             SearchOption.TopDirectoryOnly))
                {
                    if (!string.Equals(
                            Path.GetExtension(path),
                            ".zip",
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    count++;
                    totalBytes += new FileInfo(path).Length;
                }

                return new BackupStats(count, totalBytes);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is System.Security.SecurityException ||
                ex is ArgumentException)
            {
                return new BackupStats(0, 0);
            }
        }

        public static VisualElement CreateUI()
        {
            var root = new VisualElement();
            root.AddToClassList("settings-backup-card");

            var title = new Label(ItchDeployLocale._(
                "Project backups", "Резервні копії проєкту"));
            title.AddToClassList("settings-card-title");
            root.Add(title);

            var help = new Label(ItchDeployLocale._(
                "Create ZIP backups before building. Library, Temp, Builds, obj and the backup folder are excluded.",
                "Створює ZIP-копію перед збіркою. Library, Temp, Builds, obj і папка резервних копій не копіюються."));
            help.AddToClassList("settings-card-help");
            help.style.whiteSpace = WhiteSpace.Normal;
            root.Add(help);

            var autoToggleRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._(
                    "Back up automatically before building",
                    "Створювати копію автоматично перед збіркою"),
                AutoBackup,
                out var autoToggle,
                "backup-auto-toggle");
            autoToggle.RegisterValueChangedCallback(ev => AutoBackup = ev.newValue);
            root.Add(autoToggleRow);

            var details = new VisualElement();
            details.AddToClassList("settings-card-details");
            details.AddToClassList("settings-backup-details");
            root.Add(details);

            var maxField = new IntegerField(ItchDeployLocale._(
                "Backups to keep", "Кількість збережених копій"));
            maxField.value = MaxBackups;
            maxField.RegisterValueChangedCallback(ev => MaxBackups = ev.newValue);
            maxField.AddToClassList("settings-backup-count");
            details.Add(maxField);

            var countLabel = new Label
            {
                name = "backup-count-value"
            };
            countLabel.AddToClassList("settings-muted");

            var sizeLabel = new Label
            {
                name = "backup-size-value"
            };
            sizeLabel.AddToClassList("settings-muted");

            var folderRow = new VisualElement();
            folderRow.style.flexDirection = FlexDirection.Row;
            folderRow.style.alignItems = Align.Center;
            folderRow.AddToClassList("settings-control-row");
            folderRow.AddToClassList("settings-folder-row");

            var folderField = new TextField(ItchDeployLocale._(
                "Backup folder", "Папка резервних копій"));
            folderField.value = BackupDir;
            folderField.style.flexGrow = 1;
            folderField.AddToClassList("settings-folder-field");
            folderField.RegisterValueChangedCallback(ev =>
            {
                BackupDir = ev.newValue;
                RefreshStats();
            });
            folderRow.Add(folderField);

            var browseBtn = new Button(() =>
            {
                var folder = EditorUtility.OpenFolderPanel("Select Backup Folder", BackupDir, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    BackupDir = folder;
                    folderField.value = folder;
                    RefreshStats();
                }
            });
            browseBtn.text = ItchDeployLocale._(
                "Choose folder",
                "Обрати папку");
            browseBtn.tooltip = ItchDeployLocale._(
                "Choose where project backups are stored.",
                "Виберіть папку для збереження резервних копій проєкту.");
            browseBtn.AddToClassList("secondary-action");
            browseBtn.AddToClassList("settings-folder-browse-action");
            folderRow.Add(browseBtn);
            details.Add(folderRow);

            var stats = new VisualElement();
            stats.AddToClassList("settings-card-stats");
            stats.Add(countLabel);
            stats.Add(sizeLabel);

            void RefreshStats()
            {
                var current = GetBackupStats(BackupDir);
                countLabel.text = ItchDeployLocale._(
                    "Existing backups", "Резервних копій") +
                    ": " + current.Count;
                sizeLabel.text = ItchDeployLocale._(
                    "Total size", "Загальний розмір") +
                    ": " +
                    $"{(current.TotalBytes / (1024f * 1024f * 1024f)):F2} GB";
            }

            void Refresh()
            {
                details.style.display = AutoBackup ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var backupNowBtn = new Button(() =>
            {
                var path = RunBackup();
                if (path != null)
                {
                    if (EditorUtility.DisplayDialog("Backup Complete",
                            $"Saved to:\n{path}", "Open Folder", "OK"))
                        EditorUtility.RevealInFinder(Path.GetDirectoryName(path));
                }
                else
                {
                    EditorUtility.DisplayDialog("Backup Failed", "Check console for details.", "OK");
                }

                RefreshStats();
            });
            backupNowBtn.text = ItchDeployLocale._(
                "Back up now", "Створити копію зараз");
            backupNowBtn.AddToClassList("settings-wide-action");
            backupNowBtn.AddToClassList("settings-backup-primary-action");
            root.Add(backupNowBtn);

            var openFolderBtn = new Button(() => EditorUtility.RevealInFinder(BackupDir));
            openFolderBtn.text = ItchDeployLocale._(
                "Open backups folder", "Відкрити папку резервних копій");
            openFolderBtn.AddToClassList("secondary-action");
            openFolderBtn.AddToClassList("settings-wide-secondary");
            openFolderBtn.AddToClassList("settings-backup-open-action");
            root.Add(openFolderBtn);
            root.Add(stats);

            autoToggle.RegisterValueChangedCallback(ev => Refresh());
            Refresh();
            RefreshStats();
            root.schedule.Execute(RefreshStats).Every(750);

            return root;
        }
    }
}
