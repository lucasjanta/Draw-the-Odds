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
        private VisualElement BuildSettingsUnityAutomationUI()
        {
            var root = new VisualElement();
            root.name = "unity-automation-settings-panel";
            var config = ItchDeployConfig.Load();
            config.ci ??= new ItchDeployConfig.CiData();

            AddSettingsSectionTitle(root, "Unity Build Automation", string.Empty);
            var unityCard = new VisualElement();
            unityCard.AddToClassList("deploy-panel");
            unityCard.AddToClassList("settings-ci-provider-card");
            unityCard.name = "unity-build-automation-card";
            var unityInfo = new Label(config.ci.unityBuildAutomationDeploy
                ? ItchDeployLocale._(
                    "One action prepares the official package and Butler hook, creates a target for the active platform when needed, updates existing targets, synchronizes the current itch.io key, and verifies the result.",
                    "Одна дія готує офіційний пакет і хук Butler, за потреби створює ціль для активної платформи, оновлює наявні цілі, синхронізує поточний ключ itch.io та перевіряє результат.")
                : ItchDeployLocale._(
                    "One action prepares the official package and Butler hook, creates a target for the active platform when needed, and verifies its settings. Uploading cloud builds to itch.io is disabled.",
                    "Одна дія готує офіційний пакет і хук Butler, за потреби створює ціль для активної платформи та перевіряє її налаштування. Завантаження хмарних збірок на itch.io вимкнено."));
            unityInfo.AddToClassList("settings-card-help");
            unityInfo.style.whiteSpace = WhiteSpace.Normal;
            unityCard.Add(unityInfo);

            var unityDeployRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._(
                    "Deploy successful cloud builds to itch.io",
                    "Завантажувати успішні хмарні збірки на itch.io"),
                config.ci.unityBuildAutomationDeploy,
                out var unityDeployToggle,
                "unity-build-automation-deploy-toggle");
            unityDeployRow.AddToClassList("settings-ci-deploy-toggle");
            unityDeployToggle.RegisterValueChangedCallback(change =>
            {
                config.ci.unityBuildAutomationDeploy = change.newValue;
                ItchDeployConfig.Save(config);
                ScheduleLocaleRebuild();
            });
            unityCard.Add(unityDeployRow);

            var packageReady = UnityCloudBuildIntegration.IsPackageInstalled;
            var projectLinked = UnityCloudBuildIntegration.IsProjectLinked;
            var hookReady = UnityCloudBuildIntegration.IsDeployHookCurrent(
                config,
                config.ci.unityBuildAutomationDeploy);
            var hookPublished = UnityCloudBuildIntegration.IsDeployHookPublished(
                config,
                config.ci.unityBuildAutomationDeploy,
                out var hookPublishedIssue);
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            var activeCloudTargetName = PlatformChannelMap.GetDisplayName(
                activeBuildTarget);
            UnityCloudBuildIntegration.CloudBuildStartResult latestBuildStartResult =
                UnityCloudBuildIntegration.LoadLastBillingFailure(activeBuildTarget);
            var hasRememberedBillingBlock = latestBuildStartResult?.failure ==
                UnityCloudBuildIntegration.CloudBuildStartFailure.BillingUnavailable;
            var unityStatusList = new VisualElement();
            unityStatusList.AddToClassList("settings-ci-status-list");
            unityStatusList.Add(BuildCiStatusRow(
                packageReady,
                packageReady
                    ? ItchDeployLocale._(
                        "Official Build Automation package is installed.",
                        "Офіційний пакет Build Automation встановлено.")
                    : UnityCloudBuildIntegration.IsInstallInProgress
                        ? ItchDeployLocale._(
                            "Build Automation package is being installed…",
                            "Пакет Build Automation встановлюється…")
                        : ItchDeployLocale._(
                            "Build Automation package is not installed.",
                            "Пакет Build Automation не встановлено."),
                "unity-build-automation-package-status"));
            unityStatusList.Add(BuildCiStatusRow(
                projectLinked,
                projectLinked
                    ? ItchDeployLocale._(
                        "Unity Cloud project is linked.",
                        "Проєкт Unity Cloud підключено.")
                    : ItchDeployLocale._(
                        "Unity Cloud project needs to be linked during setup.",
                        "Під час налаштування потрібно підключити проєкт Unity Cloud."),
                "unity-build-automation-project-status"));
            unityStatusList.Add(BuildCiStatusRow(
                hookReady,
                hookReady
                    ? ItchDeployLocale._(
                        "Butler post-build hook matches these settings.",
                        "Post-build хук Butler відповідає поточним налаштуванням.")
                    : ItchDeployLocale._(
                        "Butler post-build hook needs to be generated or updated.",
                        "Post-build хук Butler потрібно створити або оновити."),
                "unity-build-automation-hook-status"));
            var hookPublishedStatus = BuildCiStatusRow(
                hookPublished,
                hookPublished
                    ? ItchDeployLocale._(
                        "The Butler hook is available on the tracked remote branch.",
                        "Хук Butler доступний у відстежуваній віддаленій гілці.")
                    : ItchDeployLocale._(
                        "Commit and push ci/itchdeploy-uba-post-build.sh before starting a cloud build.",
                        "Перед запуском хмарної збірки закомітьте й надішліть ci/itchdeploy-uba-post-build.sh."),
                "unity-build-automation-hook-published-status");
            hookPublishedStatus.tooltip = hookPublishedIssue;
            unityStatusList.Add(hookPublishedStatus);
            var cloudReadinessStatus = BuildCiStatusRow(
                ready: false,
                text: hasRememberedBillingBlock
                    ? ItchDeployLocale._(
                        "Build Automation requires activation for this Unity organization. Open Unity Cloud and enable a consumption plan or raise the build-minutes cap; this panel will recheck automatically.",
                        "Для цієї організації Unity потрібно активувати Build Automation. У Unity Cloud увімкніть тариф за споживанням або збільште ліміт build minutes — панель перевірить стан автоматично.")
                    : ItchDeployLocale._(
                        "Cloud targets have not been checked yet.",
                        "Хмарні цілі ще не перевірено."),
                name: "unity-build-automation-cloud-readiness-status",
                blocked: hasRememberedBillingBlock);
            cloudReadinessStatus.tooltip = ItchDeployLocale._(
                "ItchDeploy checks target settings and the BUTLER_API_KEY name through Unity's authenticated Build Automation API. Secret values are never shown in this window or written to logs.",
                "ItchDeploy перевіряє налаштування хмарних цілей і назву BUTLER_API_KEY через авторизований API Build Automation. Значення секрету не показується у вікні й не записується в логи.");
            unityStatusList.Add(cloudReadinessStatus);
            unityCard.Add(unityStatusList);

            var unityActions = new VisualElement();
            unityActions.AddToClassList("settings-ci-unity-actions");
            var unityStageActions = new VisualElement();
            unityStageActions.AddToClassList("settings-peer-actions");
            unityStageActions.AddToClassList("settings-ci-unity-action-row");
            unityStageActions.AddToClassList("last");
            UnityCloudBuildIntegration.CloudSetupResult latestCloudResult = null;
            UnityCloudBuildIntegration.HookPublishResult latestHookPublishResult = null;
            var cloudCheckPending = false;
            var cloudBuildStartPending = false;
            var hookPublishPending = false;
            var setupAndVerify = new Button
            {
                name = "unity-build-automation-setup-and-verify"
            };
            setupAndVerify.AddToClassList("settings-primary-action");
            setupAndVerify.AddToClassList("settings-peer-action");
            setupAndVerify.AddToClassList("last");

            string CloudStatusText(UnityCloudBuildIntegration.CloudSetupResult result)
            {
                if (result == null)
                    return ItchDeployLocale._(
                        "Cloud targets have not been checked yet.",
                        "Хмарні цілі ще не перевірено.");

                return result.failure switch
                {
                    UnityCloudBuildIntegration.CloudSetupFailure.None =>
                        config.ci.unityBuildAutomationDeploy
                            ? ItchDeployLocale._(
                                $"Setup complete. {result.targetCount} cloud target(s) are ready for builds and Butler deployment.",
                                $"Налаштування завершено. Хмарних цілей готово: {result.targetCount}. Можна запускати збірку й деплой через Butler.")
                            : ItchDeployLocale._(
                                $"Setup complete. {result.targetCount} cloud target(s) are ready for builds.",
                                $"Налаштування завершено. Хмарних цілей готово: {result.targetCount}. Можна запускати хмарну збірку."),
                    UnityCloudBuildIntegration.CloudSetupFailure.NoBuildTargets =>
                        ItchDeployLocale._(
                            $"No cloud target exists for {activeCloudTargetName}. The setup action will create and configure it.",
                            $"Для {activeCloudTargetName} ще немає хмарної цілі. Дія налаштування створить і налаштує її."),
                    UnityCloudBuildIntegration.CloudSetupFailure.SourceControlMissing =>
                        ItchDeployLocale._(
                            "Connect this project repository in Unity Cloud before creating its first build target.",
                            "Перед створенням першої хмарної цілі підключіть репозиторій проєкту в Unity Cloud."),
                    UnityCloudBuildIntegration.CloudSetupFailure.BillingUnavailable =>
                        ItchDeployLocale._(
                            $"Build Automation cannot run builds on the current Unity plan ({result.billingPlan}). Open Unity Cloud, enable a consumption plan or raise the build-minutes cap. This panel will recheck automatically.",
                            $"Build Automation не може запускати збірки на поточному тарифі Unity ({result.billingPlan}). У Unity Cloud увімкніть тариф за споживанням або збільште ліміт build minutes — панель перевірить стан автоматично."),
                    UnityCloudBuildIntegration.CloudSetupFailure.UnsupportedPlatform =>
                        ItchDeployLocale._(
                            "The active platform is not supported by Unity Build Automation: " + result.error,
                            "Unity Build Automation не підтримує активну платформу: " + result.error),
                    UnityCloudBuildIntegration.CloudSetupFailure.TargetCreationFailed =>
                        ItchDeployLocale._(
                            "The cloud target could not be created: " + result.error,
                            "Не вдалося створити хмарну ціль: " + result.error),
                    UnityCloudBuildIntegration.CloudSetupFailure.UnitySessionMissing =>
                        ItchDeployLocale._(
                            "The Unity session cannot access Build Automation. Reconnect the project in Project Settings.",
                            "Сесія Unity не має доступу до Build Automation. Повторно підключіть проєкт у Project Settings."),
                    UnityCloudBuildIntegration.CloudSetupFailure.LocalButlerKeyMissing =>
                        ItchDeployLocale._(
                            "The cloud target needs BUTLER_API_KEY, but itch.io authorization is not available locally.",
                            "Хмарна ціль потребує BUTLER_API_KEY, але локальна авторизація itch.io недоступна."),
                    UnityCloudBuildIntegration.CloudSetupFailure.RemoteHookMissing =>
                        ItchDeployLocale._(
                            $"Butler hook is missing on {result.targetCount - result.hookReadyCount} cloud target(s). Run setup to apply it.",
                            $"Хук Butler відсутній на хмарних цілях: {result.targetCount - result.hookReadyCount}. Запустіть налаштування."),
                    UnityCloudBuildIntegration.CloudSetupFailure.RemoteSecretMissing =>
                        ItchDeployLocale._(
                            $"BUTLER_API_KEY is missing on {result.targetCount - result.secretReadyCount} cloud target(s). Run setup to synchronize it.",
                            $"BUTLER_API_KEY відсутній на хмарних цілях: {result.targetCount - result.secretReadyCount}. Запустіть синхронізацію."),
                    UnityCloudBuildIntegration.CloudSetupFailure.RequestFailed =>
                        ItchDeployLocale._(
                            "Build Automation could not be checked: " + result.error,
                            "Не вдалося перевірити Build Automation: " + result.error),
                    _ => ItchDeployLocale._(
                        "Complete the local Unity Build Automation setup first.",
                        "Спочатку завершіть локальне налаштування Unity Build Automation."),
                };
            }

            void RefreshActionState()
            {
                setupAndVerify.SetEnabled(
                    !cloudCheckPending &&
                    !cloudBuildStartPending &&
                    !hookPublishPending &&
                    !UnityCloudBuildIntegration.IsInstallInProgress);

                if (UnityCloudBuildIntegration.IsInstallInProgress)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        "Installing Unity Build Automation…",
                        "Встановлюємо Unity Build Automation…");
                }
                else if (!packageReady)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        "Install Unity Build Automation",
                        "Встановити Unity Build Automation");
                }
                else if (!projectLinked)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        "Connect Unity Cloud Project",
                        "Підключити Unity Cloud Project");
                }
                else if (cloudCheckPending)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        "Checking and configuring cloud targets…",
                        "Перевіряємо й налаштовуємо cloud targets…");
                }
                else if (cloudBuildStartPending)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        "Starting cloud build…",
                        "Запускаємо хмарну збірку…");
                }
                else if (hookPublishPending)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        "Publishing the Butler hook to Git…",
                        "Публікуємо хук Butler у Git…");
                }
                else if (latestBuildStartResult?.started == true)
                {
                    var buildLabel = latestBuildStartResult.buildNumber > 0
                        ? " #" + latestBuildStartResult.buildNumber
                        : string.Empty;
                    setupAndVerify.text = ItchDeployLocale._(
                        "Open cloud build" + buildLabel,
                        "Відкрити перебіг збірки" + buildLabel);
                }
                else if (latestCloudResult?.failure ==
                         UnityCloudBuildIntegration.CloudSetupFailure.BillingUnavailable)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        "Open Build Automation activation",
                        "Відкрити активацію Build Automation");
                }
                else if (latestBuildStartResult?.failure ==
                         UnityCloudBuildIntegration.CloudBuildStartFailure.BillingUnavailable)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        $"Retry cloud build after billing update — {activeCloudTargetName}",
                        $"Повторити запуск після зміни billing — {activeCloudTargetName}");
                }
                else if (latestHookPublishResult != null &&
                         !latestHookPublishResult.published)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        "Open the exact hook location on GitHub",
                        "Відкрити точне місце хука на GitHub");
                }
                else if (latestCloudResult?.failure ==
                         UnityCloudBuildIntegration.CloudSetupFailure.NoBuildTargets)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        $"Create and configure {activeCloudTargetName} target",
                        $"Створити й налаштувати ціль {activeCloudTargetName}");
                }
                else if (latestCloudResult?.failure ==
                         UnityCloudBuildIntegration.CloudSetupFailure.SourceControlMissing)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        "Connect repository in Unity Cloud",
                        "Підключити репозиторій у Unity Cloud");
                }
                else if (latestCloudResult?.failure ==
                         UnityCloudBuildIntegration.CloudSetupFailure.TargetCreationFailed)
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        $"Retry creating {activeCloudTargetName} target",
                        $"Повторити створення цілі {activeCloudTargetName}");
                }
                else if (latestCloudResult?.verified == true)
                {
                    if (!hookReady)
                    {
                        setupAndVerify.text = ItchDeployLocale._(
                            "Generate or update the Butler hook",
                            "Створити або оновити хук Butler");
                    }
                    else if (!hookPublished)
                    {
                        setupAndVerify.text = ItchDeployLocale._(
                            "Publish the Butler hook to Git",
                            "Опублікувати хук Butler у Git");
                    }
                    else
                    {
                        setupAndVerify.text = config.ci.unityBuildAutomationDeploy
                            ? ItchDeployLocale._(
                                $"Start cloud build and deploy — {activeCloudTargetName}",
                                $"Запустити хмарну збірку й деплой — {activeCloudTargetName}")
                            : ItchDeployLocale._(
                                $"Start cloud build — {activeCloudTargetName}",
                                $"Запустити хмарну збірку — {activeCloudTargetName}");
                    }
                }
                else
                {
                    setupAndVerify.text = ItchDeployLocale._(
                        "Set up and verify Unity Build Automation",
                        "Налаштувати й перевірити Unity Build Automation");
                }
            }

            string CloudBuildStartStatusText(
                UnityCloudBuildIntegration.CloudBuildStartResult result)
            {
                if (result?.started == true)
                {
                    var buildLabel = result.buildNumber > 0
                        ? " #" + result.buildNumber
                        : string.Empty;
                    var status = result.buildStatus?.ToLowerInvariant() switch
                    {
                        "queued" => ItchDeployLocale._("queued", "у черзі"),
                        "started" => ItchDeployLocale._("running", "виконується"),
                        "senttobuilder" => ItchDeployLocale._("sent to builder", "передано на білдер"),
                        _ => string.IsNullOrWhiteSpace(result.buildStatus)
                            ? ItchDeployLocale._("started", "запущено")
                            : result.buildStatus,
                    };
                    return config.ci.unityBuildAutomationDeploy
                        ? ItchDeployLocale._(
                            $"Cloud build{buildLabel} is {status}. Butler will deploy it to itch.io after a successful build.",
                            $"Хмарну збірку{buildLabel} запущено (стан: {status}). Після успішної збірки Butler завантажить її на itch.io.")
                        : ItchDeployLocale._(
                            $"Cloud build{buildLabel} is {status}. Open Unity Cloud to follow its progress.",
                            $"Хмарну збірку{buildLabel} запущено (стан: {status}). Перебіг доступний у Unity Cloud.");
                }

                return result?.failure switch
                {
                    UnityCloudBuildIntegration.CloudBuildStartFailure.TargetMissing =>
                        ItchDeployLocale._(
                            "No ready cloud target exists for the active platform. Run setup again.",
                            "Для активної платформи немає готової хмарної цілі. Повторіть налаштування."),
                    UnityCloudBuildIntegration.CloudBuildStartFailure.UnsupportedPlatform =>
                        ItchDeployLocale._(
                            "The active platform is not supported: " + result.error,
                            "Активна платформа не підтримується: " + result.error),
                    UnityCloudBuildIntegration.CloudBuildStartFailure.UnitySessionMissing =>
                        ItchDeployLocale._(
                            "Unity authorization expired. Reconnect the project in Project Settings.",
                            "Авторизація Unity завершилась. Повторно підключіть проєкт у Project Settings."),
                    UnityCloudBuildIntegration.CloudBuildStartFailure.BillingUnavailable =>
                        ItchDeployLocale._(
                            "Cloud builds are disabled for this Unity organization. Enable Build Automation billing or raise the build-minutes limit in Unity Cloud, then try again.",
                            "Хмарні збірки вимкнено для цієї організації Unity. У Unity Cloud увімкніть оплату Build Automation або збільште ліміт build minutes, а потім повторіть запуск."),
                    _ => ItchDeployLocale._(
                        "The cloud build could not be started: " + result?.error,
                        "Не вдалося запустити хмарну збірку: " + result?.error),
                };
            }

            string HookPublishStatusText(
                UnityCloudBuildIntegration.HookPublishResult result)
            {
                if (result?.published == true)
                {
                    return result.committed
                        ? ItchDeployLocale._(
                            $"The Butler hook was committed and pushed to {result.branch}.",
                            $"Хук Butler закомічено й надіслано в гілку {result.branch}.")
                        : ItchDeployLocale._(
                            $"The current Butler hook is available in {result.branch}.",
                            $"Поточний хук Butler доступний у гілці {result.branch}.");
                }

                return result?.failure switch
                {
                    UnityCloudBuildIntegration.HookPublishFailure.UnpushedCommitsPresent =>
                        ItchDeployLocale._(
                            "ItchDeploy did not push unrelated commits. Open the exact GitHub location or publish the other local commits first.",
                            "ItchDeploy не надсилав сторонні коміти. Відкрийте точне місце на GitHub або спочатку опублікуйте інші локальні коміти."),
                    UnityCloudBuildIntegration.HookPublishFailure.DetachedHead =>
                        ItchDeployLocale._(
                            "Check out a Git branch, then publish the Butler hook again.",
                            "Перейдіть на Git-гілку, а потім повторіть публікацію хука Butler."),
                    _ => ItchDeployLocale._(
                        "The hook could not be published automatically: " + result?.error,
                        "Не вдалося автоматично опублікувати хук: " + result?.error),
                };
            }

            void OpenHookRepositoryLocation()
            {
                var repositoryUrl = latestHookPublishResult?.repositoryUrl;
                var branch = latestHookPublishResult?.branch;
                if (string.IsNullOrWhiteSpace(repositoryUrl))
                    GitHubActionsIntegration.TryGetRepository(out _, out repositoryUrl);

                var targetUrl = UnityCloudBuildIntegration.BuildHookRepositoryUrl(
                    repositoryUrl,
                    branch);
                if (!string.IsNullOrWhiteSpace(targetUrl))
                    Application.OpenURL(targetUrl);
                else
                    EditorUtility.RevealInFinder(
                        UnityCloudBuildIntegration.HookAbsolutePath);
            }

            async void PublishHook()
            {
                if (hookPublishPending)
                    return;

                hookPublishPending = true;
                latestHookPublishResult = null;
                SetCiStatusRowState(
                    hookPublishedStatus,
                    ready: false,
                    checking: true,
                    text: ItchDeployLocale._(
                        "Committing and pushing only the Butler hook…",
                        "Комітимо й надсилаємо лише хук Butler…"));
                RefreshActionState();

                UnityCloudBuildIntegration.HookPublishResult result;
                try
                {
                    result = await UnityCloudBuildIntegration.PublishDeployHookAsync(
                        config,
                        config.ci.unityBuildAutomationDeploy);
                }
                catch (Exception ex)
                {
                    result = new UnityCloudBuildIntegration.HookPublishResult
                    {
                        failure = UnityCloudBuildIntegration.HookPublishFailure.PushFailed,
                        error = ex.Message,
                    };
                }

                if (this == null)
                    return;

                hookPublishPending = false;
                latestHookPublishResult = result;
                SetCiStatusRowState(
                    hookPublishedStatus,
                    ready: result.published,
                    checking: false,
                    text: HookPublishStatusText(result));
                hookPublishedStatus.tooltip = result.error;
                RefreshActionState();

                if (result.published)
                    unityCard.schedule.Execute(ScheduleLocaleRebuild).StartingIn(100);
            }

            async void StartCloudBuild()
            {
                if (cloudBuildStartPending)
                    return;

                if (!UnityCloudBuildIntegration.IsDeployHookPublished(
                        config,
                        config.ci.unityBuildAutomationDeploy,
                        out var publicationIssue))
                {
                    SetCiStatusRowState(
                        hookPublishedStatus,
                        ready: false,
                        checking: false,
                        text: ItchDeployLocale._(
                            "The cloud build was not started. Commit and push the current Butler hook first.",
                            "Хмарну збірку не запущено. Спочатку закомітьте й надішліть поточний хук Butler."));
                    hookPublishedStatus.tooltip = publicationIssue;
                    RefreshActionState();
                    return;
                }

                var generation = ++_unityAutomationCheckGeneration;
                latestBuildStartResult = null;
                cloudBuildStartPending = true;
                SetCiStatusRowState(
                    cloudReadinessStatus,
                    ready: false,
                    checking: true,
                    text: ItchDeployLocale._(
                        $"Starting a cloud build for {activeCloudTargetName}…",
                        $"Запускаємо хмарну збірку для {activeCloudTargetName}…"));
                RefreshActionState();

                var result = await UnityCloudBuildIntegration.StartCloudBuildAsync(
                    activeBuildTarget);
                if (this == null || generation != _unityAutomationCheckGeneration)
                    return;

                cloudBuildStartPending = false;
                latestBuildStartResult = result;
                UnityCloudBuildIntegration.RememberBuildStartResult(
                    activeBuildTarget,
                    result);
                var billingBlocked = result.failure ==
                    UnityCloudBuildIntegration.CloudBuildStartFailure.BillingUnavailable;
                SetCiStatusRowState(
                    cloudReadinessStatus,
                    ready: result.started,
                    checking: false,
                    text: CloudBuildStartStatusText(result),
                    blocked: billingBlocked);
                RefreshActionState();

                if (result.failure ==
                    UnityCloudBuildIntegration.CloudBuildStartFailure.TargetMissing)
                {
                    latestCloudResult = new UnityCloudBuildIntegration.CloudSetupResult
                    {
                        failure = UnityCloudBuildIntegration.CloudSetupFailure.NoBuildTargets,
                    };
                }
                else if (result.failure ==
                         UnityCloudBuildIntegration.CloudBuildStartFailure.UnitySessionMissing)
                {
                    UnityCloudBuildIntegration.OpenProjectSettings();
                }
            }

            async void RunCloudCheck(bool configure)
            {
                if (cloudCheckPending)
                    return;

                var generation = ++_unityAutomationCheckGeneration;
                cloudCheckPending = true;
                SetCiStatusRowState(
                    cloudReadinessStatus,
                    ready: false,
                    checking: true,
                    text: configure
                        ? ItchDeployLocale._(
                            "Configuring and verifying cloud targets…",
                            "Налаштовуємо й перевіряємо хмарні цілі…")
                        : ItchDeployLocale._(
                            "Checking cloud targets…",
                            "Перевіряємо хмарні цілі…"));
                RefreshActionState();

                UnityCloudBuildIntegration.CloudSetupResult result;
                try
                {
                    result = configure
                        ? await UnityCloudBuildIntegration.ConfigureAndVerifyCloudAsync(
                            config,
                            config.ci.unityBuildAutomationDeploy)
                        : await UnityCloudBuildIntegration.CheckCloudReadinessAsync(
                            config.ci.unityBuildAutomationDeploy);
                }
                catch (Exception ex)
                {
                    result = new UnityCloudBuildIntegration.CloudSetupResult
                    {
                        failure = UnityCloudBuildIntegration.CloudSetupFailure.RequestFailed,
                        error = ex.Message,
                    };
                }

                if (this == null || generation != _unityAutomationCheckGeneration)
                    return;

                latestCloudResult = result;
                cloudCheckPending = false;
                var billingBlocked = result.failure ==
                        UnityCloudBuildIntegration.CloudSetupFailure.BillingUnavailable ||
                    latestBuildStartResult?.failure ==
                        UnityCloudBuildIntegration.CloudBuildStartFailure.BillingUnavailable;
                SetCiStatusRowState(
                    cloudReadinessStatus,
                    ready: result.verified && !billingBlocked,
                    checking: false,
                    text: billingBlocked && result.failure !=
                            UnityCloudBuildIntegration.CloudSetupFailure.BillingUnavailable
                        ? CloudBuildStartStatusText(latestBuildStartResult)
                        : CloudStatusText(result),
                    blocked: billingBlocked);
                RefreshActionState();

                if (configure && result.failure ==
                         UnityCloudBuildIntegration.CloudSetupFailure.UnitySessionMissing)
                {
                    UnityCloudBuildIntegration.OpenProjectSettings();
                }
                else if (configure && result.failure ==
                         UnityCloudBuildIntegration.CloudSetupFailure.SourceControlMissing)
                {
                    UnityCloudBuildIntegration.OpenDashboard();
                }

                if (configure && (!hookReady || result.changed))
                {
                    unityCard.schedule.Execute(ScheduleLocaleRebuild).StartingIn(100);
                }
            }

            setupAndVerify.clicked += () =>
            {
                if (latestBuildStartResult?.started == true)
                {
                    UnityCloudBuildIntegration.OpenDashboard();
                    return;
                }

                if (latestCloudResult?.failure ==
                    UnityCloudBuildIntegration.CloudSetupFailure.BillingUnavailable)
                {
                    UnityCloudBuildIntegration.OpenDashboard();
                    return;
                }

                if (latestBuildStartResult?.failure ==
                    UnityCloudBuildIntegration.CloudBuildStartFailure.BillingUnavailable)
                {
                    StartCloudBuild();
                    return;
                }

                if (latestHookPublishResult != null &&
                    !latestHookPublishResult.published)
                {
                    OpenHookRepositoryLocation();
                    latestHookPublishResult = null;
                    RefreshActionState();
                    return;
                }

                if (!packageReady)
                {
                    try
                    {
                        _statusMessage = UnityCloudBuildIntegration.PrepareProject(
                            config,
                            config.ci.unityBuildAutomationDeploy);
                        RefreshActionState();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            "[ItchDeploy] Unity Build Automation setup failed: " +
                            ex.Message);
                    }
                    return;
                }

                if (!projectLinked)
                {
                    UnityCloudBuildIntegration.OpenProjectSettings();
                    return;
                }

                if (latestCloudResult?.verified == true)
                {
                    if (!hookReady)
                    {
                        RunCloudCheck(configure: true);
                        return;
                    }

                    if (!hookPublished)
                    {
                        PublishHook();
                        return;
                    }

                    StartCloudBuild();
                    return;
                }

                RunCloudCheck(configure: true);
            };
            RefreshActionState();
            unityStageActions.Add(setupAndVerify);

            unityActions.Add(unityStageActions);
            unityCard.Add(unityActions);
            root.Add(unityCard);

            if (packageReady && projectLinked)
            {
                unityCard.schedule.Execute(
                    () => RunCloudCheck(configure: false)).StartingIn(100);
            }
            unityCard.schedule.Execute(() =>
            {
                if (this == null || unityCard.panel == null)
                    return;

                if (packageReady != UnityCloudBuildIntegration.IsPackageInstalled ||
                    projectLinked != UnityCloudBuildIntegration.IsProjectLinked ||
                    hookReady != UnityCloudBuildIntegration.IsDeployHookCurrent(
                        config,
                        config.ci.unityBuildAutomationDeploy) ||
                    hookPublished != UnityCloudBuildIntegration.IsDeployHookPublished(
                        config,
                        config.ci.unityBuildAutomationDeploy,
                        out _))
                {
                    ScheduleLocaleRebuild();
                }
            }).Every(1500);
            unityCard.schedule.Execute(() =>
            {
                if (this == null || unityCard.panel == null || cloudCheckPending)
                    return;

                if (latestCloudResult?.failure ==
                        UnityCloudBuildIntegration.CloudSetupFailure.SourceControlMissing ||
                    latestCloudResult?.failure ==
                        UnityCloudBuildIntegration.CloudSetupFailure.BillingUnavailable)
                {
                    RunCloudCheck(configure: false);
                }
            }).Every(7000);

            return root;
        }
    }
}
