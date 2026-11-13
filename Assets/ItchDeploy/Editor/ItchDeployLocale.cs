using System.Globalization;
using UnityEditor;

namespace ItchDeploy.Editor
{
    public static class ItchDeployLocale
    {
        private const string PrefKey = "ItchDeploy_Locale";

        public enum Lang { English, Ukrainian }

        private static Lang? _current;

        public static Lang Current
        {
            get
            {
                if (_current.HasValue)
                    return _current.Value;

                _current = EditorPrefs.HasKey(PrefKey)
                    ? (Lang)EditorPrefs.GetInt(PrefKey, (int)Lang.English)
                    : DetectDefault();
                return _current.Value;
            }
            set
            {
                _current = value;
                EditorPrefs.SetInt(PrefKey, (int)value);
            }
        }

        internal static Lang DetectDefault() => ResolveDefault(SystemLanguageCode);

        internal static Lang ResolveDefault(string twoLetterLanguageCode) =>
            string.Equals(twoLetterLanguageCode, "uk", System.StringComparison.OrdinalIgnoreCase)
                ? Lang.Ukrainian
                : Lang.English;

        private static string SystemLanguageCode
        {
            get
            {
                try { return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; }
                catch { return "en"; }
            }
        }

        public static string _(string en, string uk) => Current == Lang.Ukrainian ? uk : en;

        public static string TabDashboard => _("Overview", "Огляд");
        public static string TabPipeline => _("Pipeline", "Послідовність кроків");
        public static string TabDeploy => _("Build & deploy", "Збірка та деплой");
        public static string TabPreview => _("Preflight check", "Перевірка перед деплоєм");
        public static string TabProfiles => _("Project profiles", "Профілі проєктів");
        public static string TabAnalytics => _("itch.io page statistics", "Статистика сторінки itch.io");
        public static string TabNotifications => _("Team notifications", "Сповіщення для команди");
        public static string TabSettings => _("Project settings", "Налаштування проєкту");
        public static string TabItchToml => _("itch app launch manifest", "Маніфест запуску itch app");
        public static string TabScheduler => _("Local editor scheduler", "Локальний розклад редактора");
        public static string TabWebhook => _("HTTP integrations", "HTTP-інтеграції");
        public static string TabMatrix => _("Multi-platform deploy", "Деплой кількох платформ");
        public static string TabPromoter => _("Copy channel", "Копіювання каналу");
        public static string TabChangelog => _("Git changes → CHANGELOG.md", "Git-зміни → CHANGELOG.md");
        public static string TabRelease => _("Release notes & Git tag", "Нотатки релізу та Git-тег");
        public static string TabReports => _("Local HTML/JSON reports", "Локальні HTML/JSON звіти");
        public static string TabDefines => _("Scripting define symbols", "Символи компіляції коду");
        public static string TabVerify => _("Build integrity", "Цілісність збірки");
        public static string TabWorkspace => _("Recent Unity projects", "Останні Unity-проєкти");

        public static string BuildDeploy => _("Build and deploy", "Зібрати й завантажити");
        public static string DeployExisting => _("Deploy existing build", "Завантажити готову збірку");
        public static string AutoFix => _("Fix build settings", "Виправити налаштування збірки");
        public static string RefreshPreview => _("Refresh check", "Оновити перевірку");
        public static string SaveKey => _("Save key securely", "Зберегти ключ безпечно");
        public static string TestConnection => _("Verify itch.io access", "Перевірити доступ до itch.io");
        public static string InstallButler => _("Install Butler", "Встановити Butler");
        public static string CheckUpdates => _("Check for updates", "Перевірити оновлення");
        public static string RunPipeline => _("Run pipeline", "Запустити послідовність");
        public static string AllPlatforms => _("All platforms", "Усі платформи");
        public static string Changelog => _("Generate CHANGELOG.md", "Створити CHANGELOG.md");
        public static string Rollback => _("Rollback", "Відкотити версію");
        public static string Version => _("Version", "Версія");
        public static string Target => _("Target", "Платформа");
        public static string Channel => _("Channel", "Канал");
        public static string Username => _("itch.io username", "Ім’я користувача itch.io");
        public static string GameSlug => _("Project URL slug", "Частина URL проєкту");
        public static string ApiKey => _("itch.io API key", "API-ключ itch.io");
        public static string WebGLCompression => _("WebGL compression", "Стиснення WebGL");
        public static string AutoDeploy => _("Deploy after a successful build", "Виконувати деплой після успішної збірки");
        public static string Folder => _("Folder", "Папка");
        public static string Path => _("Path", "Шлях");
        public static string Product => _("Product", "Продукт");
        public static string Company => _("Company", "Компанія");
        public static string Language => _("Language", "Мова");
        public static string Import => _("Import settings", "Імпортувати налаштування");
        public static string Export => _("Export settings", "Експортувати налаштування");

        public static string MissingCredentials => _("Set up itch.io access in Project settings", "Налаштуйте доступ до itch.io в налаштуваннях проєкту");
        public static string ButlerNotFound => _("Butler is not installed", "Butler не встановлено");
        public static string Downloading => _("Downloading…", "Завантаження…");
        public static string Processing => _("Processing…", "Обробка…");
        public static string Success => _("Success", "Успішно");
        public static string Failed => _("Failed", "Помилка");
        public static string NoBuildFound => _("No build found", "Збірку не знайдено");
        public static string PipelineComplete => _("Pipeline complete", "Послідовність завершена");

