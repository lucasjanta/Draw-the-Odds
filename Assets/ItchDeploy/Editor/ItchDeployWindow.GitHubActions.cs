using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ItchDeploy.Editor
{
    public partial class ItchDeployWindow
    {
        private VisualElement BuildSettingsGitHubActionsUI()
        {
            var root = new VisualElement();
            root.name = "github-actions-settings-panel";
            var config = ItchDeployConfig.Load();
            config.ci ??= new ItchDeployConfig.CiData();

            AddSettingsSectionTitle(root, "GitHub Actions", string.Empty);
            var gitHubCard = new VisualElement();
            gitHubCard.AddToClassList("deploy-panel");
            gitHubCard.AddToClassList("settings-ci-provider-card");
            gitHubCard.name = "github-actions-card";
            var gitHubInfo = new Label(ItchDeployLocale._(
                "Builds the selected platforms on GitHub runners. ItchDeploy generates and updates the workflow; repository secrets remain in GitHub.",
                "Збирає вибрані платформи на GitHub runners. ItchDeploy створює та оновлює workflow, а секрети залишаються в GitHub."));
            gitHubInfo.AddToClassList("settings-card-help");
            gitHubInfo.style.whiteSpace = WhiteSpace.Normal;
            gitHubCard.Add(gitHubInfo);

            var triggerChoices = new List<string>
            {
                ItchDeployLocale._("Push to main", "Push у main"),
                ItchDeployLocale._("Version tag", "Тег версії"),
                ItchDeployLocale._("Manual only", "Лише вручну"),
            };
            var triggerIndex = config.ci.githubTrigger switch
            {
                "tag" => 1,
                "manual" => 2,
                _ => 0,
            };
            var triggerDropdown = new ItchDeployChoiceField(
                ItchDeployLocale._("Workflow trigger", "Запуск workflow"),
                triggerChoices,
                triggerIndex)
            {
                name = "github-trigger-dropdown"
            };
            triggerDropdown.AddToClassList("settings-ci-control");
            triggerDropdown.SelectionChanged += (_, _) =>
            {
                config.ci.githubTrigger = triggerDropdown.index switch
                {
                    1 => "tag",
                    2 => "manual",
                    _ => "push_main",
                };
                ItchDeployConfig.Save(config);
                ScheduleLocaleRebuild();
            };
            gitHubCard.Add(triggerDropdown);

            var githubDeployRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._(
                    "Deploy successful builds to itch.io",
                    "Деплоїти успішні збірки на itch.io"),
                config.ci.githubDeploy,
                out var githubDeployToggle,
                "github-deploy-toggle");
            githubDeployRow.AddToClassList("settings-ci-deploy-toggle");
            githubDeployToggle.RegisterValueChangedCallback(change =>
            {
                config.ci.githubDeploy = change.newValue;
                ItchDeployConfig.Save(config);
                ScheduleLocaleRebuild();
            });
            gitHubCard.Add(githubDeployRow);

            var workflowPath = Path.Combine(
                WorkflowGenerator.WorkflowsDir,
                "build-and-deploy.yml");
            var workflowExists = File.Exists(workflowPath);
            var workflowCurrent = false;
            if (workflowExists)
            {
                try
                {
                    var expected = WorkflowGenerator.GenerateYaml(
                        CreateGitHubWorkflowOptions(config),
                        config);
                    workflowCurrent = string.Equals(
                        NormalizeText(File.ReadAllText(workflowPath)),
                        NormalizeText(expected),
                        StringComparison.Ordinal);
                }
                catch { }
            }

            var hasRepository = GitHubActionsIntegration.TryGetRepository(
                out var repositorySlug,
                out var repositoryUrl);
            var requiresAndroidSigning = config.channels != null &&
                                         config.channels.ContainsKey("Android");
            var includesIosArtifact = config.channels != null &&
                                      config.channels.ContainsKey("iOS");
            var secretsVerified = false;
            IReadOnlyList<string> missingSecrets = Array.Empty<string>();

            var githubStatusList = new VisualElement();
            githubStatusList.AddToClassList("settings-ci-status-list");
            githubStatusList.Add(BuildCiStatusRow(
                hasRepository,
                hasRepository
                    ? ItchDeployLocale._(
                        "Repository: " + repositorySlug,
                        "Репозиторій: " + repositorySlug)
                    : ItchDeployLocale._(
                        "GitHub remote is not configured.",
                        "GitHub remote не налаштовано."),
                "github-repository-status"));
            githubStatusList.Add(BuildCiStatusRow(
                workflowCurrent,
                !workflowExists
                    ? ItchDeployLocale._(
                        "Workflow has not been generated.",
                        "Workflow ще не створено.")
                    : workflowCurrent
                        ? ItchDeployLocale._(
                            "Workflow matches these settings.",
                            "Workflow відповідає цим налаштуванням.")
                        : ItchDeployLocale._(
                            "Workflow needs to be updated.",
                            "Workflow потрібно оновити."),
                "github-workflow-status"));

            var secretsReady = hasRepository && secretsVerified && missingSecrets.Count == 0;
            var secretsText = !hasRepository
                ? ItchDeployLocale._(
                    "Secrets cannot be checked without a repository.",
                    "Без репозиторію secrets перевірити неможливо.")
                : !secretsVerified
                    ? ItchDeployLocale._(
                        "Secrets not verified: install and sign in to GitHub CLI, or open repository secrets.",
                        "Secrets не перевірено: авторизуйте GitHub CLI або відкрийте secrets репозиторію.")
                    : missingSecrets.Count == 0
                        ? ItchDeployLocale._(
                            "Required repository secrets are configured.",
                            "Необхідні secrets репозиторію налаштовано.")
                        : ItchDeployLocale._(
                            "Missing secrets: " + string.Join(", ", missingSecrets),
                            "Відсутні secrets: " + string.Join(", ", missingSecrets));
            var secretsStatusRow = BuildCiStatusRow(
                secretsReady,
                secretsText,
                "github-secrets-status");
            secretsStatusRow.tooltip = ItchDeployLocale._(
                "GitHub exposes secret names only. Secret values remain hidden and are never read by ItchDeploy.",
                "GitHub показує лише назви secrets. Значення залишаються прихованими й ніколи не читаються ItchDeploy.");
            if (hasRepository)
            {
                SetCiStatusRowState(
                    secretsStatusRow,
                    ready: false,
                    checking: true,
                    text: ItchDeployLocale._(
                        "Checking required repository secret names…",
                        "Перевіряємо назви обов’язкових secrets репозиторію…"));
            }
            githubStatusList.Add(secretsStatusRow);
            if (requiresAndroidSigning)
            {
                githubStatusList.Add(BuildCiStatusRow(
                    ready: true,
                    text: ItchDeployLocale._(
                        "The Android runner enables Custom Keystore and applies the four signing secrets without changing local builds.",
                        "Android runner увімкне Custom Keystore і застосує чотири signing secrets, не змінюючи локальні збірки."),
                    name: "github-android-signing-settings-status"));
            }
            if (includesIosArtifact)
            {
                githubStatusList.Add(BuildCiStatusRow(
                    ready: true,
                    text: ItchDeployLocale._(
                        "iOS currently produces an unsigned Xcode artifact; Apple signing secrets are not used.",
                        "iOS зараз створює непідписаний Xcode artifact; secrets Apple для підпису не використовуються."),
                    name: "github-ios-signing-status"));
            }
            gitHubCard.Add(githubStatusList);

            var workflowActions = new VisualElement();
            workflowActions.AddToClassList("settings-ci-workflow-actions");
            GitHubActionsIntegration.SecretCheckResult latestSecretCheck = null;
            var secretCheckPending = hasRepository;

            Button BuildStageButton(
                Action clicked,
                string text,
                bool primary,
                string name = null)
            {
                var button = new Button(clicked)
                {
                    name = name,
                    text = text
                };
                button.AddToClassList(
                    primary ? "settings-primary-action" : "secondary-action");
                button.AddToClassList("settings-peer-action");
                return button;
            }

            void StartSecretCheck()
            {
                if (!hasRepository || secretCheckPending)
                    return;

                secretCheckPending = true;
                RebuildWorkflowActions();
                var pendingButton = workflowActions.Q<Button>("github-secrets-check");
                VerifyGitHubSecretsAsync(
                    repositorySlug,
                    config.ci.githubDeploy,
                    requiresAndroidSigning,
                    secretsStatusRow,
                    pendingButton,
                    result =>
                    {
                        latestSecretCheck = result;
                        if (result.failure ==
                                GitHubActionsIntegration.SecretCheckFailure.CliNotAuthenticated &&
                            GitHubActionsIntegration.IsCliLoginRunning)
                        {
                            PollCliLogin(90);
                            return;
                        }
                        secretCheckPending = false;
                        RebuildWorkflowActions();
                    });
            }

            void PollCliLogin(int attemptsRemaining)
            {
                if (this == null || !hasRepository)
                    return;

                secretCheckPending = true;
                RebuildWorkflowActions();
                var pendingButton = workflowActions.Q<Button>("github-secrets-check");
                VerifyGitHubSecretsAsync(
                    repositorySlug,
                    config.ci.githubDeploy,
                    requiresAndroidSigning,
                    secretsStatusRow,
                    pendingButton,
                    result =>
                    {
                        latestSecretCheck = result;
                        if (result.failure ==
                                GitHubActionsIntegration.SecretCheckFailure.CliNotAuthenticated &&
                            attemptsRemaining > 0)
                        {
                            if (!GitHubActionsIntegration.IsCliLoginRunning)
                            {
                                secretCheckPending = false;
                                SetCiStatusRowState(
                                    secretsStatusRow,
                                    ready: false,
                                    checking: false,
                                    text: ItchDeployLocale._(
                                        "GitHub sign-in was not completed. Start the browser sign-in again.",
                                        "Вхід GitHub не завершено. Запустіть вхід через браузер ще раз."));
                                RebuildWorkflowActions();
                                return;
                            }

                            SetCiStatusRowState(
                                secretsStatusRow,
                                ready: false,
                                checking: true,
                                text: string.IsNullOrWhiteSpace(
                                    GitHubActionsIntegration.CliLoginDeviceCode)
                                    ? ItchDeployLocale._(
                                        "Complete sign-in in the browser; this panel will update automatically…",
                                        "Завершіть вхід у браузері — ця панель оновиться автоматично…")
                                    : ItchDeployLocale._(
                                        "Enter code " +
                                        GitHubActionsIntegration.CliLoginDeviceCode +
                                        " in GitHub. It is already copied; this panel will update automatically…",
                                        "Введіть код " +
                                        GitHubActionsIntegration.CliLoginDeviceCode +
                                        " у GitHub. Його вже скопійовано; панель оновиться автоматично…"));
                            if (pendingButton != null)
                            {
                                pendingButton.text = ItchDeployLocale._(
                                    "Waiting for browser sign-in…",
                                    "Очікуємо завершення входу…");
                                pendingButton.SetEnabled(false);
                            }
                            workflowActions.schedule.Execute(
                                () => PollCliLogin(attemptsRemaining - 1)).StartingIn(2000);
                            return;
                        }

                        secretCheckPending = false;
                        RebuildWorkflowActions();
                    });
            }

            void OpenCliLoginWhenReady(int attemptsRemaining)
            {
                if (!GitHubActionsIntegration.IsCliLoginRunning)
                {
                    secretCheckPending = false;
                    SetCiStatusRowState(
                        secretsStatusRow,
                        ready: false,
                        checking: false,
                        text: ItchDeployLocale._(
                            "GitHub sign-in could not be prepared. Start it again.",
                            "Не вдалося підготувати вхід GitHub. Запустіть його ще раз."));
                    RebuildWorkflowActions();
                    return;
                }

                if (string.IsNullOrWhiteSpace(
                        GitHubActionsIntegration.CliLoginDeviceCode) &&
                    attemptsRemaining > 0)
                {
                    workflowActions.schedule.Execute(
                        () => OpenCliLoginWhenReady(attemptsRemaining - 1))
                        .StartingIn(250);
                    return;
                }

                Application.OpenURL(GitHubActionsIntegration.GitHubDeviceLoginUrl);
                PollCliLogin(90);
            }

            void StartCliLogin()
            {
                if (!GitHubActionsIntegration.TryStartCliLogin(out var error))
                {
                    Debug.LogWarning(
                        "[ItchDeploy] Could not start GitHub CLI sign-in: " + error);
                    SetCiStatusRowState(
                        secretsStatusRow,
                        ready: false,
                        checking: false,
                        text: ItchDeployLocale._(
                            "Could not start GitHub sign-in. Check the CLI installation and try again.",
                            "Не вдалося запустити вхід GitHub. Перевірте встановлення CLI й повторіть."));
                    return;
                }

                secretCheckPending = true;
                RebuildWorkflowActions();
                SetCiStatusRowState(
                    secretsStatusRow,
                    ready: false,
                    checking: true,
                    text: ItchDeployLocale._(
                        "Preparing secure GitHub sign-in…",
                        "Готуємо захищений вхід GitHub…"));
                var preparingButton = workflowActions.Q<Button>("github-secrets-check");
                if (preparingButton != null)
                {
                    preparingButton.text = ItchDeployLocale._(
                        "Preparing browser sign-in…",
                        "Готуємо вхід через браузер…");
                    preparingButton.SetEnabled(false);
                }
                OpenCliLoginWhenReady(40);
            }

            void RebuildWorkflowActions()
            {
                workflowActions.Clear();
                var row = new VisualElement();
                row.AddToClassList("settings-peer-actions");
                row.AddToClassList("settings-ci-workflow-action-row");
                row.AddToClassList("last");

                if (!workflowCurrent)
                {
                    row.Add(BuildStageButton(
                        () => SetupGitHubActions(config),
                        ItchDeployLocale._(
                            "Update GitHub Actions workflow",
                            "Оновити workflow GitHub Actions"),
                        primary: true,
                        name: "github-update-workflow"));
                }
                else if (!hasRepository)
                {
                    var unavailable = BuildStageButton(
                        null,
                        ItchDeployLocale._(
                            "Add a GitHub origin remote first",
                            "Спочатку додайте GitHub origin remote"),
                        primary: false,
                        name: "github-repository-action-unavailable");
                    unavailable.SetEnabled(false);
                    row.Add(unavailable);
                }
                else if (secretCheckPending &&
                         GitHubActionsIntegration.IsCliLoginRunning)
                {
                    row.Add(BuildStageButton(
                        () => Application.OpenURL(
                            GitHubActionsIntegration.GitHubDeviceLoginUrl),
                        ItchDeployLocale._(
                            "Open GitHub sign-in",
                            "Відкрити GitHub для входу"),
                        primary: true,
                        name: "github-open-device-login"));
                }
                else if (secretCheckPending)
                {
                    var checking = BuildStageButton(
                        null,
                        ItchDeployLocale._(
                            "Checking repository secrets…",
                            "Перевіряємо secrets репозиторію…"),
                        primary: true,
                        name: "github-secrets-check");
                    checking.SetEnabled(false);
                    row.Add(checking);
                }
                else if (latestSecretCheck?.failure ==
                         GitHubActionsIntegration.SecretCheckFailure.CliNotInstalled)
                {
                    row.Add(BuildStageButton(
                        () => Application.OpenURL("https://cli.github.com/"),
                        ItchDeployLocale._(
                            "Install GitHub CLI",
                            "Встановити GitHub CLI"),
                        primary: true,
                        name: "github-install-cli"));
                    row.Add(BuildStageButton(
                        StartSecretCheck,
                        ItchDeployLocale._(
                            "Check again",
                            "Перевірити знову"),
                        primary: false,
                        name: "github-secrets-check"));
                }
                else if (latestSecretCheck?.failure ==
                         GitHubActionsIntegration.SecretCheckFailure.CliNotAuthenticated)
                {
                    row.Add(BuildStageButton(
                        StartCliLogin,
                        ItchDeployLocale._(
                            "Sign in with GitHub CLI",
                            "Увійти через GitHub CLI"),
                        primary: true,
                        name: "github-auth-cli"));
                    row.Add(BuildStageButton(
                        StartSecretCheck,
                        ItchDeployLocale._(
                            "Check again",
                            "Перевірити знову"),
                        primary: false,
                        name: "github-secrets-check"));
                }
                else if (latestSecretCheck == null ||
                         !latestSecretCheck.verified ||
                         latestSecretCheck.missingSecrets.Count > 0)
                {
                    row.Add(BuildStageButton(
                        () => Application.OpenURL(
                            repositoryUrl + "/settings/secrets/actions"),
                        ItchDeployLocale._(
                            "Open repository secrets",
                            "Відкрити secrets репозиторію"),
                        primary: true,
                        name: "github-open-repository-secrets"));
                    row.Add(BuildStageButton(
                        StartSecretCheck,
                        ItchDeployLocale._(
                            "Check again",
                            "Перевірити знову"),
                        primary: false,
                        name: "github-secrets-check"));
                }
                else
                {
                    row.Add(BuildStageButton(
                        () => Application.OpenURL(repositoryUrl + "/actions"),
                        ItchDeployLocale._(
                            "Open GitHub Actions",
                            "Відкрити GitHub Actions"),
                        primary: true,
                        name: "github-open-actions"));
                }

                var buttons = row.Query<Button>().ToList();
                if (buttons.Count > 0)
                    buttons[^1].AddToClassList("last");
                workflowActions.Add(row);
            }

            RebuildWorkflowActions();
            gitHubCard.Add(workflowActions);

            if (hasRepository)
            {
                gitHubCard.schedule.Execute(() =>
                {
                    var pendingButton = workflowActions.Q<Button>("github-secrets-check");
                    VerifyGitHubSecretsAsync(
                        repositorySlug,
                        config.ci.githubDeploy,
                        requiresAndroidSigning,
                        secretsStatusRow,
                        pendingButton,
                        result =>
                        {
                            latestSecretCheck = result;
                            if (result.failure ==
                                    GitHubActionsIntegration.SecretCheckFailure.CliNotAuthenticated &&
                                GitHubActionsIntegration.IsCliLoginRunning)
                            {
                                PollCliLogin(90);
                                return;
                            }
                            secretCheckPending = false;
                            RebuildWorkflowActions();
                        });
                }).StartingIn(100);
            }

            root.Add(gitHubCard);

            return root;
        }

        private static VisualElement BuildCiStatusRow(
            bool ready,
            string text,
            string name,
            bool blocked = false)
        {
            var row = new VisualElement
            {
                name = name
            };
            row.AddToClassList("settings-inline-status");
            row.AddToClassList(
                blocked
                    ? "settings-inline-status--blocked"
                    : ready
                    ? "settings-inline-status--ready"
                    : "settings-inline-status--missing");
            var dot = new VisualElement();
            dot.AddToClassList("settings-inline-status-dot");
            var label = new Label(text);
            label.AddToClassList("settings-muted");
            label.AddToClassList("settings-inline-status-label");
            label.style.whiteSpace = WhiteSpace.Normal;
            row.Add(dot);
            row.Add(label);
            return row;
        }

        private static void SetCiStatusRowState(
            VisualElement row,
            bool ready,
            bool checking,
            string text,
            bool blocked = false)
        {
            if (row == null)
                return;

            row.RemoveFromClassList("settings-inline-status--ready");
            row.RemoveFromClassList("settings-inline-status--missing");
            row.RemoveFromClassList("settings-inline-status--checking");
            row.RemoveFromClassList("settings-inline-status--blocked");
            row.AddToClassList(
                checking
                    ? "settings-inline-status--checking"
                    : blocked
                        ? "settings-inline-status--blocked"
                    : ready
                        ? "settings-inline-status--ready"
                        : "settings-inline-status--missing");

            var label = row.Q<Label>(className: "settings-inline-status-label");
            if (label != null)
                label.text = text ?? string.Empty;
        }

        private async void VerifyGitHubSecretsAsync(
            string repositorySlug,
            bool deployToItch,
            bool requireAndroidSigning,
            VisualElement statusRow,
            Button checkButton,
            Action<GitHubActionsIntegration.SecretCheckResult> completed = null)
        {
            var generation = ++_githubSecretsCheckGeneration;
            SetCiStatusRowState(
                statusRow,
                ready: false,
                checking: true,
                text: ItchDeployLocale._(
                    "Checking required repository secret names…",
                    "Перевіряємо назви обов’язкових secrets репозиторію…"));
            if (checkButton != null)
            {
                checkButton.SetEnabled(false);
                checkButton.text = ItchDeployLocale._(
                    "Checking secrets…",
                    "Перевіряємо secrets…");
            }

            GitHubActionsIntegration.SecretCheckResult result;
            try
            {
                result = await Task.Run(() =>
                    GitHubActionsIntegration.CheckSecrets(
                        repositorySlug,
                        deployToItch,
                        requireAndroidSigning));
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[ItchDeploy] GitHub secret verification failed: " + ex.Message);
                result = new GitHubActionsIntegration.SecretCheckResult
                {
                    failure = GitHubActionsIntegration.SecretCheckFailure.QueryFailed,
                };
            }

            if (this == null || generation != _githubSecretsCheckGeneration)
                return;

            var missing = result.missingSecrets ?? Array.Empty<string>();
            var ready = result.verified && missing.Count == 0;
            var statusText = result.failure switch
            {
                GitHubActionsIntegration.SecretCheckFailure.RepositoryNotConfigured =>
                    ItchDeployLocale._(
                        "GitHub remote is not configured.",
                        "GitHub remote не налаштовано."),
                GitHubActionsIntegration.SecretCheckFailure.CliNotInstalled =>
                    ItchDeployLocale._(
                        "GitHub CLI is not installed. Install gh, then check again.",
                        "GitHub CLI не встановлено. Встановіть gh і повторіть перевірку."),
                GitHubActionsIntegration.SecretCheckFailure.CliNotAuthenticated =>
                    ItchDeployLocale._(
                        "GitHub CLI is installed but not signed in. Use the sign-in action below.",
                        "GitHub CLI встановлено, але не авторизовано. Скористайтеся кнопкою входу нижче."),
                GitHubActionsIntegration.SecretCheckFailure.QueryFailed =>
                    ItchDeployLocale._(
                        "Could not read repository secret names. Check repository access, then try again.",
                        "Не вдалося отримати назви secrets. Перевірте доступ до репозиторію й повторіть."),
                _ when missing.Count > 0 =>
                    ItchDeployLocale._(
                        "Missing secrets: " + string.Join(", ", missing),
                        "Відсутні secrets: " + string.Join(", ", missing)),
                _ =>
                    ItchDeployLocale._(
                        "All required repository secrets are configured.",
                        "Усі обов’язкові secrets репозиторію налаштовано."),
            };

            SetCiStatusRowState(statusRow, ready, checking: false, text: statusText);
            if (checkButton != null)
            {
                checkButton.text = ItchDeployLocale._(
                    "Check repository secrets",
                    "Перевірити secrets репозиторію");
                checkButton.SetEnabled(true);
            }
            completed?.Invoke(result);
        }

        private static WorkflowGenerator.Options CreateGitHubWorkflowOptions(
            ItchDeployConfig.ConfigData config)
        {
            return new WorkflowGenerator.Options
            {
                trigger = config.ci.githubTrigger switch
                {
                    "tag" => WorkflowGenerator.Trigger.OnTag,
                    "manual" => WorkflowGenerator.Trigger.Manual,
                    _ => WorkflowGenerator.Trigger.PushMain,
                },
                deploy = config.ci.githubDeploy,
                createRelease = true,
            };
        }

        private void SetupGitHubActions(ItchDeployConfig.ConfigData config)
        {
            try
            {
                config ??= ItchDeployConfig.Load();
                config.ci ??= new ItchDeployConfig.CiData();

                var workflowPath = WorkflowGenerator.Generate(
                    CreateGitHubWorkflowOptions(config));
                var repositoryFound = GitHubActionsIntegration.TryGetRepository(
                    out var repositorySlug,
                    out _);

                var butlerSecretReady = !config.ci.githubDeploy;
                var secretError = string.Empty;
                if (config.ci.githubDeploy && repositoryFound)
                {
                    butlerSecretReady = GitHubActionsIntegration.TrySetButlerSecret(
                        repositorySlug,
                        _settings.EffectiveApiKey,
                        out secretError);
                }

                _statusMessage = butlerSecretReady
                    ? ItchDeployLocale._(
                        "GitHub Actions is configured.",
                        "GitHub Actions налаштовано.")
                    : ItchDeployLocale._(
                        "Workflow updated. Complete the missing repository secrets.",
                        "Workflow оновлено. Додайте відсутні secrets репозиторію.");

                if (!repositoryFound)
                {
                    _statusMessage = ItchDeployLocale._(
                        "Workflow updated. Add a GitHub origin remote to verify secrets.",
                        "Workflow оновлено. Додайте GitHub origin, щоб перевірити secrets.");
                }
                else if (!butlerSecretReady && !string.IsNullOrWhiteSpace(secretError))
                {
                    Debug.LogWarning(
                        "[ItchDeploy] GitHub workflow generated, but BUTLER_API_KEY was not synchronized: " +
                        secretError);
                }

                Debug.Log("[ItchDeploy] GitHub Actions workflow updated: " + workflowPath);
                ScheduleLocaleRebuild();
            }
            catch (Exception ex)
            {
                _statusMessage = ItchDeployLocale._(
                    "GitHub Actions setup failed.",
                    "Не вдалося налаштувати GitHub Actions.");
                Debug.LogError("[ItchDeploy] GitHub Actions setup failed: " + ex.Message);
            }
        }
    }
}
