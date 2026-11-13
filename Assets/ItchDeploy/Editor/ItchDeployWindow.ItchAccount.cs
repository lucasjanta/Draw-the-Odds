using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ItchDeploy.Editor
{
    public partial class ItchDeployWindow
    {
        private VisualElement BuildSettingsAccountUI()
        {
            var root = new VisualElement();

            AddSettingsSectionTitle(
                root,
                ItchDeployLocale._("itch.io account", "Акаунт itch.io"),
                "\u2692");

            var accountCard = new VisualElement();
            accountCard.AddToClassList("deploy-panel");
            accountCard.AddToClassList("settings-account-card");

            _settingsAccountState = CreateItchAccountState(
                "itch-account-state",
                out _settingsAccountTitle,
                out _settingsAccountCopy);
            accountCard.Add(_settingsAccountState);

            _settingsAccountConnectButton = new Button(ConnectItchAccount);
            _settingsAccountConnectButton.name = "itch-account-connect";
            _settingsAccountConnectButton.AddToClassList("settings-primary-action");
            _settingsAccountConnectButton.tooltip = ItchDeployLocale._(
                "Open itch.io in the browser through Butler. Account data and credentials are not edited here.",
                "Відкриває itch.io у браузері через Butler. Дані акаунта й облікові дані тут не редагуються.");
            accountCard.Add(_settingsAccountConnectButton);
            root.Add(accountCard);

            AddSettingsSpacer(root);
            AddSettingsSectionTitle(
                root,
                ItchDeployLocale._(
                    "itch.io project",
                    "Проєкт itch.io"),
                "\u2605");

            var projectCard = new VisualElement();
            projectCard.AddToClassList("deploy-panel");
            projectCard.AddToClassList("settings-project-card");

            var projectTitle = new Label(ItchDeployLocale._(
                "Deployment target",
                "Ціль деплою"));
            projectTitle.AddToClassList("settings-card-title");
            projectCard.Add(projectTitle);

            var projectHelp = new Label(ItchDeployLocale._(
                "Select a project returned by itch.io for the connected account.",
                "Виберіть проєкт, до якого підключений акаунт має доступ на itch.io."));
            projectHelp.AddToClassList("settings-card-help");
            projectCard.Add(projectHelp);

            _settingsProjectDropdown = new ItchDeployChoiceField(
                ItchDeployLocale._("Project", "Проєкт"),
                BuildItchProjectChoices(),
                -1)
            {
                name = "itch-project-dropdown",
                placeholder = ItchDeployLocale._(
                    "Select a project",
                    "Виберіть проєкт")
            };
            _settingsProjectDropdown.AddToClassList("settings-project-dropdown");
            _settingsProjectDropdown.SelectionChanged += (index, _) =>
                SelectItchProject(index);
            projectCard.Add(_settingsProjectDropdown);

            var projectActions = new VisualElement();
            projectActions.AddToClassList("settings-action-row");
            projectActions.AddToClassList("settings-button-pair");

            _settingsProjectsRefreshButton = new Button(() =>
            {
                _settingsItchDataLoaded = false;
                RefreshItchAccountData();
            })
            {
                name = "itch-projects-refresh",
                text = ItchDeployLocale._("Refresh projects", "Оновити список")
            };
            _settingsProjectsRefreshButton.tooltip = ItchDeployLocale._(
                "Ask itch.io again which games this account can publish to.",
                "Перезапитати в itch.io, до яких ігор має доступ цей акаунт.");
            _settingsProjectsRefreshButton.AddToClassList("secondary-action");
            projectActions.Add(_settingsProjectsRefreshButton);

            var createProject = new Button(() =>
                Application.OpenURL("https://itch.io/game/new"))
            {
                name = "itch-project-create",
                text = ItchDeployLocale._(
                    "Create project on itch.io",
                    "Створити проєкт на itch.io")
            };
            createProject.tooltip = ItchDeployLocale._(
                "itch.io does not expose a documented project-creation API. Create the page on itch.io, then refresh this list.",
                "itch.io не має документованого API створення проєктів. Створіть сторінку на itch.io, а потім оновіть цей список.");
            createProject.AddToClassList("secondary-action");
            createProject.AddToClassList("last");
            projectActions.Add(createProject);
            projectCard.Add(projectActions);
            root.Add(projectCard);

            _itchPageConnectionCard = BuildItchPageConnectionCard();
            root.Add(_itchPageConnectionCard);

            RefreshItchAccountControls();
            RefreshItchPageConnectionCard();

            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (!ItchIOAPI.HasAuth ||
                    _settingsItchDataLoaded ||
                    _itchAccountRefreshPending)
                    return;

                root.schedule.Execute(() =>
                {
                    if (this == null || root.panel == null)
                        return;
                    RefreshItchAccountData();
                }).StartingIn(50);
            });

            return root;
        }

        private static VisualElement CreateItchAccountState(
            string name,
            out Label title,
            out Label copy)
        {
            var state = new VisualElement { name = name };
            state.AddToClassList("settings-account-state");

            var icon = new VisualElement();
            icon.AddToClassList("settings-account-state-icon");
            var iconCore = new VisualElement();
            iconCore.AddToClassList("settings-account-state-icon-core");
            icon.Add(iconCore);

            var body = new VisualElement();
            body.AddToClassList("settings-account-state-body");
            title = new Label();
            title.AddToClassList("settings-account-state-title");
            copy = new Label();
            copy.AddToClassList("settings-account-state-copy");
            copy.style.whiteSpace = WhiteSpace.Normal;
            body.Add(title);
            body.Add(copy);

            state.Add(icon);
            state.Add(body);
            return state;
        }

        private List<string> BuildItchProjectChoices()
        {
            return BuildItchProjectLabels(_settingsItchGames);
        }

        internal static List<string> BuildItchProjectLabels(
            IReadOnlyList<ItchIOAPI.GameInfo> games)
        {
            if (games == null || games.Count == 0)
                return new List<string>();

            var titles = games
                .Select(GetItchProjectTitle)
                .ToList();
            var titleCounts = titles
                .GroupBy(title => title, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.OrdinalIgnoreCase);
            var labels = new List<string>(games.Count);

            for (var index = 0; index < games.Count; index++)
            {
                var title = titles[index];
                var targetParts = GetItchTargetFromUrl(games[index]?.url)
                    .Split('/');
                var slug = targetParts.Length == 2 ? targetParts[1] : string.Empty;
                labels.Add(titleCounts[title] > 1 && !string.IsNullOrWhiteSpace(slug)
                    ? $"{title}  ·  {slug}"
                    : title);
            }

            return labels;
        }

        private static string GetItchProjectTitle(ItchIOAPI.GameInfo game)
        {
            if (!string.IsNullOrWhiteSpace(game?.title))
                return game.title.Trim();

            var targetParts = GetItchTargetFromUrl(game?.url).Split('/');
            return targetParts.Length == 2
                ? targetParts[1]
                : ItchDeployLocale._("Untitled project", "Проєкт без назви");
        }

        private static string GetItchTargetFromUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return string.Empty;

            const string itchHostSuffix = ".itch.io";
            var host = uri.Host;
            if (!host.EndsWith(itchHostSuffix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var user = host.Substring(0, host.Length - itchHostSuffix.Length);
            var slug = uri.AbsolutePath.Trim('/').Split('/').LastOrDefault();
            return string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(slug)
                ? string.Empty
                : $"{user}/{slug}";
        }

        internal static bool TryResolveItchProjectIndex(
            IReadOnlyList<ItchIOAPI.GameInfo> games,
            int index,
            out string user,
            out string slug)
        {
            user = string.Empty;
            slug = string.Empty;
            var game = games != null && index >= 0 && index < games.Count
                ? games[index]
                : null;
            if (game == null)
                return false;

            var target = GetItchTargetFromUrl(game.url);
            var targetParts = target.Split('/');
            if (targetParts.Length != 2)
                return false;

            user = targetParts[0];
            slug = targetParts[1];
            return !string.IsNullOrWhiteSpace(user) &&
                   !string.IsNullOrWhiteSpace(slug);
        }

        private void SelectItchProject(int index)
        {
            if (!TryResolveItchProjectIndex(
                    _settingsItchGames,
                    index,
                    out var user,
                    out var slug))
                return;

            _settings.itchUserName = user;
            _settings.itchGameSlug = slug;
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            ItchDeployConfig.WriteFromSettings(_settings);

            _itchDeployAccessValidated = false;
            _itchValidatedDeployTarget = string.Empty;
            _itchConnectionMessage = string.Empty;
            _settingsProjectDropdown?.SetIndexWithoutNotify(index);
            RefreshItchPageConnectionCard();
            RefreshDashboard();
            RefreshPublishUI();
            RefreshItchAccountData();
        }

        private async void RefreshItchAccountData(Action<bool> completed = null)
        {
            if (_itchAccountRefreshPending)
                return;

            _itchAccountRefreshPending = true;
            _settingsItchDataLoaded = false;
            _settingsItchProfile = null;
            _settingsItchGames.Clear();
            _itchDeployAccessValidated = false;
            _itchValidatedDeployTarget = string.Empty;
            _itchConnectionMessage = string.Empty;
            RefreshItchAccountControls();

            ItchAccountSnapshot snapshot = null;
            if (ItchIOAPI.HasAuth)
            {
                var apiKey = _settings.EffectiveApiKey;

                var profileApiKey = ItchDeployCredentials.ResolveProfileApiKey();
                var deployTarget = HasConfiguredItchPage()
                    ? $"{_settings.itchUserName}/{_settings.itchGameSlug}:" +
                      ResolveChannel(EditorUserBuildSettings.activeBuildTarget)
                    : string.Empty;
                try
                {
                    snapshot = await Task.Run(() =>
                    {
                        var result = new ItchAccountSnapshot
                        {
                            profile = ItchIOAPI.GetProfile(profileApiKey)
                        };
                        if (result.profile != null)
                            result.games = ItchIOAPI.GetMyGames(profileApiKey);
                        if (!string.IsNullOrWhiteSpace(deployTarget) &&
                            ButlerBridge.IsButlerAvailable)
                        {
                            result.deployTarget = deployTarget;
                            result.deployAccessValidated =
                                ButlerBridge.IsSuccessfulResult(
                                    ButlerBridge.Status(deployTarget, apiKey));
                        }
                        return result;
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ItchDeploy] itch.io connection check failed: {ex.Message}");
                }
            }

            if (this == null)
                return;

            _settingsItchProfile = snapshot?.profile;
            _settingsItchGames = snapshot?.games ?? new List<ItchIOAPI.GameInfo>();
            _itchDeployAccessValidated = snapshot?.deployAccessValidated == true;
            _itchValidatedDeployTarget = snapshot?.deployTarget ?? string.Empty;
            _settingsItchDataLoaded = true;
            _itchAccountRefreshPending = false;
            _itchConnectionMessage = !_itchDeployAccessValidated && ItchIOAPI.HasAuth
                ? ItchDeployLocale._(
                    "Butler could not verify access to the current deployment target.",
                    "Butler не зміг перевірити доступ до поточної цілі деплою.")
                : string.Empty;

            RefreshItchAccountControls();
            RefreshItchPageConnectionCard();
            RefreshDashboard();
            RefreshPublishUI();
            completed?.Invoke(_settingsItchProfile != null);
        }

        private void RefreshItchAccountControls()
        {
            var hasValidatedAccount = _settingsItchProfile != null;
            var hasStoredAuthorization = ItchIOAPI.HasAuth;
            var showConnected = hasValidatedAccount;
            var isPending = _itchLoginPending || _itchAccountRefreshPending;

            if (_settingsAccountState != null)
            {
                _settingsAccountState.EnableInClassList("connected", showConnected);
                _settingsAccountState.EnableInClassList("missing", !showConnected && !isPending);
                _settingsAccountState.EnableInClassList("checking", isPending);
            }

            RefreshItchAccountState(
                _settingsAccountState,
                _settingsAccountTitle,
                _settingsAccountCopy,
                hasValidatedAccount,
                hasStoredAuthorization);
            RefreshItchAccountState(
                _settingsSystemLoginState,
                _settingsSystemLoginTitle,
                _settingsSystemLoginCopy,
                hasValidatedAccount,
                hasStoredAuthorization);

            RefreshItchConnectButton(_settingsAccountConnectButton, showConnected);
            RefreshItchConnectButton(_settingsSystemConnectButton, showConnected);

            if (_settingsProjectsRefreshButton != null)
            {
                _settingsProjectsRefreshButton.text = isPending
                    ? ItchDeployLocale._(
                        "Loading projects…",
                        "Завантажуємо проєкти…")
                    : ItchDeployLocale._(
                        "Refresh project list",
                        "Оновити список проєктів");
                _settingsProjectsRefreshButton.SetEnabled(
                    hasStoredAuthorization && !isPending);
            }

            if (_settingsProjectDropdown != null)
            {
                var choices = BuildItchProjectChoices();
                var selectedGame = _settingsItchGames.FirstOrDefault(game =>
                    string.Equals(
                        GetItchTargetFromUrl(game.url),
                        $"{_settings.itchUserName}/{_settings.itchGameSlug}",
                        StringComparison.OrdinalIgnoreCase));
                var selectedIndex = selectedGame == null
                    ? -1
                    : _settingsItchGames.IndexOf(selectedGame);
                _settingsProjectDropdown.placeholder = isPending
                    ? ItchDeployLocale._(
                        "Loading projects…",
                        "Завантажуємо проєкти…")
                    : _settingsItchGames.Count == 0 && HasConfiguredItchPage()
                        ? ItchDeployLocale._(
                            $"Current target: {_settings.itchGameSlug}",
                            $"Поточна ціль: {_settings.itchGameSlug}")
                        : _settingsItchGames.Count == 0
                            ? ItchDeployLocale._(
                                "No projects available",
                                "Немає доступних проєктів")
                            : ItchDeployLocale._(
                                "Select a project",
                                "Виберіть проєкт");
                _settingsProjectDropdown.SetChoices(choices, selectedIndex);
                _settingsProjectDropdown.SetEnabled(
                    _settingsItchProfile != null &&
                    _settingsItchGames.Count > 0 &&
                    !isPending);
            }
        }

        private void RefreshItchAccountState(
            VisualElement state,
            Label title,
            Label copy,
            bool hasValidatedAccount,
            bool hasStoredAuthorization)
        {
            if (state == null || title == null || copy == null)
                return;

            var isPending = _itchLoginPending || _itchAccountRefreshPending;
            state.EnableInClassList("connected", hasValidatedAccount);
            state.EnableInClassList("missing", !hasValidatedAccount && !isPending);
            state.EnableInClassList("checking", isPending);

            if (_itchLoginPending)
            {
                title.text = ItchDeployLocale._(
                    "Finish sign-in in the browser",
                    "Завершіть вхід у браузері");
                copy.text = ItchDeployLocale._(
                    "The plugin is waiting for itch.io to complete authorization.",
                    "Плагін очікує завершення авторизації на itch.io.");
                return;
            }

            if (_itchAccountRefreshPending)
            {
                title.text = ItchDeployLocale._(
                    "Checking itch.io connection",
                    "Перевіряю підключення itch.io");
                copy.text = ItchDeployLocale._(
                    "Validating the account and loading available projects...",
                    "Перевіряю акаунт і завантажую доступні проєкти...");
                return;
            }

            if (hasValidatedAccount)
            {
                title.text = _settingsItchProfile == null
                    ? ItchDeployLocale._(
                        "Ready to deploy through Butler",
                        "Готово до деплою через Butler")
                    : ItchDeployLocale._(
                        $"Connected as {_settingsItchProfile.username}",
                        $"Підключено як {_settingsItchProfile.username}");
                copy.text = ItchDeployLocale._(
                    $"Projects available: {_settingsItchGames.Count}. Choose the deployment target below.",
                    $"Доступних проєктів: {_settingsItchGames.Count}. Виберіть ціль деплою нижче.");
                return;
            }

            if (_settingsItchDataLoaded && hasStoredAuthorization)
            {
                title.text = ItchDeployLocale._(
                    "Sign-in needs to be renewed",
                    "Потрібен повторний вхід");
                copy.text = string.IsNullOrWhiteSpace(_itchConnectionMessage)
                    ? ItchDeployLocale._(
                        "The saved authorization could not be verified.",
                        "Не вдалося перевірити збережену авторизацію.")
                    : _itchConnectionMessage;
                return;
            }

            title.text = hasStoredAuthorization
                ? ItchDeployLocale._(
                    "Saved authorization is ready to verify",
                    "Збережену авторизацію можна перевірити")
                : ItchDeployLocale._(
                    "itch.io account is not connected",
                    "Акаунт itch.io не підключено");
            copy.text = hasStoredAuthorization
                ? ItchDeployLocale._(
                    "The plugin will verify it before deployment.",
                    "Плагін перевірить її перед деплоєм.")
                : ItchDeployLocale._(
                    "Click the button and finish sign-in in the browser.",
                    "Натисніть кнопку та завершіть вхід у браузері.");
        }

        private void RefreshItchConnectButton(Button button, bool showConnected)
        {
            if (button == null)
                return;

            button.text = _itchLoginPending
                ? ItchDeployLocale._("Waiting for browser...", "Очікування браузера...")
                : _itchAccountRefreshPending
                    ? ItchDeployLocale._("Checking connection...", "Перевіряю підключення...")
                : showConnected
                    ? ItchDeployLocale._("Check again", "Перевірити ще раз")
                : ItchIOAPI.HasAuth && !_settingsItchDataLoaded
                    ? ItchDeployLocale._(
                        "Verify saved authorization",
                        "Перевірити збережену авторизацію")
                : ItchIOAPI.HasAuth
                    ? ItchDeployLocale._(
                        "Renew itch.io sign-in",
                        "Оновити вхід itch.io")
                    : ItchDeployLocale._(
                        "Connect itch.io",
                        "Підключити itch.io");
            button.SetEnabled(!_itchLoginPending && !_itchAccountRefreshPending);
        }

        private void ConnectItchAccount()
        {
            if (_itchLoginPending || _itchAccountRefreshPending)
                return;

            if (_settingsItchProfile != null)
            {
                RefreshItchAccountData();
                return;
            }

            if (ItchIOAPI.HasAuth && !_settingsItchDataLoaded)
            {
                RefreshItchAccountData(valid =>
                {
                    if (!valid)
                        StartItchBrowserLogin();
                });
                return;
            }

            StartItchBrowserLogin();
        }

        private void StartItchBrowserLogin()
        {
            _itchLoginPending = true;
            _itchConnectionMessage = string.Empty;
            RefreshItchAccountControls();
            ItchDeployOAuth.BeginLogin((success, message) =>
            {
                if (this == null)
                    return;

                _itchLoginPending = false;
                if (success)
                {
                    _settingsItchDataLoaded = false;
                    RefreshItchAccountData(valid =>
                    {
                        if (valid)
                            return;

                        _itchConnectionMessage = ItchDeployLocale._(
                            "itch.io completed sign-in, but the API connection could not be verified.",
                            "Вхід на itch.io завершено, але не вдалося перевірити API-підключення.");
                        RefreshItchAccountControls();
                    });
                }
                else
                {
                    _settingsItchDataLoaded = true;
                    _itchConnectionMessage = message;
                    RefreshItchAccountControls();
                }
            });
        }

        private VisualElement BuildItchPageConnectionCard()
        {
            var card = new VisualElement
            {
                name = "itch-page-connection"
            };
            card.AddToClassList("deploy-panel");
            card.AddToClassList("itch-page-connection");

            var state = new VisualElement
            {
                name = "itch-page-state"
            };
            state.AddToClassList("itch-page-state");
            var stateIcon = new VisualElement
            {
                name = "itch-page-state-icon"
            };
            stateIcon.AddToClassList("itch-page-state-icon");
            var stateIconCore = new VisualElement();
            stateIconCore.AddToClassList("itch-page-state-icon-core");
            stateIcon.Add(stateIconCore);
            var stateBody = new VisualElement();
            stateBody.AddToClassList("itch-page-state-body");
            var stateTitle = new Label
            {
                name = "itch-page-state-title"
            };
            stateTitle.AddToClassList("itch-page-state-title");
            var stateCopy = new Label
            {
                name = "itch-page-state-copy"
            };
            stateCopy.AddToClassList("itch-page-state-copy");
            stateCopy.style.whiteSpace = WhiteSpace.Normal;
            stateBody.Add(stateTitle);
            stateBody.Add(stateCopy);
            state.Add(stateIcon);
            state.Add(stateBody);
            card.Add(state);

            var urlRow = new VisualElement
            {
                name = "itch-page-url-row"
            };
            urlRow.AddToClassList("itch-page-url-row");
            var urlCaption = new Label("URL");
            urlCaption.AddToClassList("itch-page-url-caption");
            var url = new Label
            {
                name = "itch-page-url"
            };
            url.AddToClassList("itch-page-url");
            url.selection.isSelectable = true;
            urlRow.Add(urlCaption);
            urlRow.Add(url);
            card.Add(urlRow);

            var connectedActions = new VisualElement
            {
                name = "itch-page-connected-actions"
            };
            connectedActions.AddToClassList("settings-action-row");
            connectedActions.AddToClassList("itch-page-actions");
            connectedActions.AddToClassList("settings-button-pair");
            var openPage = new Button(OpenItchProjectPage)
            {
                text = ItchDeployLocale._(
                    "Open public page",
                    "Відкрити сторінку")
            };
            openPage.tooltip = ItchDeployLocale._(
                "Open the public itch.io page players see.",
                "Відкрити публічну сторінку itch.io, яку бачать гравці.");
            openPage.AddToClassList("itch-page-primary");
            var editPage = new Button(OpenItchProjectEditor)
            {
                text = ItchDeployLocale._(
                    "Edit on itch.io",
                    "Редагувати на itch.io")
            };
            editPage.tooltip = ItchDeployLocale._(
                "Open the itch.io edit page for this project.",
                "Відкрити сторінку редагування цього проєкту на itch.io.");
            editPage.AddToClassList("secondary-action");
            editPage.AddToClassList("last");
            connectedActions.Add(openPage);
            connectedActions.Add(editPage);
            card.Add(connectedActions);

            var webgl = new VisualElement
            {
                name = "itch-page-webgl"
            };
            webgl.AddToClassList("itch-page-webgl");
            var webglTitle = new Label(ItchDeployLocale._(
                "WebGL in the browser",
                "WebGL у браузері"));
            webglTitle.AddToClassList("itch-page-webgl-title");
            var webglCopy = new Label
            {
                name = "itch-page-webgl-copy"
            };
            webglCopy.AddToClassList("itch-page-webgl-copy");
            webglCopy.style.whiteSpace = WhiteSpace.Normal;
            var webglAction = new Button(OpenItchProjectEditor)
            {
                text = ItchDeployLocale._(
                    "Open WebGL page settings",
                    "Відкрити налаштування WebGL")
            };
            webglAction.tooltip = ItchDeployLocale._(
                "Open the itch.io page settings where an HTML5 upload is marked playable in the browser.",
                "Відкрити налаштування сторінки itch.io, де HTML5-збірка позначається як така, що грається в браузері.");
            webglAction.AddToClassList("itch-page-webgl-action");
            webglAction.AddToClassList("secondary-action");
            webgl.Add(webglTitle);
            webgl.Add(webglCopy);
            webgl.Add(webglAction);
            card.Add(webgl);

            RefreshItchPageConnectionCard();
            return card;
        }

        private void RefreshItchPageConnectionCard()
        {
            var card = _itchPageConnectionCard;
            if (card == null)
                return;

            var connected = HasVerifiedItchPage();
            card.EnableInClassList("connected", connected);
            card.EnableInClassList("missing", !connected);

            var state = card.Q<VisualElement>("itch-page-state");
            state?.EnableInClassList("connected", connected);
            state?.EnableInClassList("missing", !connected);

            var stateTitle = card.Q<Label>("itch-page-state-title");
            if (stateTitle != null)
                stateTitle.text = connected
                    ? ItchDeployLocale._(
                        "Page address configured",
                        "Адресу сторінки налаштовано")
                    : ItchDeployLocale._(
                        "Project is not selected",
                        "Проєкт не вибрано");

            var stateCopy = card.Q<Label>("itch-page-state-copy");
            if (stateCopy != null)
                stateCopy.text = connected
                    ? ItchDeployLocale._(
                        "Unity builds will be uploaded to this page through Butler.",
                        "Unity-білди завантажуватимуться на цю сторінку через Butler.")
                    : ItchDeployLocale._(
                        "Select a project from the list above before deployment.",
                        "Перед деплоєм виберіть проєкт зі списку вище.");

            var urlRow = card.Q<VisualElement>("itch-page-url-row");
            if (urlRow != null)
                urlRow.style.display =
                    connected ? DisplayStyle.Flex : DisplayStyle.None;
            var url = card.Q<Label>("itch-page-url");
            if (url != null)
                url.text = connected ? GetConfiguredItchPageUrl() : "";

            var connectedActions = card.Q<VisualElement>(
                "itch-page-connected-actions");
            if (connectedActions != null)
                connectedActions.style.display =
                    connected ? DisplayStyle.Flex : DisplayStyle.None;
            var webgl = card.Q<VisualElement>("itch-page-webgl");
            if (webgl != null)
            {
                webgl.style.display =
                    connected ? DisplayStyle.Flex : DisplayStyle.None;
                webgl.EnableInClassList(
                    "active-target",
                    EditorUserBuildSettings.activeBuildTarget ==
                    BuildTarget.WebGL);
            }

            var webglCopy = card.Q<Label>("itch-page-webgl-copy");
            if (webglCopy != null)
                webglCopy.text =
                    EditorUserBuildSettings.activeBuildTarget ==
                    BuildTarget.WebGL
                        ? ItchDeployLocale._(
                            "After the first upload, enable “HTML5 / Playable in browser” once in the itch.io editor. Later WebGL builds update automatically.",
                            "Після першого завантаження один раз увімкніть «HTML5 / Playable in browser» у редакторі itch.io. Наступні WebGL-білди оновлюватимуться автоматично.")
                        : ItchDeployLocale._(
                            "For a WebGL build, enable “HTML5 / Playable in browser” once after the first upload.",
                            "Для WebGL після першого завантаження один раз увімкніть «HTML5 / Playable in browser».");
        }

        private bool HasConfiguredItchPage()
        {
            return !string.IsNullOrWhiteSpace(_settings?.itchUserName) &&
                   !string.IsNullOrWhiteSpace(_settings?.itchGameSlug);
        }

        private bool HasVerifiedItchPage()
        {
            if (!HasConfiguredItchPage())
                return false;
            if (!_settingsItchDataLoaded)
                return true;
            if (_settingsItchProfile == null)
                return false;

            var configuredTarget =
                $"{_settings.itchUserName}/{_settings.itchGameSlug}";
            return _settingsItchGames.Any(game =>
                string.Equals(
                    GetItchTargetFromUrl(game.url),
                    configuredTarget,
                    StringComparison.OrdinalIgnoreCase));
        }

        private string GetConfiguredItchPageUrl()
        {
            if (!HasConfiguredItchPage())
                return "";

            var user = _settings.itchUserName.Trim().Trim('/');
            var slug = _settings.itchGameSlug.Trim().Trim('/');
            return $"https://{user}.itch.io/{slug}";
        }

        private void OpenItchProjectPage()
        {
            var url = GetConfiguredItchPageUrl();
            if (string.IsNullOrEmpty(url))
            {
                EditorUtility.DisplayDialog(
                    "ItchDeploy",
                    ItchDeployLocale._(
                        "Enter the itch.io account name and project URL slug first.",
                        "Спершу заповніть ім’я акаунта та частину URL проєкту."),
                    "OK");
                return;
            }

            Application.OpenURL(url);
        }

        private void OpenItchProjectEditor()
        {
            var url = GetConfiguredItchPageUrl();
            if (string.IsNullOrEmpty(url))
            {
                EditorUtility.DisplayDialog(
                    "ItchDeploy",
                    ItchDeployLocale._(
                        "Enter the itch.io account name and project URL slug first.",
                        "Спершу заповніть ім’я акаунта та частину URL проєкту."),
                    "OK");
                return;
            }

            Application.OpenURL(url + "/edit");
        }
    }
}
