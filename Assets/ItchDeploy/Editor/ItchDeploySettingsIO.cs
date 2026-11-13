using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class ItchDeploySettingsIO
    {
        public static void ExportToJson()
        {
            var settings = ItchDeploySettings.Load();
            ItchDeployConfig.WriteFromSettings(settings);
            var projectRoot = Path.GetDirectoryName(ItchDeployConfig.FilePath);
            var destination = EditorUtility.SaveFilePanel(
                ItchDeployLocale._(
                    "Save an ItchDeploy configuration copy",
                    "Зберегти копію конфігурації ItchDeploy"),
                projectRoot ?? string.Empty,
                ".itchdeploy",
                "json");
            if (string.IsNullOrWhiteSpace(destination))
                return;

            try
            {
                if (!PathsEqual(destination, ItchDeployConfig.FilePath))
                    File.Copy(ItchDeployConfig.FilePath, destination, overwrite: true);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog(
                    ItchDeployLocale._("Export failed", "Не вдалося експортувати"),
                    ex.Message,
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "ItchDeploy",
                ItchDeployLocale._(
                    $"Configuration copy saved to:\n{destination}\n\nAPI keys and tokens were not included.",
                    $"Копію конфігурації збережено у:\n{destination}\n\nAPI-ключі й токени не додавалися."),
                "OK");
        }

        public static void ImportFromJson()
        {
            var path = EditorUtility.OpenFilePanel(
                ItchDeployLocale._(
                    "Choose an ItchDeploy configuration",
                    "Виберіть конфігурацію ItchDeploy"),
                Path.GetDirectoryName(ItchDeployConfig.FilePath) ?? string.Empty,
                "json");
            if (string.IsNullOrEmpty(path))
                return;

            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog(
                    "ItchDeploy",
                    ItchDeployLocale._("File not found.", "Файл не знайдено."),
                    "OK");
                return;
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (System.Exception ex)
            {
                ShowImportError(ex.Message);
                return;
            }

            if (!TryValidateSettingsJson(json, out var validationError))
            {
                ShowImportError(validationError);
                return;
            }

            var confirmed = EditorUtility.DisplayDialog(
                ItchDeployLocale._("Import configuration?", "Імпортувати конфігурацію?"),
                ItchDeployLocale._(
                    "This replaces the current .itchdeploy.json and immediately applies its project, platform, build, and CI/CD settings. If a current file exists, it will be saved as .itchdeploy.json.backup.",
                    "Ця дія замінить поточний .itchdeploy.json і одразу застосує налаштування проєкту, платформ, збірки та CI/CD. Якщо поточний файл існує, його буде збережено як .itchdeploy.json.backup."),
                ItchDeployLocale._("Import", "Імпортувати"),
                ItchDeployLocale._("Cancel", "Скасувати"));
            if (!confirmed)
                return;

            var backupPath = ItchDeployConfig.FilePath + ".backup";
            try
            {
                if (File.Exists(ItchDeployConfig.FilePath))
                    File.Copy(ItchDeployConfig.FilePath, backupPath, overwrite: true);

                if (!PathsEqual(path, ItchDeployConfig.FilePath))
                    File.Copy(path, ItchDeployConfig.FilePath, overwrite: true);
                ItchDeployConfig.InvalidateCache();
                ItchDeployConfig.ApplyToSettings(ItchDeploySettings.Load());
            }
            catch (System.Exception ex)
            {
                try
                {
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, ItchDeployConfig.FilePath, overwrite: true);
                        ItchDeployConfig.InvalidateCache();
                    }
                }
                catch { }
                ShowImportError(ex.Message);
                return;
            }

            EditorUtility.DisplayDialog(
                "ItchDeploy",
                ItchDeployLocale._(
                    "Configuration imported and applied successfully.",
                    "Конфігурацію успішно імпортовано й застосовано."),
                "OK");
        }

        internal static bool TryValidateSettingsJson(string json, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = ItchDeployLocale._(
                    "The selected file is empty.",
                    "Вибраний файл порожній.");
                return false;
            }

            try
            {
                if (MiniJson.Parse(json) is not MiniJson.Object root)
                {
                    error = ItchDeployLocale._(
                        "The selected file is not a JSON object.",
                        "Вибраний файл не є JSON-об’єктом.");
                    return false;
                }

                var recognized = root.Members.ContainsKey("itch_user") ||
                                 root.Members.ContainsKey("itch_game") ||
                                 root.Members.ContainsKey("channels") ||
                                 root.Members.ContainsKey("ci");
                if (!recognized)
                {
                    error = ItchDeployLocale._(
                        "This JSON does not contain ItchDeploy configuration fields. Choose a .itchdeploy.json file created by ItchDeploy.",
                        "Цей JSON не містить полів конфігурації ItchDeploy. Виберіть файл .itchdeploy.json, створений ItchDeploy.");
                    return false;
                }

                ItchDeployConfig.Deserialize(json);
                return true;
            }
            catch (System.Exception ex)
            {
                error = ItchDeployLocale._(
                    "Invalid ItchDeploy JSON: ",
                    "Некоректний JSON ItchDeploy: ") + ex.Message;
                return false;
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            var comparison = Application.platform == RuntimePlatform.WindowsEditor
                ? System.StringComparison.OrdinalIgnoreCase
                : System.StringComparison.Ordinal;
            return string.Equals(
                Path.GetFullPath(left ?? string.Empty),
                Path.GetFullPath(right ?? string.Empty),
                comparison);
        }

        private static void ShowImportError(string message)
        {
            EditorUtility.DisplayDialog(
                ItchDeployLocale._("Import failed", "Не вдалося імпортувати"),
                message,
                "OK");
        }
    }
}