        public static string DashboardTitle => _("Project overview", "Огляд проєкту");
        public static string SetupChecklist => _("Setup checklist", "Що потрібно налаштувати");
        public static string CheckButler => _("Butler", "Butler");
        public static string CheckCreds => _("itch.io project", "Проєкт itch.io");
        public static string CheckApiKey => _("itch.io authorization", "Авторизація itch.io");
        public static string CheckAutoDeploy => _("Deploy after build", "Деплой після збірки");
        public static string NextStep => _("Next step", "Наступний крок");
        public static string QuickActions => _("Quick actions", "Швидкі дії");
        public static string ProjectInfo => _("Project information", "Інформація про проєкт");
        public static string ReadyToDeploy => _("Ready to deploy", "Готово до деплою");
        public static string NeedsSetup => _("Setup required", "Потрібне налаштування");
        public static string RefreshButler => _("Check Butler", "Перевірити Butler");
        public static string CompleteSetup => _("Complete the checklist to enable deployment", "Виконайте чекліст, щоб увімкнути деплой");
        public static string DeployNow => _("Build and deploy", "Зібрати й завантажити");
        public static string DeployTo => _("Publish to", "Публікація до");
        public static string SetupGuide => _("Set up the project in three steps", "Налаштуйте проєкт за три кроки");
        public static string StepConnect => _("1. Connect an itch.io account", "1. Підключіть акаунт itch.io");
        public static string StepSelect => _("2. Select an itch.io project", "2. Виберіть проєкт itch.io");
        public static string StepDeploy => _("3. Build and deploy", "3. Зберіть і завантажте");
        public static string SetupQuickDesc => _("Connect an account and select a project automatically", "Підключіть акаунт і виберіть проєкт автоматично");
        public static string NoSecretsCI => _("Deploy directly from this Unity Editor", "Виконуйте деплой безпосередньо з цього Unity Editor");
        public static string ProjectConfigured => _("Project configured", "Проєкт налаштовано");
        public static string GitHubAction => _("Generate GitHub Actions workflow", "Створити workflow GitHub Actions");
        public static string GitHubActionDesc => _("Creates .github/workflows/build-and-deploy.yml", "Створює .github/workflows/build-and-deploy.yml");
        public static string GitHubActionDone => _("GitHub Actions workflow created at", "Workflow GitHub Actions створено у");
        public static string GitHubActionInfo => _("A push can build and deploy to itch.io automatically", "Push може автоматично зібрати проєкт і виконати деплой на itch.io");
        public static string Workflow => _("Workflow", "Автоматизація");
        public static string WorkflowDesc => _("How builds are deployed", "Як виконується деплой збірок на itch.io");
        public static string LocalDeploy => _("Local: Unity build → itch.io", "Локально: Unity збирає → itch.io публікує");
        public static string LocalDeployDesc => _("Build in Unity and deploy when it succeeds.", "Зберіть проєкт у Unity та виконайте деплой після успішної збірки.");
        public static string CIDeploy => _("CI/CD: Git push → itch.io", "CI/CD: Git push → itch.io");
        public static string CIDeployDesc => _("Optional server-side deployment through GitHub Actions.", "Необов’язковий серверний деплой через GitHub Actions.");
        public static string DeployTarget => _("Project", "Проєкт");
        public static string GitHubTrigger => _("When should the workflow run?", "Коли запускати workflow?");
        public static string GitHubPushMain => _("On push to main/master", "Після push у main/master");
        public static string GitHubTag => _("On a Git tag", "За Git-тегом");
        public static string GitHubManual => _("Manual only", "Лише вручну");
        public static string GitHubNoBuild => _("Deploy only — do not build", "Лише деплой — без збірки");
        public static string GitHubAutoSecret => _("Set GitHub secret automatically", "Автоматично задати секрет GitHub");
        public static string GitHubSecretOk => _("Secret set with gh CLI", "Секрет задано через gh CLI");
        public static string GitHubSecretFail => _("Could not set a secret automatically. Opening GitHub…", "Не вдалося задати секрет автоматично. Відкриваю GitHub…");
        public static string GitHubHowToSecret => _("Or add BUTLER_API_KEY in repository secrets", "Або додайте BUTLER_API_KEY у secrets репозиторію");

        public static string FieldUsername => _("Your itch.io account name", "Ваше ім’я акаунта itch.io");
        public static string FieldGameSlug => _("The last part of your game page URL", "Остання частина URL сторінки гри");
        public static string FieldApiKey => _("Create it in itch.io account settings", "Створіть його в налаштуваннях акаунта itch.io");
        public static string FieldVersion => _("Version shown to players", "Версія, яку побачать гравці");
        public static string FieldChannel => _("Distribution channel, for example windows or html5", "Канал розповсюдження, наприклад windows або html5");
        public static string FieldWebhookUrl => _("Incoming webhook URL", "URL вхідного webhook");
        public static string FieldTelegramToken => _("Token from @BotFather", "Токен від @BotFather");
        public static string FieldTelegramChat => _("Telegram chat ID", "ID чату Telegram");
        public static string FieldBuildFolder => _("Build output folder", "Папка з результатом збірки");

        public static string InfoCredentials => _("Connect your itch.io account", "Підключіть ваш акаунт itch.io");
        public static string InfoBuildSettings => _("Configure build and deployment options", "Налаштуйте збірку та деплой");
        public static string InfoNotifications => _("Send deployment results to Discord, Slack, or Telegram", "Надсилайте результат деплою в Discord, Slack або Telegram");

        public static string GroupSetup => _("Setup", "Налаштування");
        public static string GroupDeploy => _("Deploy", "Деплой");
        public static string GroupManage => _("History", "Історія");
        public static string GroupTools => _("Tools", "Інструменти");
        public static string GroupSystem => _("System", "Система");
    }
}
