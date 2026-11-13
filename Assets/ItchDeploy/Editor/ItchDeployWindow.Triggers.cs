using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ItchDeploy.Editor
{
    public partial class ItchDeployWindow
    {
        private VisualElement BuildSettingsTriggersUI()
        {
            var root = new VisualElement();

            var intro = new Label(ItchDeployLocale._(
                "Everything on this screen can publish or build without you pressing Publish. All of it is off until you turn it on.",
                "Усе на цьому екрані може зібрати або опублікувати проєкт без натискання «Опублікувати». Усе вимкнено, доки ви не увімкнете."));
            intro.AddToClassList("settings-card-help");
            intro.style.whiteSpace = WhiteSpace.Normal;
            root.Add(intro);

            AddSettingsSpacer(root);
            root.Add(BuildAfterBuildTriggerCard());
            AddSettingsSpacer(root);
            root.Add(BuildFolderWatcherCard());
            AddSettingsSpacer(root);
            root.Add(BuildScheduleCard());
            AddSettingsSpacer(root);
            root.Add(BuildReleaseSideEffectsCard());
            AddSettingsSpacer(root);
            root.Add(BuildWebhookReceiverCard());

            return root;
        }

        private VisualElement BuildAfterBuildTriggerCard()
        {
            var card = new VisualElement();
            card.AddToClassList("deploy-panel");

            var title = new Label(ItchDeployLocale._(
                "Publish after every Unity build", "Публікувати після кожної збірки Unity"));
            title.AddToClassList("settings-card-title");
            card.Add(title);

            var help = new Label(ItchDeployLocale._(
                "Uploads to itch.io whenever any successful build finishes, including builds started from Unity's own Build Settings window or by another tool.",
                "Завантажує на itch.io після будь-якої успішної збірки — зокрема запущеної з вікна Build Settings самої Unity або іншим інструментом."));
            help.AddToClassList("settings-card-help");
            help.style.whiteSpace = WhiteSpace.Normal;
            card.Add(help);

            var row = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._("Publish after build", "Публікувати після збірки"),
                DeployWatcher.AutoDeployOnBuild,
                out var toggle,
                "trigger-auto-deploy");
            toggle.RegisterValueChangedCallback(ev => DeployWatcher.AutoDeployOnBuild = ev.newValue);
            card.Add(row);

            return card;
        }

        private VisualElement BuildFolderWatcherCard()
        {
            var card = new VisualElement();
            card.AddToClassList("deploy-panel");

            var title = new Label(ItchDeployLocale._(
                "Watch the build folder", "Стежити за папкою збірок"));
            title.AddToClassList("settings-card-title");
            card.Add(title);

            var help = new Label(ItchDeployLocale._(
                "Publishes when a build artifact appears or changes in the watched folder, even when the change came from outside Unity. Copying an old build in is enough to trigger an upload.",
                "Публікує, коли у папці з'являється або змінюється артефакт збірки — навіть якщо зміна прийшла не з Unity. Достатньо скопіювати туди стару збірку."));
            help.AddToClassList("settings-card-help");
            help.style.whiteSpace = WhiteSpace.Normal;
            card.Add(help);

            var details = new VisualElement();
            details.AddToClassList("settings-card-details");

            var pathField = ItchDeployUI.TextField(
                ItchDeployLocale.Folder,
                DeployWatcher.WatchedBuildPath,
                value => DeployWatcher.WatchedBuildPath = value);
            details.Add(pathField);

            var browse = ItchDeployUI.SecondaryButton(
                ItchDeployLocale._("Choose folder...", "Вибрати папку..."),
                () =>
                {
                    var folder = EditorUtility.OpenFolderPanel(
                        ItchDeployLocale._("Watched build folder", "Папка збірок для стеження"),
                        string.IsNullOrEmpty(DeployWatcher.WatchedBuildPath)
                            ? BuildDeployer.BuildsRoot
                            : DeployWatcher.WatchedBuildPath,
                        string.Empty);
                    if (string.IsNullOrEmpty(folder)) return;

                    DeployWatcher.WatchedBuildPath = folder;
                    pathField.SetValueWithoutNotify(folder);
                    if (DeployWatcher.WatcherEnabled)
                    {
                        DeployWatcher.StopFileWatcher();
                        DeployWatcher.StartFileWatcher();
                    }
                });
            browse.AddToClassList("settings-wide-secondary");
            details.Add(browse);

            var row = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._("Watch for new builds", "Стежити за новими збірками"),
                DeployWatcher.WatcherEnabled,
                out var toggle,
                "trigger-folder-watcher");
            toggle.RegisterValueChangedCallback(ev =>
            {
                DeployWatcher.WatcherEnabled = ev.newValue;
                if (ev.newValue) DeployWatcher.StartFileWatcher();
                else DeployWatcher.StopFileWatcher();
                details.style.display = ev.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            card.Add(row);

            details.style.display = DeployWatcher.WatcherEnabled
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            card.Add(details);

            return card;
        }

        private VisualElement BuildScheduleCard()
        {
            var card = new VisualElement();
            card.AddToClassList("deploy-panel");

            var title = new Label(ItchDeployLocale._(
                "Scheduled builds", "Збірки за розкладом"));
            title.AddToClassList("settings-card-title");
            card.Add(title);

            var help = new Label(ItchDeployLocale._(
                "Builds on an interval while this Editor stays open. A scheduled build takes the Editor over for the whole build.",
                "Збирає проєкт із заданим інтервалом, доки цей редактор відкритий. Збірка за розкладом займає редактор на весь час."));
            help.AddToClassList("settings-card-help");
            help.style.whiteSpace = WhiteSpace.Normal;
            card.Add(help);

            var details = new VisualElement();
            details.AddToClassList("settings-card-details");

            var platforms = BuildPlatformCatalog.Targets
                .Select(target => target.ToString())
                .ToList();
            var platformIndex = Mathf.Max(0, platforms.IndexOf(ScheduledBuilds.TargetPlatform));
            var platformField = new ItchDeployChoiceField(
                ItchDeployLocale.Target,
                platforms,
                platformIndex);
            platformField.SelectionChanged += (_, value) => ScheduledBuilds.TargetPlatform = value;
            details.Add(platformField);

            var interval = new IntegerField(ItchDeployLocale._("Every (hours)", "Кожні (годин)"))
            {
                value = ScheduledBuilds.IntervalHours,
            };
            interval.RegisterValueChangedCallback(ev =>
            {
                ScheduledBuilds.IntervalHours = Mathf.Max(1, ev.newValue);
                interval.SetValueWithoutNotify(ScheduledBuilds.IntervalHours);
            });
            details.Add(interval);

            var idleRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._("Only while the Editor is idle", "Лише коли редактор не активний"),
                ScheduledBuilds.BuildOnIdle,
                out var idleToggle,
                "trigger-schedule-idle");
            idleToggle.tooltip = ItchDeployLocale._(
                "Wait until the Editor window is not focused before starting a scheduled build.",
                "Чекати, доки вікно редактора не у фокусі, перш ніж почати збірку за розкладом.");
            idleToggle.RegisterValueChangedCallback(ev => ScheduledBuilds.BuildOnIdle = ev.newValue);
            details.Add(idleRow);

            var last = new Label(string.IsNullOrEmpty(ScheduledBuilds.LastBuildDate)
                ? ItchDeployLocale._("No scheduled build has run yet.", "Збірок за розкладом ще не було.")
                : ItchDeployLocale._("Last scheduled build: ", "Остання збірка за розкладом: ") +
                  ScheduledBuilds.LastBuildDate);
            last.AddToClassList("settings-muted");
            details.Add(last);

            var row = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._("Build on a schedule", "Збирати за розкладом"),
                ScheduledBuilds.Enabled,
                out var toggle,
                "trigger-schedule");
            toggle.RegisterValueChangedCallback(ev =>
            {
                ScheduledBuilds.Enabled = ev.newValue;
                details.style.display = ev.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            card.Add(row);

            details.style.display = ScheduledBuilds.Enabled ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(details);

            return card;
        }

        private VisualElement BuildReleaseSideEffectsCard()
        {
            var card = new VisualElement();
            card.AddToClassList("deploy-panel");

            var title = new Label(ItchDeployLocale._(
                "After a successful deployment", "Після успішного деплою"));
            title.AddToClassList("settings-card-title");
            card.Add(title);

            var tagDetails = new VisualElement();
            tagDetails.AddToClassList("settings-card-details");

            var prefix = ItchDeployUI.TextField(
                ItchDeployLocale._("Tag prefix", "Префікс тега"),
                GitTagManager.TagPrefix,
                value => GitTagManager.TagPrefix = value);
            prefix.tooltip = ItchDeployLocale._(
                "Tags are named {prefix}{version}-{channel}.",
                "Теги мають вигляд {префікс}{версія}-{канал}.");
            tagDetails.Add(prefix);

            var pushRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._("Push the tag to origin", "Надсилати тег в origin"),
                GitTagManager.PushTags,
                out var pushToggle,
                "trigger-tag-push");
            pushToggle.RegisterValueChangedCallback(ev => GitTagManager.PushTags = ev.newValue);
            tagDetails.Add(pushRow);

            var tagRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._("Create a git tag", "Створювати git-тег"),
                GitTagManager.AutoTag,
                out var tagToggle,
                "trigger-auto-tag");
            tagToggle.RegisterValueChangedCallback(ev =>
            {
                GitTagManager.AutoTag = ev.newValue;
                tagDetails.style.display = ev.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            card.Add(tagRow);
            tagDetails.style.display = GitTagManager.AutoTag ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(tagDetails);

            var reportRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._("Write a deployment report", "Зберігати звіт про деплой"),
                ItchDeployReport.AutoGenerate,
                out var reportToggle,
                "trigger-reports");
            reportToggle.tooltip = ItchDeployLocale._(
                "Saves an HTML and JSON report of each deployment under BuildReports/.",
                "Зберігає HTML- і JSON-звіт кожного деплою в теку BuildReports/.");
            reportToggle.RegisterValueChangedCallback(ev => ItchDeployReport.AutoGenerate = ev.newValue);
            card.Add(reportRow);

            return card;
        }

        private VisualElement BuildWebhookReceiverCard()
        {
            var card = new VisualElement();
            card.AddToClassList("deploy-panel");

            var title = new Label(ItchDeployLocale._(
                "Local webhook receiver", "Локальний приймач webhook"));
            title.AddToClassList("settings-card-title");
            card.Add(title);

            var help = new Label(ItchDeployLocale._(
                "Listens for signed GitHub or GitLab push events and builds when main or master is pushed. Prefer GitHub Actions unless you specifically need the build to happen on this machine.",
                "Слухає підписані push-події GitHub або GitLab і збирає проєкт після push у main чи master. Якщо збірка не мусить відбуватись саме на цій машині, краще використовуйте GitHub Actions."));
            help.AddToClassList("settings-card-help");
            help.style.whiteSpace = WhiteSpace.Normal;
            card.Add(help);

            var details = new VisualElement();
            details.AddToClassList("settings-card-details");

            var secret = ItchDeployUI.TextField(
                ItchDeployLocale._("Shared secret", "Спільний секрет"),
                WebhookReceiver.Secret,
                value => WebhookReceiver.Secret = value,
                password: true);
            secret.tooltip = ItchDeployLocale._(
                "At least 16 characters. Stored encrypted, never in the project.",
                "Щонайменше 16 символів. Зберігається зашифровано, поза проєктом.");
            details.Add(secret);

            var port = new IntegerField(ItchDeployLocale._("Port", "Порт"))
            {
                value = WebhookReceiver.Port,
            };
            port.RegisterValueChangedCallback(ev =>
            {
                WebhookReceiver.Port = ev.newValue;
                port.SetValueWithoutNotify(WebhookReceiver.Port);
            });
            details.Add(port);

            var url = new Label($"http://127.0.0.1:{WebhookReceiver.Port}/webhook");
            url.AddToClassList("settings-muted");
            details.Add(url);
            port.RegisterValueChangedCallback(_ =>
                url.text = $"http://127.0.0.1:{WebhookReceiver.Port}/webhook");

            var remoteRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._("Listen on all interfaces", "Слухати на всіх інтерфейсах"),
                WebhookReceiver.AllowRemote,
                out var remoteToggle,
                "trigger-webhook-remote");
            remoteToggle.tooltip = ItchDeployLocale._(
                "Exposes this Editor to the network over plain HTTP. Prefer localhost plus an authenticated HTTPS tunnel.",
                "Відкриває цей редактор мережі через звичайний HTTP. Краще localhost плюс автентифікований HTTPS-тунель.");
            remoteToggle.RegisterValueChangedCallback(ev => WebhookReceiver.AllowRemote = ev.newValue);
            details.Add(remoteRow);

            if (!string.IsNullOrEmpty(WebhookReceiver.LastEvent))
            {
                var lastEvent = new Label(
                    ItchDeployLocale._("Last event: ", "Остання подія: ") + WebhookReceiver.LastEvent);
                lastEvent.AddToClassList("settings-muted");
                details.Add(lastEvent);
            }

            var row = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._("Receive push webhooks", "Приймати push-webhook"),
                WebhookReceiver.Enabled,
                out var toggle,
                "trigger-webhook");
            toggle.RegisterValueChangedCallback(ev =>
            {
                WebhookReceiver.Enabled = ev.newValue;

                toggle.SetValueWithoutNotify(WebhookReceiver.Enabled);
                details.style.display = toggle.value ? DisplayStyle.Flex : DisplayStyle.None;
            });
            card.Add(row);

            details.style.display = WebhookReceiver.Enabled ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(details);

            return card;
        }
    }
}
