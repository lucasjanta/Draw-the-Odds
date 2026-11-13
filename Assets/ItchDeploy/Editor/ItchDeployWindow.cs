using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UIElements;

namespace ItchDeploy.Editor
{
    public partial class ItchDeployWindow : EditorWindow
    {
        private const string MainTabNamePrefix = "itch-main-tab-";
        private const double DashboardPollIntervalSeconds = 3.0d;
        private const double BuildLookupCacheSeconds = 5.0d;
        private ItchDeploySettings _settings;
        private int _selectedTab;

        private VisualElement _dashboardUI;
        private VisualElement _publishUI;
        private Label _publishTitleLabel;
        private Label _publishSubtitleLabel;
        private VisualElement _statusBadge;
        private Label _statusBadgeLabel;
        private Label _dashboardTargetLabel;
        private VisualElement _deployPanelContainer;
        private VisualElement _setupPanelContainer;
        private Label _deployBtnLabel;
        private Button _dashboardPrimaryButton;
        private Label _deployChannelLabel;
        private TextField _versionField;
        private TextField _publishVersionField;
        private Label _publishTargetLabel;
        private Label _publishChannelLabel;
        private Label _publishChannelCaption;
        private Label _publishHintLabel;
        private Button _publishPrimaryButton;
        private Button _publishExistingButton;
        private readonly List<VisualElement> _healthCards = new();
        private VisualElement _deployResultBanner;
        private Label _deployResultMsg;
        private VisualElement _historyRoot;
        private VisualElement _setupItemsRoot;
        private VisualElement _installBtnContainer;
        private Label _footerButlerLabel;
        private VisualElement _footerButlerStatus;
        private double _lastDashboardRefresh;
        private double _lastBuildLookupTime = double.NegativeInfinity;
        private BuildTarget _cachedLatestBuildTarget;
        private string _cachedLatestBuildPath;

        private VisualElement _settingsUI;
        private VisualElement _settingsStickyHeader;
        private ScrollView _contentScroll;
        private VisualElement _settingsContentContainer;
        private readonly List<VisualElement> _settingsSubPanels = new();
        private VisualElement _settingsAccountState;
        private Label _settingsAccountTitle;
        private Label _settingsAccountCopy;
        private Button _settingsAccountConnectButton;
        private Button _settingsSystemConnectButton;
        private VisualElement _settingsSystemLoginState;
        private Label _settingsSystemLoginTitle;
        private Label _settingsSystemLoginCopy;
        private ItchDeployChoiceField _settingsProjectDropdown;
        private Button _settingsProjectsRefreshButton;
        private VisualElement _itchPageConnectionCard;
        private ItchIOAPI.UserProfile _settingsItchProfile;
        private List<ItchIOAPI.GameInfo> _settingsItchGames = new();
        private bool _settingsItchDataLoaded;
        private bool _itchLoginPending;
        private bool _itchAccountRefreshPending;
        private bool _itchDeployAccessValidated;
        private string _itchValidatedDeployTarget;
        private string _itchConnectionMessage;
        private int _githubSecretsCheckGeneration;
        private int _unityAutomationCheckGeneration;

        private sealed class ItchAccountSnapshot
        {
            public ItchIOAPI.UserProfile profile;
            public List<ItchIOAPI.GameInfo> games = new();
            public bool deployAccessValidated;
            public string deployTarget;
        }

        private string _customVersion;
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.Info;
        private string _apiKeyField = "";
        private string _lastDeployResult = "";
        private bool _lastDeploySuccess;
        private double _lastDeployTime;

        private bool _butlerCached;
        private double _lastRefresh;
        private int _settingsSubTab;
        private bool _localeRebuildPending;
        private VisualElement _settingsTabViewport;

        internal BuildTarget? ActiveBuildTargetOverrideForTests { get; set; }

        internal void ShowSettingsSubTabForTests(int index) => ShowSettingsSubTab(index);

        private static Font _uiFont;

        public static void Open()
        {
            OpenWindow();
        }

        public static void OpenProjectSettings()
        {
            var window = OpenWindow();
            window.ShowTab(1);
            window.ShowSettingsSubTab(0);
            window.Repaint();
        }

        private static ItchDeployWindow OpenWindow()
        {
            var w = Resources.FindObjectsOfTypeAll<ItchDeployWindow>().FirstOrDefault();
            if (w == null)
                w = CreateInstance<ItchDeployWindow>();

            w.titleContent = new GUIContent("ItchDeploy");
            w.minSize = new Vector2(760, 560);
            w.maxSize = new Vector2(1440, 1000);

            w.Show();
            w.EnsureVisualTree();
            w.Focus();
            return w;
        }

        private void OnEnable()
        {
            RefreshCached();
            BuildDeployer.OnDeployComplete += OnDeployDone;
            BuildDeployer.OnBuildComplete += OnBuildDone;
            ButlerInstaller.TryAutoInstall();

            EditorApplication.delayCall -= EnsureVisualTree;
            EditorApplication.delayCall += EnsureVisualTree;
        }

        private void EnsureVisualTree()
        {
            if (this == null || rootVisualElement.childCount > 0)
                return;

            CreateGUI();
        }

        private void RefreshCached()
        {
            _settings = ItchDeploySettings.Load();
            _customVersion = Application.version;
            _apiKeyField = ItchDeploySecureStorage.LoadApiKey() ?? "";
            _butlerCached = ButlerBridge.IsButlerAvailable;
            _settingsItchProfile = null;
            _settingsItchGames.Clear();
            _settingsItchDataLoaded = false;
            _lastRefresh = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            _githubSecretsCheckGeneration++;
            _unityAutomationCheckGeneration++;
            BuildDeployer.OnDeployComplete -= OnDeployDone;
            BuildDeployer.OnBuildComplete -= OnBuildDone;
            EditorApplication.update -= OnDashboardUpdate;
            EditorApplication.delayCall -= EnsureVisualTree;
        }

        private void OnDeployDone(string result, bool success)
        {
            _statusType = success ? MessageType.Info : MessageType.Error;
            _statusMessage = success ? "Deploy successful" : "Deploy failed";
            _lastDeployResult = result;
            _lastDeploySuccess = success;
            _lastDeployTime = EditorApplication.timeSinceStartup;
            NotificationManager.SendAllInBackground(
                $"{Application.productName} v{_customVersion}: {result}",
                success);
            InvalidateLatestBuildCache();
            if (_dashboardUI?.style.display == DisplayStyle.Flex)
                RefreshDashboard(refreshHistory: true);
            if (_publishUI?.style.display == DisplayStyle.Flex)
                RefreshPublishUI(forceBuildLookup: true);
            Repaint();
        }

        private void OnBuildDone(BuildReport _)
        {
            InvalidateLatestBuildCache();
            if (_publishUI?.style.display == DisplayStyle.Flex)
                RefreshPublishUI(forceBuildLookup: true);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Clear();

            if (_settings == null)
                _settings = ItchDeploySettings.Load();
            if (_selectedTab != 0 && _selectedTab != 1 && _selectedTab != 3)
                _selectedTab = 0;

            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ItchDeploy/Editor/UI/ItchDeployWindow.uss");
            if (style != null)
                rootVisualElement.styleSheets.Add(style);

            var root = new VisualElement();
            root.AddToClassList("window-root");
            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                root.EnableInClassList("compact-window", evt.newRect.width < 920f);
                root.EnableInClassList("medium-window", evt.newRect.width < 1180f);
            });

            var sidebar = new VisualElement();
            sidebar.AddToClassList("sidebar");

            var brand = new VisualElement();
            brand.AddToClassList("sidebar-brand");
            var brandMark = new VisualElement();
            brandMark.AddToClassList("sidebar-brand-mark");
            var brandTray = new VisualElement();
            brandTray.AddToClassList("brand-deploy-tray");
            var brandStem = new VisualElement();
            brandStem.AddToClassList("brand-deploy-stem");
            var brandArrowLeft = new VisualElement();
            brandArrowLeft.AddToClassList("brand-deploy-arrow-left");
            var brandArrowRight = new VisualElement();
            brandArrowRight.AddToClassList("brand-deploy-arrow-right");
            brandMark.Add(brandTray);
            brandMark.Add(brandStem);
            brandMark.Add(brandArrowLeft);
            brandMark.Add(brandArrowRight);
            var brandCopy = new VisualElement();
            brandCopy.AddToClassList("sidebar-brand-copy");
            var brandTitle = new Label("ItchDeploy");
            brandTitle.AddToClassList("sidebar-brand-title");
            var brandCaption = new Label(ItchDeployLocale._(
                "Deploy to itch.io",
                "Деплой на itch.io"));
            brandCaption.AddToClassList("sidebar-brand-caption");
            brandCopy.Add(brandTitle);
            brandCopy.Add(brandCaption);
            brand.Add(brandMark);
            brand.Add(brandCopy);
            sidebar.Add(brand);

            var navigation = new ScrollView(ScrollViewMode.Vertical);
            navigation.AddToClassList("sidebar-navigation");
            navigation.contentContainer.AddToClassList("sidebar-navigation-content");
            sidebar.Add(navigation);

            var buttons = new List<Button>();

            void SelectNavigation(Button selected)
            {
                foreach (var button in buttons)
                    button.RemoveFromClassList("selected");
                selected.AddToClassList("selected");
            }

            void AddSection(string title)
            {
                var label = new Label(title);
                label.AddToClassList("group-label");
                navigation.Add(label);
            }

            void SetNavigationContent(Button button, string icon, string label)
            {
                button.text = "";

                var builtin = button.Q<Label>(className: "unity-text-element");
                if (builtin != null)
                    builtin.style.display = DisplayStyle.None;

                var iconLabel = new Label(icon);
                iconLabel.AddToClassList("nav-icon");

                var textLabel = new Label(label);
                textLabel.AddToClassList("nav-label");
                textLabel.style.flexGrow = 1;

                button.Add(iconLabel);
                button.Add(textLabel);
            }

            void AddTabButton(VisualElement parent, int tabIndex, string label)
            {
                var tabButton = new Button();
                tabButton.name = MainTabNamePrefix + tabIndex;
                var marker = tabIndex switch
                {
                    0 => "01",
                    3 => "02",
                    1 => "03",
                    _ => "--"
                };
                SetNavigationContent(tabButton, marker, label);

                tabButton.tooltip = null;
                tabButton.AddToClassList("tab-button");
                tabButton.clicked += () =>
                {
                    ShowTab(tabIndex);
                    SelectNavigation(tabButton);
                    Repaint();
                };
                if (tabIndex == _selectedTab)
                    tabButton.AddToClassList("selected");
                buttons.Add(tabButton);
                parent.Add(tabButton);
            }

            AddSection(ItchDeployLocale.GroupDeploy);
            AddTabButton(navigation, 0, ItchDeployLocale.TabDashboard);
            AddTabButton(navigation, 3, ItchDeployLocale.TabDeploy);

            AddSection(ItchDeployLocale.GroupSetup);
            AddTabButton(navigation, 1, ItchDeployLocale.TabSettings);

            var contentScroll = new ScrollView(ScrollViewMode.Vertical);
            contentScroll.AddToClassList("content-area");
            contentScroll.contentContainer.AddToClassList("content-area-inner");
            _contentScroll = contentScroll;

            _dashboardUI = BuildDashboard();
            contentScroll.Add(_dashboardUI);

            _publishUI = BuildPublishUI();
            _publishUI.style.display = DisplayStyle.None;
            contentScroll.Add(_publishUI);

            _settingsUI = BuildSettingsUI();
            _settingsUI.style.display = DisplayStyle.None;
            contentScroll.Add(_settingsUI);

            _settingsStickyHeader = BuildSettingsStickyHeader();
            _settingsStickyHeader.style.display = DisplayStyle.None;

            var mainColumn = new VisualElement();
            mainColumn.AddToClassList("main-column");
            mainColumn.Add(contentScroll);

            mainColumn.Add(_settingsStickyHeader);
            mainColumn.Add(BuildFooter());

            root.Add(sidebar);
            root.Add(mainColumn);
            rootVisualElement.Add(root);

            EditorApplication.update -= OnDashboardUpdate;
            EditorApplication.update += OnDashboardUpdate;
            ApplyUiFont(root);
            ShowTab(_selectedTab);
            if (_selectedTab == 1)
                ShowSettingsSubTab(_settingsSubTab);
        }

        private static Font GetUiFont()
        {
            if (_uiFont != null)
                return _uiFont;

#if UNITY_EDITOR_WIN
            var preferredFonts = new[] { "Segoe UI", "Arial" };
#elif UNITY_EDITOR_OSX
            var preferredFonts = new[] { "SF Pro Text", "Helvetica Neue", "Arial" };
#else
            var preferredFonts = new[] { "Noto Sans", "Ubuntu", "DejaVu Sans" };
#endif
            _uiFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 13);
            if (_uiFont != null)
                _uiFont.hideFlags = HideFlags.HideAndDontSave;
            return _uiFont;
        }

        private static void ApplyUiFont(VisualElement root)
        {
            if (root == null)
                return;

            var font = GetUiFont();
            if (font == null)
                return;

            root.style.unityFont = font;
            foreach (var textElement in root.Query<TextElement>().ToList())
                textElement.style.unityFont = font;
        }

        private static void PrepareToolkitSurface(VisualElement root)
        {
            if (root == null) return;

            root.AddToClassList("toolkit-surface");
            ApplyUiFont(root);

            foreach (var button in root.Query<Button>().ToList())
            {
                var hasComponentStyle = button.GetClasses().Any(className =>
                    !className.StartsWith("unity-", StringComparison.Ordinal));
                if (!hasComponentStyle)
                    button.AddToClassList("secondary-action");

                if (button.text == "?")
                    button.AddToClassList("icon-action");
            }

            foreach (var label in root.Query<Label>().ToList())
            {
                if (label.ClassListContains("unity-base-field__label"))
                    continue;

                var hasComponentStyle = label.GetClasses().Any(className =>
                    !className.StartsWith("unity-", StringComparison.Ordinal));
                if (!hasComponentStyle)
                    label.AddToClassList("toolkit-default-text");
            }

            foreach (var field in root
                         .Query<VisualElement>(className: "unity-base-field")
                         .ToList())
            {
                if (field is Toggle)
                    continue;

                var input = field.Q<VisualElement>(
                    className: "unity-base-field__input");
                if (input == null)
                    continue;

                void SetControlHover(bool hovered)
                {
                    input.EnableInClassList("control-hover", hovered);
                }

                field.RegisterCallback<PointerOverEvent>(
                    _ => SetControlHover(true));
                field.RegisterCallback<PointerOutEvent>(
                    _ => SetControlHover(false));
                field.RegisterCallback<MouseOverEvent>(
                    _ => SetControlHover(true));
                field.RegisterCallback<MouseOutEvent>(
                    _ => SetControlHover(false));
                field.RegisterCallback<FocusInEvent>(
                    _ => input.AddToClassList("control-focus"));
                field.RegisterCallback<FocusOutEvent>(
                    _ => input.RemoveFromClassList("control-focus"));
            }

            foreach (var toggle in root.Query<Toggle>().ToList())
            {
                var checkmark = toggle.Q<VisualElement>(
                    className: "unity-toggle__checkmark");
                if (checkmark == null) continue;

                var indicator = checkmark.Q<VisualElement>("itch-toggle-indicator");
                if (indicator == null)
                {
                    indicator = new VisualElement
                    {
                        name = "itch-toggle-indicator",
                        pickingMode = PickingMode.Ignore
                    };
                    indicator.AddToClassList("toggle-indicator");
                    checkmark.Add(indicator);
                }

                void RefreshIndicator(bool value) =>
                    indicator.style.display =
                        value ? DisplayStyle.Flex : DisplayStyle.None;

                RefreshIndicator(toggle.value);
                toggle.RegisterValueChangedCallback(
                    change => RefreshIndicator(change.newValue));
                toggle.RegisterCallback<PointerEnterEvent>(
                    _ => checkmark.AddToClassList("control-hover"));
                toggle.RegisterCallback<PointerLeaveEvent>(
                    _ => checkmark.RemoveFromClassList("control-hover"));
                toggle.RegisterCallback<FocusInEvent>(
                    _ => checkmark.AddToClassList("control-focus"));
                toggle.RegisterCallback<FocusOutEvent>(
                    _ => checkmark.RemoveFromClassList("control-focus"));
            }
        }

        private void ShowTab(int idx)
        {
            _selectedTab = idx;
            foreach (var navigationButton in rootVisualElement
                         .Query<Button>(className: "tab-button")
                         .ToList())
            {
                navigationButton.EnableInClassList(
                    "selected",
                    navigationButton.name == MainTabNamePrefix + idx);
            }
            if (_dashboardUI == null || _settingsUI == null) return;

            _dashboardUI.style.display = idx == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _publishUI.style.display = idx == 3 ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsUI.style.display = idx == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_settingsStickyHeader != null)
                _settingsStickyHeader.style.display =
                    idx == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            _contentScroll?.EnableInClassList("settings-header-visible", idx == 1);

            if (idx == 0) RefreshDashboard(refreshHistory: true);
            else if (idx == 3) RefreshPublishUI(forceBuildLookup: true);
            else if (idx == 1)
                RefreshSettingsUI();
        }

        private VisualElement BuildDashboard()
        {
            var dash = new VisualElement();
            dash.AddToClassList("dashboard");
            dash.style.display = DisplayStyle.Flex;

            var header = new VisualElement();
            header.AddToClassList("status-header");
            header.AddToClassList("dashboard-hero");

            var heroCopy = new VisualElement();
            heroCopy.AddToClassList("dashboard-hero-copy");
            var eyebrow = new Label(ItchDeployLocale._(
                "Deployment overview",
                "Огляд деплою"));
            eyebrow.AddToClassList("dashboard-eyebrow");
            var productLabel = new Label(Application.productName);
            productLabel.AddToClassList("status-product");
            var heroSubtitle = new Label(ItchDeployLocale._(
                "Build the active Unity platform and deploy it to itch.io.",
                "Збірка активної платформи Unity та деплой на itch.io."));
            heroSubtitle.AddToClassList("dashboard-hero-subtitle");
            heroCopy.Add(eyebrow);
            heroCopy.Add(productLabel);
            heroCopy.Add(heroSubtitle);

            var heroAside = new VisualElement();
            heroAside.AddToClassList("dashboard-hero-aside");
            var targetCaption = new Label(ItchDeployLocale._(
                "itch.io target",
                "Ціль itch.io"));
            targetCaption.AddToClassList("dashboard-target-caption");

            _dashboardTargetLabel = new Label();
            _dashboardTargetLabel.AddToClassList("deploy-target");

            _statusBadge = new VisualElement();
            _statusBadge.AddToClassList("status-badge");
            var statusBadgeDot = new VisualElement();
            statusBadgeDot.AddToClassList("status-badge-dot");
            _statusBadgeLabel = new Label();
            _statusBadgeLabel.AddToClassList("status-badge-label");
            _statusBadge.Add(statusBadgeDot);
            _statusBadge.Add(_statusBadgeLabel);

            heroAside.Add(targetCaption);
            heroAside.Add(_dashboardTargetLabel);
            heroAside.Add(_statusBadge);
            header.Add(heroCopy);
            header.Add(heroAside);
            dash.Add(header);

            _deployPanelContainer = BuildDeployPanel();
            dash.Add(_deployPanelContainer);

            _setupPanelContainer = BuildSetupPanel();
            dash.Add(_setupPanelContainer);

            var healthHeading = new VisualElement();
            healthHeading.AddToClassList("section-heading");
            var healthTitle = new Label(ItchDeployLocale._(
                "Pipeline health",
                "Готовність пайплайна"));
            healthTitle.AddToClassList("section-heading-title");
            var healthSubtitle = new Label(ItchDeployLocale._(
                "Tools and connections required for deployment",
                "Інструменти й підключення, необхідні для деплою"));
            healthSubtitle.AddToClassList("section-heading-subtitle");
            healthHeading.Add(healthTitle);
            healthHeading.Add(healthSubtitle);
            dash.Add(healthHeading);

            var cardsRow = new VisualElement();
            cardsRow.AddToClassList("health-cards");
            _healthCards.Clear();
            foreach (var id in new[] { "butler", "creds", "key", "ci" })
            {
                var card = new VisualElement();
                card.AddToClassList("health-card");
                if (id == "ci")
                    card.AddToClassList("last");
                card.name = "hc-" + id;

                var icon = new VisualElement();
                icon.AddToClassList("health-card-icon");
                icon.name = "hc-icon";
                var iconCore = new VisualElement();
                iconCore.AddToClassList("health-card-icon-core");
                icon.Add(iconCore);

                var label = new Label(GetHealthLabel(id));
                label.AddToClassList("health-card-label");

                var status = new Label();
                status.AddToClassList("health-card-status");
                status.name = "hc-status";

                card.Add(icon);
                var copy = new VisualElement();
                copy.AddToClassList("health-card-copy");
                copy.Add(label);
                copy.Add(status);
                card.Add(copy);
                cardsRow.Add(card);
                _healthCards.Add(card);
            }
            dash.Add(cardsRow);

            _deployResultBanner = new VisualElement();
            _deployResultBanner.AddToClassList("deploy-result");
            _deployResultBanner.style.display = DisplayStyle.None;
            var resultIcon = new VisualElement();
            resultIcon.AddToClassList("deploy-result-icon");
            resultIcon.name = "dri-icon";
            var resultIconCore = new VisualElement();
            resultIconCore.AddToClassList("deploy-result-icon-core");
            resultIcon.Add(resultIconCore);
            var resultText = new Label();
            resultText.AddToClassList("deploy-result-text");
            resultText.name = "dri-text";
            _deployResultMsg = new Label();
            _deployResultMsg.AddToClassList("deploy-result-msg");
            _deployResultBanner.Add(resultIcon);
            _deployResultBanner.Add(resultText);
            _deployResultBanner.Add(_deployResultMsg);
            dash.Add(_deployResultBanner);

            _historyRoot = new VisualElement();
            _historyRoot.AddToClassList("history-section");
            _historyRoot.AddToClassList("dashboard-secondary-card");
            _historyRoot.AddToClassList("last");
            dash.Add(_historyRoot);

            return dash;
        }

        private VisualElement BuildPublishUI()
        {
            var root = new VisualElement();
            root.AddToClassList("dashboard");
            root.AddToClassList("publish-surface");

            _publishTitleLabel = new Label(ItchDeployLocale._(
                "Build and deploy",
                "Збірка та деплой"));
            _publishTitleLabel.AddToClassList("page-title");
            root.Add(_publishTitleLabel);

            _publishSubtitleLabel = new Label(ItchDeployLocale._(
                "Build the active Unity platform and deploy it to the selected itch.io channel.",
                "Створює збірку активної платформи Unity та виконує деплой у вибраний канал itch.io."));
            _publishSubtitleLabel.AddToClassList("page-subtitle");
            root.Add(_publishSubtitleLabel);

            var publishCard = new VisualElement();
            publishCard.AddToClassList("publish-card");

            var targetRow = new VisualElement();
            targetRow.AddToClassList("publish-info-row");
            targetRow.AddToClassList("publish-target-row");
            var targetCaption = new Label(ItchDeployLocale._(
                "Active platform",
                "Активна платформа"));
            targetCaption.AddToClassList("publish-caption");
            _publishTargetLabel = new Label();
            _publishTargetLabel.AddToClassList("publish-value");
            var targetButton = new Button(OpenUnityBuildSettings)
            {
                text = ItchDeployLocale._("Change", "Змінити")
            };
            targetButton.tooltip = ItchDeployLocale._(
                "Open Unity's Build Settings to change the active platform. The itch.io channel follows it.",
                "Відкрити Build Settings, щоб змінити активну платформу. Канал itch.io зміниться слідом.");
            targetButton.AddToClassList("secondary-action");
            targetButton.AddToClassList("publish-change-button");
            targetRow.Add(targetCaption);
            targetRow.Add(_publishTargetLabel);
            targetRow.Add(new VisualElement { style = { flexGrow = 1 } });
            targetRow.Add(targetButton);
            publishCard.Add(targetRow);

            var channelRow = new VisualElement();
            channelRow.AddToClassList("publish-info-row");
            _publishChannelCaption = new Label(ItchDeployLocale._(
                "itch.io channel",
                "Канал itch.io"));
            _publishChannelCaption.AddToClassList("publish-caption");
            _publishChannelLabel = new Label();
            _publishChannelLabel.AddToClassList("publish-value");
            channelRow.Add(_publishChannelCaption);
            channelRow.Add(_publishChannelLabel);
            publishCard.Add(channelRow);

            var publishVersionRow = new VisualElement();
            publishVersionRow.AddToClassList("publish-version-control-row");
            var publishVersionCaption = new Label(ItchDeployLocale.Version);
            publishVersionCaption.AddToClassList("publish-caption");
            _publishVersionField = new TextField();
            _publishVersionField.AddToClassList("publish-version-input");
            _publishVersionField.RegisterValueChangedCallback(ev => _customVersion = ev.newValue.Trim());
            publishVersionRow.Add(publishVersionCaption);
            publishVersionRow.Add(_publishVersionField);
            publishCard.Add(publishVersionRow);

            _publishHintLabel = new Label();
            _publishHintLabel.AddToClassList("publish-hint");
            publishCard.Add(_publishHintLabel);

            _publishPrimaryButton = new Button(StartBuildAndPublish);
            _publishPrimaryButton.tooltip = ItchDeployLocale._(
                "Build the active platform and upload the result to its itch.io channel.",
                "Зібрати активну платформу та завантажити результат у відповідний канал itch.io.");
            _publishPrimaryButton.AddToClassList("deploy-button");
            publishCard.Add(_publishPrimaryButton);

            var actions = new VisualElement();
            actions.AddToClassList("settings-action-row");
            _publishExistingButton = new Button(DeployMostRecentBuild)
            {
                text = ItchDeployLocale._(
                    "Deploy latest completed build",
                    "Завантажити останню готову збірку")
            };
            _publishExistingButton.tooltip = ItchDeployLocale._(
                "Upload the build already on disk without building again.",
                "Завантажити збірку, яка вже є на диску, не збираючи заново.");
            _publishExistingButton.AddToClassList("secondary-action");
            var openBuilds = new Button(OpenBuildsFolder)
            {
                text = ItchDeployLocale._(
                    "Open builds folder",
                    "Відкрити папку білдів")
            };
            openBuilds.tooltip = ItchDeployLocale._(
                "Open the Builds folder in the file browser.",
                "Відкрити теку Builds у файловому провіднику.");
            openBuilds.AddToClassList("secondary-action");
            actions.Add(_publishExistingButton);
            actions.Add(openBuilds);
            publishCard.Add(actions);
            root.Add(publishCard);

            var note = new VisualElement();
            note.AddToClassList("publish-steps-card");
            var noteTitle = new Label(ItchDeployLocale._(
                "Deployment steps",
                "Етапи деплою"));
            noteTitle.AddToClassList("section-label");
            note.Add(noteTitle);
            var publishSteps = new VisualElement();
            publishSteps.AddToClassList("publish-steps");
            AddPublishStep(
                publishSteps,
                "01",
                ItchDeployLocale._("Unity build", "Збірка в Unity"),
                ItchDeployLocale._(
                    "Creates a build for the active platform.",
                    "Створює білд для активної платформи."));
            AddPublishStep(
                publishSteps,
                "02",
                ItchDeployLocale._("Upload with Butler", "Завантаження через Butler"),
                ItchDeployLocale._(
                    "Uploads the completed build folder to itch.io.",
                    "Завантажує готову папку білда на itch.io."));
            AddPublishStep(
                publishSteps,
                "03",
                ItchDeployLocale._("Deployment result", "Результат деплою"),
                ItchDeployLocale._(
                    "Updates history and the local report after completion.",
                    "Оновлює історію та локальний звіт після завершення."));
            note.Add(publishSteps);
            root.Add(note);

            PrepareToolkitSurface(root);
            return root;
        }

        private void RefreshPublishUI(bool forceBuildLookup = false)
        {
            if (_publishUI == null) return;

            var target = EditorUserBuildSettings.activeBuildTarget;
            var buildSupported = BuildPlatformCatalog.TryValidateBuildConfiguration(
                target,
                out var buildReadinessIssue,
                out var buildSupportError);
            var directDeploy = BuildPlatformCatalog.CanDeployCurrentArtifactToItch(
                target,
                out _);
            if (!buildSupported)
                buildSupportError = GetLocalizedBuildSupportError(
                    target,
                    buildReadinessIssue,
                    buildSupportError);
            var buildOnlyReason = GetLocalizedBuildOnlyReason(target);
            var deployReady = _butlerCached
                              && !string.IsNullOrWhiteSpace(_settings.itchUserName)
                              && !string.IsNullOrWhiteSpace(_settings.itchGameSlug)
                              && _settings.HasApiKey;
            var ready = buildSupported && (!directDeploy || deployReady);
            var busy = BuildDeployer.IsBuilding || BuildDeployer.IsDeploying;
            var latestBuild = GetMostRecentBuild(target, forceBuildLookup);

            _publishTargetLabel.text = PlatformChannelMap.GetDisplayName(target);
            _publishTitleLabel.text = directDeploy
                ? ItchDeployLocale._("Build and deploy", "Збірка та деплой")
                : ItchDeployLocale._("Build artifact", "Збірка артефакту");
            _publishSubtitleLabel.text = directDeploy
                ? ItchDeployLocale._(
                    "Build the active Unity platform and deploy it to the selected itch.io channel.",
                    "Створює збірку активної платформи Unity та виконує деплой у вибраний канал itch.io.")
                : buildOnlyReason;
            _publishChannelCaption.text = directDeploy
                ? ItchDeployLocale._("itch.io channel", "Канал itch.io")
                : ItchDeployLocale._("Build artifact", "Артефакт збірки");
            _publishChannelLabel.text = directDeploy
                ? $"{_settings.itchUserName}/{_settings.itchGameSlug} : {FormatChannelForDisplay(ResolveChannel(target))}"
                : GetLocalizedArtifactDescription(target);
            _publishVersionField.SetValueWithoutNotify(string.IsNullOrWhiteSpace(_customVersion) ? Application.version : _customVersion);
            _publishPrimaryButton.text = busy
                ? BuildDeployer.CurrentStatus
                : GetPrimaryPipelineActionLabel(target, directDeploy);
            _publishPrimaryButton.SetEnabled(ready && !busy);
            _publishExistingButton.style.display = directDeploy
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _publishExistingButton.SetEnabled(
                directDeploy && ready && !busy && !string.IsNullOrEmpty(latestBuild));
            _publishHintLabel.text = !buildSupported
                ? buildSupportError
                : !directDeploy
                    ? buildOnlyReason
                    : ready
                ? (string.IsNullOrEmpty(latestBuild)
                    ? ItchDeployLocale._(
                        "Ready for the first build.",
                        "Готово до першої збірки.")
                    : ItchDeployLocale._(
                        "A completed build was found and can be deployed without rebuilding.",
                        "Знайдено готову збірку — її можна завантажити без повторної збірки."))
                : ItchDeployLocale._(
                    "First configure the account, project URL, API key and Butler in Project settings.",
                    "Спершу вкажіть акаунт, URL проєкту, API-ключ і встановіть Butler у «Налаштуваннях проєкту».");
        }

        private void StartBuildAndPublish()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            if (target == BuildTarget.WebGL)
                WebGLHelper.SetCompression(_settings.webglCompression);
            var directDeploy = BuildPlatformCatalog.CanDeployCurrentArtifactToItch(
                target,
                out _);
            BuildDeployer.BuildAndDeploy(
                target,
                ResolveChannel(target),
                _customVersion,
                _settings,
                deployAfterBuild: directDeploy);
        }

        private void DeployMostRecentBuild()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var latestBuild = GetMostRecentBuild(target, force: true);
            if (string.IsNullOrEmpty(latestBuild)) return;
            BuildDeployer.DeployExistingBuild(latestBuild, target, ResolveChannel(target), _customVersion, _settings);
        }

        private string GetMostRecentBuild(BuildTarget target, bool force = false)
        {
            var now = EditorApplication.timeSinceStartup;
            if (force ||
                target != _cachedLatestBuildTarget ||
                now - _lastBuildLookupTime >= BuildLookupCacheSeconds)
            {
                _cachedLatestBuildTarget = target;
                _cachedLatestBuildPath = BuildDeployer.FindMostRecentBuild(target);
                _lastBuildLookupTime = now;
            }

            return _cachedLatestBuildPath;
        }

        private void InvalidateLatestBuildCache()
        {
            _lastBuildLookupTime = double.NegativeInfinity;
            _cachedLatestBuildPath = null;
        }

        private static void OpenUnityBuildSettings()
        {
            EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
        }

        private static void OpenBuildsFolder()
        {
            Directory.CreateDirectory(BuildDeployer.BuildsRoot);
            EditorUtility.RevealInFinder(BuildDeployer.BuildsRoot);
        }

        private VisualElement BuildDeployPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("deploy-panel");

            var label = new Label(ItchDeployLocale._(
                "Deploy",
                "Деплой"));
            label.AddToClassList("section-label");
            panel.Add(label);

            _dashboardPrimaryButton = new Button(() =>
            {
                if (BuildDeployer.IsBuilding || BuildDeployer.IsDeploying) return;
                var target = EditorUserBuildSettings.activeBuildTarget;
                if (target == BuildTarget.WebGL)
                    WebGLHelper.SetCompression(_settings.webglCompression);
                var directDeploy = BuildPlatformCatalog.CanDeployCurrentArtifactToItch(
                    target,
                    out _);
                BuildDeployer.BuildAndDeploy(
                    target,
                    ResolveChannel(target),
                    _customVersion,
                    _settings,
                    deployAfterBuild: directDeploy);
            });
            _dashboardPrimaryButton.text = "";
            _dashboardPrimaryButton.tooltip = ItchDeployLocale._(
                "Run the whole pipeline for the active platform: validate, build, then upload through Butler.",
                "Виконати весь шлях для активної платформи: перевірка, збірка, завантаження через Butler.");
            _dashboardPrimaryButton.AddToClassList("deploy-button");
            _dashboardPrimaryButton.AddToClassList("dashboard-deploy-button");

            _deployBtnLabel = new Label();
            _deployBtnLabel.AddToClassList("deploy-button-label");
            _dashboardPrimaryButton.Add(_deployBtnLabel);
            panel.Add(_dashboardPrimaryButton);

            var row = new VisualElement();
            row.AddToClassList("deploy-meta-row");

            _deployChannelLabel = new Label();
            _deployChannelLabel.AddToClassList("deploy-target-label");
            _deployChannelLabel.style.flexGrow = 1;
            row.Add(_deployChannelLabel);
            panel.Add(row);

            var versionRow = new VisualElement();
            versionRow.AddToClassList("deploy-version-row");

            var verLabel = new Label(ItchDeployLocale.Version + ": ");
            verLabel.AddToClassList("deploy-version-label");

            _versionField = new TextField();
            _versionField.value = _customVersion;
            _versionField.AddToClassList("version-input");
            _versionField.style.flexGrow = 1;
            _versionField.RegisterValueChangedCallback(ev => _customVersion = ev.newValue);

            var resetBtn = new Button(() => { _versionField.value = Application.version; _customVersion = Application.version; });
            resetBtn.text = ItchDeployLocale._("Use Unity version", "Взяти версію Unity");
            resetBtn.tooltip = ItchDeployLocale._(
                "Replace the version above with PlayerSettings.bundleVersion.",
                "Замінити версію вище на PlayerSettings.bundleVersion.");
            resetBtn.AddToClassList("compact-action");

            var targetBtn = new Button(() =>
            {
                EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
            });
            targetBtn.text = ItchDeployLocale._("Build Settings", "Налаштування збірки");
            targetBtn.tooltip = ItchDeployLocale._(
                "Open Unity's own Build Settings window.",
                "Відкрити власне вікно Build Settings в Unity.");
            targetBtn.AddToClassList("compact-action");
            targetBtn.AddToClassList("last");

            versionRow.Add(verLabel);
            versionRow.Add(_versionField);
            versionRow.Add(resetBtn);
            versionRow.Add(targetBtn);
            panel.Add(versionRow);

            return panel;
        }

        private VisualElement BuildSetupPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("setup-panel");

            var title = new Label(ItchDeployLocale._(
                "Setup required",
                "Потрібне налаштування"));
            title.AddToClassList("section-label");
            panel.Add(title);

            _setupItemsRoot = new VisualElement();
            panel.Add(_setupItemsRoot);

            var setupBtn = new Button(OpenProjectSettings)
            {
                name = "setup-open-project-settings",
                text = ItchDeployLocale._(
                    "Open itch.io project settings",
                    "Відкрити налаштування проєкту itch.io")
            };
            setupBtn.tooltip = ItchDeployLocale._(
                "Open the settings where the itch.io account, project and channels are chosen.",
                "Відкрити налаштування, де обираються акаунт itch.io, проєкт і канали.");
            setupBtn.AddToClassList("btn");
            setupBtn.AddToClassList("btn--primary");
            panel.Add(setupBtn);

            _installBtnContainer = new VisualElement();
            var installBtn = new Button(() => ButlerBridge.TryAutoInstall());
            installBtn.text = ItchDeployLocale.InstallButler;
            installBtn.tooltip = ItchDeployLocale._(
                "Download Butler from itch.io and install it for this machine.",
                "Завантажити Butler з itch.io та встановити його на цю машину.");
            installBtn.AddToClassList("btn");
            _installBtnContainer.Add(installBtn);
            panel.Add(_installBtnContainer);

            var noSecrets = new Label(ItchDeployLocale.NoSecretsCI);
            noSecrets.AddToClassList("settings-muted");
            panel.Add(noSecrets);

            return panel;
        }

        private static void AddPublishStep(
            VisualElement parent,
            string number,
            string title,
            string copy)
        {
            var row = new VisualElement();
            row.AddToClassList("publish-step");
            var numberLabel = new Label(number);
            numberLabel.AddToClassList("publish-step-number");
            var body = new VisualElement();
            body.AddToClassList("publish-step-body");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("publish-step-title");
            var copyLabel = new Label(copy);
            copyLabel.AddToClassList("publish-step-copy");
            body.Add(titleLabel);
            body.Add(copyLabel);
            row.Add(numberLabel);
            row.Add(body);
            parent.Add(row);
        }

        private VisualElement BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("footer");
            var spacer = new VisualElement { style = { flexGrow = 1 } };
            _footerButlerStatus = new VisualElement();
            _footerButlerStatus.AddToClassList("footer-connection");
            var footerButlerDot = new VisualElement();
            footerButlerDot.AddToClassList("footer-status-dot");
            _footerButlerLabel = new Label();
            _footerButlerLabel.AddToClassList("footer-label");
            _footerButlerLabel.AddToClassList("footer-status-text");
            _footerButlerStatus.Add(footerButlerDot);
            _footerButlerStatus.Add(_footerButlerLabel);

            var refreshBtn = new Button(() =>
            {
                RefreshCached();
                RefreshDashboard();
                RefreshPublishUI();
                RefreshSettingsUI();
            });
            refreshBtn.text = ItchDeployLocale._("Check Butler", "Перевірити Butler");
            refreshBtn.tooltip = ItchDeployLocale._(
                "Refresh the local Butler installation status.",
                "Повторно перевірити локальне встановлення Butler.");
            refreshBtn.AddToClassList("footer-refresh");

            footer.Add(spacer);
            footer.Add(_footerButlerStatus);
            footer.Add(refreshBtn);
            RefreshFooter();
            return footer;
        }

        private void RefreshFooter()
        {
            if (_footerButlerLabel == null || _footerButlerStatus == null)
                return;

            _footerButlerLabel.text = _butlerCached
                ? ItchDeployLocale._("Butler connected", "Butler підключено")
                : ItchDeployLocale._("Butler unavailable", "Butler недоступний");
            _footerButlerStatus.RemoveFromClassList(
                _butlerCached ? "offline" : "connected");
            _footerButlerStatus.AddToClassList(
                _butlerCached ? "connected" : "offline");
        }

        private static string GetHealthLabel(string id)
        {
            return id switch
            {
                "butler" => ItchDeployLocale.CheckButler,
                "creds" => ItchDeployLocale.CheckCreds,
                "key" => ItchDeployLocale.CheckApiKey,
                "ci" => "GitHub Actions",
                _ => id
            };
        }

        private static string FormatChannelForDisplay(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel)) return "—";
            var value = channel.Trim();
            return value.Equals("html5", StringComparison.OrdinalIgnoreCase)
                ? "HTML5"
                : value;
        }

        private static string FormatHistoryDestination(string platform, string channel)
        {
            var platformDisplay = string.IsNullOrWhiteSpace(platform) ? "—" : platform.Trim();
            var channelDisplay = FormatChannelForDisplay(channel);
            return platformDisplay.IndexOf(
                       "(" + channelDisplay + ")",
                       StringComparison.OrdinalIgnoreCase) >= 0
                ? platformDisplay
                : $"{platformDisplay}  •  {channelDisplay}";
        }

        private void RefreshDashboard(bool refreshHistory = false)
        {
            var deployReady = _butlerCached
                && !string.IsNullOrEmpty(_settings.itchUserName)
                && !string.IsNullOrEmpty(_settings.itchGameSlug)
                && _settings.HasApiKey;

            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildSupported = BuildPlatformCatalog.TryValidateBuildConfiguration(
                activeTarget,
                out var buildReadinessIssue,
                out var buildSupportError);
            var directDeploy = BuildPlatformCatalog.CanDeployCurrentArtifactToItch(
                activeTarget,
                out _);
            if (!buildSupported)
                buildSupportError = GetLocalizedBuildSupportError(
                    activeTarget,
                    buildReadinessIssue,
                    buildSupportError);
            var buildOnlyReason = GetLocalizedBuildOnlyReason(activeTarget);
            var ready = buildSupported && (!directDeploy || deployReady);

            _statusBadgeLabel.text = !buildSupported
                ? ItchDeployLocale._("Build settings need attention", "Перевірте налаштування збірки")
                : directDeploy
                    ? (deployReady ? ItchDeployLocale.ReadyToDeploy : ItchDeployLocale.NeedsSetup)
                    : ItchDeployLocale._("Ready to build", "Готово до збірки");
            _statusBadge.RemoveFromClassList(ready ? "needs-setup" : "ready");
            _statusBadge.AddToClassList(ready ? "ready" : "needs-setup");

            if (_dashboardTargetLabel != null)
                _dashboardTargetLabel.text = !directDeploy
                    ? PlatformChannelMap.GetDisplayName(activeTarget) + "  •  " +
                      ItchDeployLocale._("build only", "лише збірка")
                    : string.IsNullOrWhiteSpace(_settings.itchUserName) ||
                      string.IsNullOrWhiteSpace(_settings.itchGameSlug)
                    ? PlatformChannelMap.GetDisplayName(activeTarget)
                    : $"{_settings.itchUserName}/{_settings.itchGameSlug}  •  {FormatChannelForDisplay(ResolveChannel(activeTarget))}";

            _deployPanelContainer.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
            _setupPanelContainer.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;

            var busy = BuildDeployer.IsBuilding || BuildDeployer.IsDeploying;
            _deployBtnLabel.text = busy
                ? BuildDeployer.CurrentStatus
                : GetPrimaryPipelineActionLabel(activeTarget, directDeploy);
            _dashboardPrimaryButton?.SetEnabled(ready && !busy);

            _deployChannelLabel.text =
                directDeploy
                    ? ItchDeployLocale.Target + ": " + PlatformChannelMap.GetDisplayName(activeTarget)
                    : buildOnlyReason;

            if (string.IsNullOrEmpty(_versionField.value))
                _versionField.value = Application.version;

            _installBtnContainer.style.display = DisplayStyle.None;
            if (!ready)
            {
                _setupItemsRoot.Clear();
                var missing = new List<string>();
                if (!buildSupported)
                    missing.Add(buildSupportError);
                if (directDeploy && !_butlerCached)
                    missing.Add(ItchDeployLocale.InstallButler);
                if (directDeploy && !_settings.HasApiKey)
                    missing.Add(ItchDeployLocale._(
                        "Connect an itch.io account",
                        "Підключити акаунт itch.io"));
                if (directDeploy &&
                    (string.IsNullOrEmpty(_settings.itchUserName) ||
                     string.IsNullOrEmpty(_settings.itchGameSlug)))
                    missing.Add(ItchDeployLocale._(
                        "Select an itch.io project",
                        "Вибрати проєкт itch.io"));
                for (int i = 0; i < missing.Count; i++)
                {
                    var item = new Label("  " + (i + 1) + ".  " + missing[i]);
                    item.AddToClassList("setup-item");
                    _setupItemsRoot.Add(item);
                }
                _installBtnContainer.style.display = (directDeploy && !_butlerCached && ButlerInstaller.CurrentStatus != ButlerInstaller.Status.Downloading)
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }

            UpdateHealthCard("hc-butler", _butlerCached);
            UpdateHealthCard("hc-creds",
                !string.IsNullOrEmpty(_settings.itchUserName) && !string.IsNullOrEmpty(_settings.itchGameSlug));
            UpdateHealthCard("hc-key", _settings.HasApiKey);
            UpdateHealthCard("hc-ci", File.Exists(Path.Combine(
                WorkflowGenerator.WorkflowsDir,
                "build-and-deploy.yml")));

            var elapsed = EditorApplication.timeSinceStartup - _lastDeployTime;
            if (elapsed < 60 && !string.IsNullOrEmpty(_lastDeployResult))
            {
                _deployResultBanner.style.display = DisplayStyle.Flex;
                _deployResultBanner.RemoveFromClassList(_lastDeploySuccess ? "fail" : "success");
                _deployResultBanner.AddToClassList(_lastDeploySuccess ? "success" : "fail");
                var textLabel = _deployResultBanner.Q<Label>("dri-text");
                if (textLabel != null)
                    textLabel.text = _lastDeploySuccess
                        ? ItchDeployLocale._(
                            "Deployment completed",
                            "Деплой завершено")
                        : ItchDeployLocale._(
                            "Deployment failed",
                            "Помилка деплою");
                _deployResultMsg.text = _lastDeployResult.Length > 80 ? _lastDeployResult[..80] + "..." : _lastDeployResult;
            }
            else
            {
                _deployResultBanner.style.display = DisplayStyle.None;
            }

            if (refreshHistory)
                RefreshHistory();

            RefreshFooter();
        }

        private static string GetPrimaryPipelineActionLabel(
            BuildTarget target,
            bool directDeploy)
        {
            if (directDeploy)
                return ItchDeployLocale._("Build and deploy", "Зібрати й завантажити");

            if (target == BuildTarget.iOS)
                return ItchDeployLocale._("Build Xcode project", "Зібрати Xcode-проєкт");

            if (target == BuildTarget.Android)
                return ItchDeployLocale._("Build Android App Bundle", "Зібрати Android App Bundle");

            return ItchDeployLocale._("Build", "Зібрати");
        }

        private static string GetLocalizedArtifactDescription(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.WebGL => ItchDeployLocale._(
                    "WebGL folder with index.html",
                    "Папка WebGL з index.html"),
                BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 =>
                    ItchDeployLocale._(
                        "Windows executable (.exe) and Data folder",
                        "Файл Windows (.exe) і папка Data"),
                BuildTarget.StandaloneLinux64 => ItchDeployLocale._(
                    "Linux executable (.x86_64) and Data folder",
                    "Файл Linux (.x86_64) і папка Data"),
                BuildTarget.StandaloneOSX => ItchDeployLocale._(
                    "macOS application bundle (.app)",
                    "Пакет застосунку macOS (.app)"),
                BuildTarget.Android => EditorUserBuildSettings.buildAppBundle
                    ? ItchDeployLocale._(
                        "Android App Bundle (.aab)",
                        "Android App Bundle (.aab)")
                    : ItchDeployLocale._(
                        "Android package (.apk)",
                        "Пакет Android (.apk)"),
                BuildTarget.iOS => ItchDeployLocale._(
                    "Xcode project (signing required on macOS)",
                    "Xcode-проєкт (потрібен підпис у macOS)"),
                _ => target.ToString(),
            };
        }

        private static string GetLocalizedBuildOnlyReason(BuildTarget target)
        {
            if (target == BuildTarget.iOS)
                return ItchDeployLocale._(
                    "Unity creates an unsigned Xcode project. Sign and export the IPA on macOS before distribution.",
                    "Unity створює непідписаний Xcode-проєкт. Перед розповсюдженням підпишіть і експортуйте IPA у macOS.");

            if (target == BuildTarget.Android && EditorUserBuildSettings.buildAppBundle)
                return ItchDeployLocale._(
                    "AAB is a build-only artifact for Google Play. Select APK to deploy through itch.io.",
                    "AAB — артефакт лише для Google Play. Для деплою через itch.io виберіть APK.");

            return string.Empty;
        }

        private static string GetLocalizedBuildSupportError(
            BuildTarget target,
            BuildPlatformCatalog.BuildReadinessIssue issue,
            string fallback)
        {
            return issue switch
            {
                BuildPlatformCatalog.BuildReadinessIssue.MissingScriptingBackend =>
                    ItchDeployLocale._(
                        $"The selected scripting backend is unavailable for {PlatformChannelMap.GetDisplayName(target)}. Install matching Build Support, switch the backend, or build on a matching OS runner.",
                        $"Вибраний scripting backend недоступний для {PlatformChannelMap.GetDisplayName(target)}. Встановіть відповідний модуль, змініть backend або збирайте на runner з відповідною ОС."),
                BuildPlatformCatalog.BuildReadinessIssue.UnsupportedAndroidInputHandling =>
                    ItchDeployLocale._(
                        "Android does not support Active Input Handling = Both. Select one input backend before building.",
                        "Android не підтримує Active Input Handling = Both. Перед збіркою виберіть один input backend."),
                BuildPlatformCatalog.BuildReadinessIssue.MissingBuildSupport =>
                    ItchDeployLocale._(
                        $"Unity Build Support for {PlatformChannelMap.GetDisplayName(target)} is not installed. Add it in Unity Hub.",
                        $"Модуль Unity Build Support для {PlatformChannelMap.GetDisplayName(target)} не встановлено. Додайте його в Unity Hub."),
                _ => fallback,
            };
        }

        private void RunPlatformPipeline(BuildTarget target, string channel)
        {
            if (target == BuildTarget.WebGL)
                WebGLHelper.SetCompression(_settings.webglCompression);

            var directDeploy = BuildPlatformCatalog.CanDeployCurrentArtifactToItch(
                target,
                out _);
            BuildDeployer.BuildAndDeploy(
                target,
                channel,
                _customVersion,
                _settings,
                deployAfterBuild: directDeploy);
        }

        private void UpdateHealthCard(string name, bool ok)
        {
            var card = _dashboardUI?.Q(name);
            if (card == null) return;
            card.RemoveFromClassList(ok ? "missing" : "ok");
            card.AddToClassList(ok ? "ok" : "missing");
            var status = card.Q<Label>("hc-status");
            if (status != null)
                status.text = ok
                    ? ItchDeployLocale._("Ready", "Готово")
                    : ItchDeployLocale._("Not configured", "Не налаштовано");
        }

        private void RefreshHistory()
        {
            _historyRoot.Clear();
            var history = DeployHistory.GetAll();
            var title = new Label(ItchDeployLocale._(
                "Latest deployment",
                "Останній деплой"));
            title.AddToClassList("section-label");
            _historyRoot.Add(title);

            if (history.Count == 0)
            {
                var empty = new Label(ItchDeployLocale._(
                    "No local deployments yet. The first successful deployment will appear here.",
                    "Локальних деплоїв ще немає. Перший успішний деплой з’явиться тут."));
                empty.AddToClassList("history-empty");
                _historyRoot.Add(empty);
                return;
            }

            var last = history[0];
            var row = new VisualElement();
            row.AddToClassList("history-row");

            var historyIcon = new VisualElement();
            historyIcon.AddToClassList("history-icon");
            historyIcon.AddToClassList(
                last.status == "Success" ? "success" : "fail");
            var historyIconCore = new VisualElement();
            historyIconCore.AddToClassList("history-icon-core");
            historyIcon.Add(historyIconCore);
            row.Add(historyIcon);

            var copy = new VisualElement();
            copy.AddToClassList("history-copy");
            AddHistoryCell(copy, last.version, "history-version");
            AddHistoryCell(
                copy,
                FormatHistoryDestination(last.platform, last.channel),
                "history-destination");
            AddHistoryCell(
                copy,
                $"{last.FormattedTime}  •  {last.FormattedSize}",
                "history-meta");
            row.Add(copy);
            _historyRoot.Add(row);
        }

        private static Label AddHistoryCell(
            VisualElement row,
            string text,
            string className)
        {
            var lbl = new Label(text);
            lbl.AddToClassList(className);
            row.Add(lbl);
            return lbl;
        }

        private VisualElement BuildSettingsStickyHeader()
        {
            var header = new VisualElement
            {
                name = "settings-sticky-header"
            };
            header.AddToClassList("settings-sticky-header");

            _settingsTabViewport = new VisualElement
            {
                name = "settings-tab-viewport"
            };
            _settingsTabViewport.AddToClassList("settings-tab-viewport");

            var tabBar = new VisualElement();
            tabBar.AddToClassList("settings-tab-bar");
            var settingsSubTabs = SettingsSubTabs;

            for (int i = 0; i < settingsSubTabs.Length; i++)
            {
                var idx = i;
                var button = new Button(() => ShowSettingsSubTab(idx))
                {
                    text = settingsSubTabs[i]
                };
                button.AddToClassList("settings-sub-tab");
                if (i == settingsSubTabs.Length - 1)
                    button.AddToClassList("last");
                button.EnableInClassList("selected", i == _settingsSubTab);
                tabBar.Add(button);
            }

            _settingsTabViewport.Add(tabBar);
            header.Add(_settingsTabViewport);
            PrepareToolkitSurface(header);
            return header;
        }

        private VisualElement BuildSettingsUI()
        {
            var root = new VisualElement();
            root.AddToClassList("dashboard");
            root.AddToClassList("settings-surface");

            _settingsContentContainer = new VisualElement();
            _settingsContentContainer.AddToClassList("settings-content");
            root.Add(_settingsContentContainer);

            _settingsSubPanels.Clear();
            for (int i = 0; i < SettingsSubTabs.Length; i++)
                _settingsSubPanels.Add(null);

            _settingsSubTab = Mathf.Clamp(
                _settingsSubTab,
                0,
                SettingsSubTabs.Length - 1);
            EnsureSettingsPanel(_settingsSubTab);

            root.AddToClassList("toolkit-surface");
            ApplyUiFont(root);
            return root;
        }

        private VisualElement EnsureSettingsPanel(int index)
        {
            if (index < 0 || index >= _settingsSubPanels.Count)
                return null;

            var existing = _settingsSubPanels[index];
            if (existing != null)
                return existing;

            var panel = index switch
            {
                0 => BuildSettingsAccountUI(),
                1 => BuildSettingsBuildUI(),
                2 => BuildSettingsTriggersUI(),
                3 => BuildSettingsToolsUI(),
                4 => BuildSettingsGitHubActionsUI(),
                5 => BuildSettingsUnityAutomationUI(),
                6 => BuildSettingsSystemUI(),
                _ => new VisualElement(),
            };
            panel.AddToClassList("settings-panel");
            panel.style.display = index == _settingsSubTab
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _settingsSubPanels[index] = panel;
            _settingsContentContainer.Add(panel);
            PrepareToolkitSurface(panel);
            return panel;
        }

        private VisualElement BuildSettingsSubIMGUI(Action drawMethod)
        {
            var container = new IMGUIContainer(drawMethod);
            container.AddToClassList("tab-content");
            return container;
        }

        private VisualElement BuildSettingsBuildUI()
        {
            var root = new VisualElement();

            AddSettingsSectionTitle(
                root,
                ItchDeployLocale._("Build settings", "Налаштування збірки"),
                "\u2699");

            var card = new VisualElement();
            card.AddToClassList("deploy-panel");
            card.AddToClassList("settings-build-card");

            var activeTarget = ActiveBuildTargetOverrideForTests ??
                               EditorUserBuildSettings.activeBuildTarget;
            var platformRow = new VisualElement();
            platformRow.AddToClassList("settings-control-row");
            var platformLabel = new Label(ItchDeployLocale._(
                "Active platform",
                "Активна платформа"));
            platformLabel.AddToClassList("settings-row-label");
            var platformValue = new Label(
                PlatformChannelMap.GetDisplayName(activeTarget));
            platformValue.AddToClassList("publish-value");
            platformValue.style.flexGrow = 1;
            var openBuildProfiles = new Button(OpenUnityBuildSettings)
            {
                text = ItchDeployLocale._(
                    "Change platform",
                    "Змінити платформу")
            };
            openBuildProfiles.AddToClassList("secondary-action");
            platformRow.Add(platformLabel);
            platformRow.Add(platformValue);
            platformRow.Add(openBuildProfiles);
            card.Add(platformRow);

            var moduleReady = BuildPlatformCatalog.TryValidateBuildConfiguration(
                activeTarget,
                out var buildReadinessIssue,
                out var moduleError);
            if (!moduleReady)
                moduleError = GetLocalizedBuildSupportError(
                    activeTarget,
                    buildReadinessIssue,
                    moduleError);
            var platformStatus = new VisualElement();
            platformStatus.AddToClassList("settings-inline-status");
            platformStatus.AddToClassList(
                moduleReady
                    ? "settings-inline-status--ready"
                    : "settings-inline-status--missing");
            var platformStatusDot = new VisualElement();
            platformStatusDot.AddToClassList("settings-inline-status-dot");
            var platformStatusText = new Label(moduleReady
                ? ItchDeployLocale._(
                    "Build Support is installed. Output: ",
                    "Модуль збірки встановлено. Результат: ") +
                  GetLocalizedArtifactDescription(activeTarget)
                : moduleError);
            platformStatusText.AddToClassList("settings-muted");
            platformStatusText.style.whiteSpace = WhiteSpace.Normal;
            platformStatus.Add(platformStatusDot);
            platformStatus.Add(platformStatusText);
            card.Add(platformStatus);

            if (buildReadinessIssue ==
                BuildPlatformCatalog.BuildReadinessIssue.UnsupportedAndroidInputHandling)
            {
                var inputActions = new VisualElement();
                inputActions.AddToClassList("settings-peer-actions");
                inputActions.AddToClassList("settings-context-actions");

                Button BuildInputAction(int value, string text, bool last = false)
                {
                    var action = new Button(() =>
                    {
                        if (!BuildPlatformCatalog.TrySetAndroidInputHandling(
                                value,
                                out var error))
                        {
                            Debug.LogError(
                                "[ItchDeploy] Could not update Active Input Handling: " +
                                error);
                            return;
                        }

                        ScheduleLocaleRebuild();
                    })
                    {
                        text = text
                    };
                    action.AddToClassList("secondary-action");
                    action.AddToClassList("settings-peer-action");
                    if (last)
                        action.AddToClassList("last");
                    return action;
                }

                inputActions.Add(BuildInputAction(
                    0,
                    ItchDeployLocale._(
                        "Use Input Manager (Old)",
                        "Використовувати Input Manager (Old)")));
                inputActions.Add(BuildInputAction(
                    1,
                    ItchDeployLocale._(
                        "Use Input System Package (New)",
                        "Використовувати Input System Package (New)"),
                    last: true));
                card.Add(inputActions);
            }

            var autoToggleRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale.AutoDeploy,
                _settings.autoDeployAfterBuild,
                out var autoToggle,
                "build-auto-deploy-toggle");
            autoToggle.tooltip = ItchDeployLocale._(
                "Publish immediately after a successful build.",
                "Автоматично публікувати після успішного завершення збірки.");
            autoToggleRow.tooltip = autoToggle.tooltip;
            autoToggle.RegisterValueChangedCallback(ev => { _settings.autoDeployAfterBuild = ev.newValue; EditorUtility.SetDirty(_settings); });
            card.Add(autoToggleRow);

            if (activeTarget == BuildTarget.WebGL)
            {
                var webglRow = new VisualElement();
                webglRow.style.flexDirection = FlexDirection.Row;
                webglRow.style.alignItems = Align.Center;
                webglRow.AddToClassList("settings-control-row");
                var webglLabel = new Label(ItchDeployLocale.WebGLCompression);
                webglLabel.AddToClassList("settings-row-label");
                webglRow.Add(webglLabel);
                var compressionChoices = new List<string>
                {
                    ItchDeployLocale._("Disabled", "Вимкнено"),
                    "Gzip",
                    "Brotli"
                };

                if (WebGLHelper.GetCurrentCompression() != _settings.webglCompression)
                    WebGLHelper.SetCompression(_settings.webglCompression);

                var webglDropdown = new ItchDeployChoiceField(
                    string.Empty,
                    compressionChoices,
                    Mathf.Clamp((int)_settings.webglCompression, 0, compressionChoices.Count - 1));
                webglDropdown.name = "build-webgl-compression-dropdown";
                var compressionTooltip = ItchDeployLocale._(
                    "Compression method for WebGL builds.",
                    "Метод стиснення для WebGL-збірок.");
                webglDropdown.tooltip = compressionTooltip;
                webglDropdown.style.flexGrow = 1;

                webglDropdown.SelectionChanged += (selectedIndex, _) =>
                {
                    if (selectedIndex < 0)
                        return;

                    ApplyWebGLCompressionSetting(
                        (WebGLHelper.CompressionFormat)selectedIndex);
                };
                webglRow.Add(webglDropdown);
                card.Add(webglRow);
            }
            else if (activeTarget == BuildTarget.Android)
            {
                var androidRow = new VisualElement();
                androidRow.AddToClassList("settings-control-row");
                var androidLabel = new Label(ItchDeployLocale._(
                    "Android format",
                    "Формат Android"));
                androidLabel.AddToClassList("settings-row-label");
                var androidChoices = new List<string>
                {
                    "APK — itch.io",
                    "AAB — Google Play"
                };
                var androidDropdown = new ItchDeployChoiceField(
                    string.Empty,
                    androidChoices,
                    EditorUserBuildSettings.buildAppBundle ? 1 : 0);
                androidDropdown.name = "build-android-format-dropdown";
                androidDropdown.style.flexGrow = 1;
                androidDropdown.tooltip = ItchDeployLocale._(
                    "APK can be deployed to itch.io. AAB is produced as a build-only artifact for Google Play.",
                    "APK можна завантажити на itch.io. AAB створюється лише як артефакт для Google Play.");
                androidDropdown.SelectionChanged += (selectedIndex, _) =>
                {
                    EditorUserBuildSettings.buildAppBundle =
                        selectedIndex == 1;
                    var selectedSettingsTab = _settingsSubTab;
                    rootVisualElement.schedule.Execute(() =>
                    {
                        CreateGUI();
                        ShowTab(1);
                        ShowSettingsSubTab(selectedSettingsTab);
                        Repaint();
                    }).StartingIn(1);
                };
                androidRow.Add(androidLabel);
                androidRow.Add(androidDropdown);
                card.Add(androidRow);
            }

            if (!BuildPlatformCatalog.CanDeployCurrentArtifactToItch(
                    activeTarget,
                    out _))
            {
                var buildOnlyInfo = new Label(
                    GetLocalizedBuildOnlyReason(activeTarget));
                buildOnlyInfo.AddToClassList("settings-muted");
                buildOnlyInfo.style.whiteSpace = WhiteSpace.Normal;
                card.Add(buildOnlyInfo);
            }

            root.Add(card);

            var autoVersion = AutoVersion.CreateUI();
            autoVersion.AddToClassList("settings-feature-card");
            root.Add(autoVersion);
            AddSettingsSpacer(root);
            var sceneValidation = SceneValidator.CreateUI();
            sceneValidation.AddToClassList("settings-feature-card");
            root.Add(sceneValidation);
            AddSettingsSpacer(root);
            var buildNamer = BuildNamer.CreateUI();
            buildNamer.AddToClassList("settings-feature-card");
            root.Add(buildNamer);

            return root;
        }

        private void ApplyWebGLCompressionSetting(
            WebGLHelper.CompressionFormat compression)
        {
            _settings.webglCompression = compression;
            EditorUtility.SetDirty(_settings);
            WebGLHelper.SetCompression(compression);
            AssetDatabase.SaveAssets();
        }

        private VisualElement BuildSettingsToolsUI()
        {
            var root = new VisualElement();

            AddSettingsSectionTitle(root, "Butler", "\u2692");

            var card = new VisualElement();
            card.AddToClassList("deploy-panel");

            var refreshCard = new VisualElement();
            card.Add(refreshCard);
            root.Add(card);
            bool? renderedAvailability = null;

            void RefreshButlerCard()
            {
                var available = ButlerBridge.IsButlerAvailable;
                if (renderedAvailability == available && refreshCard.childCount > 0)
                    return;

                renderedAvailability = available;
                _butlerCached = available;
                refreshCard.Clear();
                if (available)
                {
                    var pathLabel = new Label(ItchDeployLocale.Path + ": " + ButlerBridge.ButlerPath);
                    pathLabel.AddToClassList("settings-muted");
                    refreshCard.Add(pathLabel);

                    var verLabel = new Label(
                        ItchDeployLocale._("Version", "Версія") + ": " +
                        (ButlerUpdater.GetLocalVersion() ?? "?"));
                    verLabel.AddToClassList("settings-muted");
                    refreshCard.Add(verLabel);

                    var btnRow = new VisualElement();
                    btnRow.style.flexDirection = FlexDirection.Row;
                    btnRow.AddToClassList("settings-action-row");
                    var checkBtn = new Button(() => ButlerUpdater.ManualCheck());
                    checkBtn.text = ItchDeployLocale.CheckUpdates;
                    checkBtn.AddToClassList("secondary-action");
                    btnRow.Add(checkBtn);
                    var reinstallBtn = new Button(() =>
                    {
                        ButlerInstaller.DeleteInstalled();
                        ButlerBridge.RefreshPath();
                        ButlerBridge.TryAutoInstall();
                    });
                    reinstallBtn.text = ItchDeployLocale._(
                        "Reinstall", "Перевстановити");
                    reinstallBtn.AddToClassList("secondary-action");
                    btnRow.Add(reinstallBtn);
                    refreshCard.Add(btnRow);
                }
                else
                {
                    var help = new Label(ItchDeployLocale.ButlerNotFound);
                    help.style.color = ItchDeployUI.StateToColor(ItchDeployUI.State.Warn);
                    help.style.fontSize = 11;
                    help.style.whiteSpace = WhiteSpace.Normal;
                    help.style.marginBottom = 6;
                    refreshCard.Add(help);

                    var installBtn = new Button(() => ButlerBridge.TryAutoInstall());
                    installBtn.text = ItchDeployLocale.InstallButler;
                    installBtn.AddToClassList("settings-wide-action");
                    refreshCard.Add(installBtn);
                }
            }
            RefreshButlerCard();

            AddSettingsSpacer(root);
            var itchIgnore = ItchIgnore.CreateUI();
            itchIgnore.AddToClassList("settings-feature-card");
            root.Add(itchIgnore);
            AddSettingsSpacer(root);
            var buildArchive = BuildArchive.CreateUI();
            buildArchive.AddToClassList("settings-feature-card");
            root.Add(buildArchive);
            AddSettingsSpacer(root);
            var versionInfo = VersionInfoWriter.CreateUI();
            versionInfo.AddToClassList("settings-feature-card");
            root.Add(versionInfo);
            AddSettingsSpacer(root);
            var backups = BackupManager.CreateUI();
            backups.AddToClassList("settings-feature-card");
            root.Add(backups);

            root.schedule.Execute(() =>
            {
                if (root.style.display != DisplayStyle.None)
                    RefreshButlerCard();
            }).Every(2000);

            return root;
        }

        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
        }

        private VisualElement BuildSettingsSystemUI()
        {
            var root = new VisualElement();
            AddSettingsSectionTitle(
                root,
                ItchDeployLocale._("Interface", "Інтерфейс"),
                "⚙");

            var interfaceCard = new VisualElement();
            interfaceCard.AddToClassList("deploy-panel");
            var languages = new List<string>
            {
                ItchDeployLocale._("Ukrainian", "Українська"),
                "English"
            };
            var languageIndex = ItchDeployLocale.Current == ItchDeployLocale.Lang.Ukrainian ? 0 : 1;
            var language = new ItchDeployChoiceField(
                ItchDeployLocale.Language,
                languages,
                languageIndex);
            language.labelElement.style.width = 120;
            language.labelElement.style.minWidth = 120;
            language.tooltip = ItchDeployLocale._(
                "Change the plugin interface language.",
                "Змінити мову інтерфейсу плагіна.");
            language.SelectionChanged += (_, _) =>
            {
                var selectedLanguage = language.index == 0
                    ? ItchDeployLocale.Lang.Ukrainian
                    : ItchDeployLocale.Lang.English;
                if (selectedLanguage == ItchDeployLocale.Current)
                    return;

                ItchDeployLocale.Current = selectedLanguage;
                ScheduleLocaleRebuild();
            };
            interfaceCard.Add(language);
            root.Add(interfaceCard);

            AddSettingsSpacer(root);
            AddSettingsSectionTitle(
                root,
                ItchDeployLocale._("itch.io connection", "Підключення itch.io"),
                string.Empty);
            var oauthCard = new VisualElement();
            oauthCard.AddToClassList("deploy-panel");
            oauthCard.name = "itch-system-login-card";

            var oauthTitle = new Label(ItchDeployLocale._(
                "Browser sign-in",
                "Вхід через браузер"));
            oauthTitle.AddToClassList("settings-card-title");
            oauthCard.Add(oauthTitle);

            var oauthHelp = new Label(ItchDeployLocale._(
                "Sign in once in the browser. Butler stores the authorization locally, and the plugin verifies access to the current deployment target.",
                "Увійдіть один раз у браузері. Butler збереже авторизацію локально, а плагін перевірить доступ до поточної цілі деплою."));
            oauthHelp.AddToClassList("settings-card-help");
            oauthCard.Add(oauthHelp);

            _settingsSystemLoginState = CreateItchAccountState(
                "itch-system-login-state",
                out _settingsSystemLoginTitle,
                out _settingsSystemLoginCopy);
            _settingsSystemLoginState.AddToClassList("settings-system-login-state");
            oauthCard.Add(_settingsSystemLoginState);

            _settingsSystemConnectButton = new Button(ConnectItchAccount)
            {
                name = "itch-system-connect"
            };
            _settingsSystemConnectButton.AddToClassList("settings-primary-action");
            oauthCard.Add(_settingsSystemConnectButton);
            RefreshItchAccountControls();
            root.Add(oauthCard);

            AddSettingsSpacer(root);
            AddSettingsSectionTitle(
                root,
                ItchDeployLocale._("Project configuration file", "Файл конфігурації проєкту"),
                string.Empty);
            var settingsCard = new VisualElement();
            settingsCard.AddToClassList("deploy-panel");
            settingsCard.AddToClassList("settings-config-file-card");
            settingsCard.name = "settings-config-file-card";

            var settingsHelp = new Label(ItchDeployLocale._(
                "The .itchdeploy.json file transfers the itch.io target, platform channels, build options, and CI/CD preferences between projects. API keys, tokens, and other secrets are never included.",
                "Файл .itchdeploy.json переносить між проєктами ціль itch.io, канали платформ, параметри збірки та CI/CD. API-ключі, токени й інші секрети до нього не потрапляють."));
            settingsHelp.AddToClassList("settings-card-help");
            settingsHelp.name = "settings-config-file-help";
            settingsCard.Add(settingsHelp);

            var currentFile = new VisualElement();
            currentFile.AddToClassList("settings-config-file-summary");
            var currentFileCaption = new Label(ItchDeployLocale._(
                "Current file",
                "Поточний файл"));
            currentFileCaption.AddToClassList("settings-config-file-caption");
            var currentFileValue = new Label(".itchdeploy.json");
            currentFileValue.AddToClassList("settings-config-file-value");
            currentFileValue.tooltip = ItchDeployConfig.FilePath;
            currentFile.Add(currentFileCaption);
            currentFile.Add(currentFileValue);
            settingsCard.Add(currentFile);

            var importHelp = new Label(ItchDeployLocale._(
                "Import: choose a .itchdeploy.json file created by ItchDeploy. After confirmation it replaces the current project configuration; an existing file is backed up first.",
                "Імпорт: виберіть файл .itchdeploy.json, створений ItchDeploy. Після підтвердження він замінить конфігурацію цього проєкту; наявний файл спочатку буде збережено як резервну копію."));
            importHelp.AddToClassList("settings-muted");
            importHelp.AddToClassList("settings-config-file-note");
            importHelp.name = "settings-config-file-import-help";
            settingsCard.Add(importHelp);

            var actions = new VisualElement();
            actions.AddToClassList("settings-peer-actions");
            actions.AddToClassList("settings-config-file-actions");
            var importSettings = new Button(ItchDeploySettingsIO.ImportFromJson)
            {
                text = ItchDeployLocale._(
                    "Choose file to import",
                    "Вибрати файл для імпорту"),
                name = "settings-config-file-import"
            };
            importSettings.AddToClassList("secondary-action");
            importSettings.AddToClassList("settings-peer-action");
            actions.Add(importSettings);
            var exportSettings = new Button(ItchDeploySettingsIO.ExportToJson)
            {
                text = ItchDeployLocale._(
                    "Save a JSON copy",
                    "Зберегти копію JSON"),
                name = "settings-config-file-export"
            };
            exportSettings.AddToClassList("secondary-action");
            exportSettings.AddToClassList("settings-peer-action");
            exportSettings.AddToClassList("last");
            actions.Add(exportSettings);
            settingsCard.Add(actions);
            root.Add(settingsCard);

            AddSettingsSpacer(root);
            AddSettingsSectionTitle(
                root,
                ItchDeployLocale._("Plugin version", "Версія плагіна"),
                string.Empty);
            var updateCard = new VisualElement();
            updateCard.AddToClassList("deploy-panel");

            var installed = new Label(ItchDeployLocale._("Installed: ", "Встановлено: ") +
                                      ItchDeployVersion.Version);
            installed.AddToClassList("settings-card-help");
            updateCard.Add(installed);

            if (PluginUpdater.HasUpdate)
            {
                var available = new Label(
                    ItchDeployLocale._("Available: ", "Доступно: ") + PluginUpdater.LatestVersion);
                available.AddToClassList("settings-card-help");
                updateCard.Add(available);
            }

            var updateRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._("Check for updates daily", "Щодня перевіряти оновлення"),
                PluginUpdater.AutoCheck,
                out var updateToggle,
                "system-update-check");
            updateToggle.tooltip = ItchDeployLocale._(
                "Contacts github.com once a day to compare release tags. Nothing about your project is sent.",
                "Раз на добу звертається до github.com, щоб порівняти теги релізів. Дані вашого проєкту не надсилаються.");
            updateToggle.RegisterValueChangedCallback(ev => PluginUpdater.AutoCheck = ev.newValue);
            updateCard.Add(updateRow);

            var checkNow = ItchDeployUI.SecondaryButton(
                ItchDeployLocale.CheckUpdates,
                () => PluginUpdater.CheckForUpdates(interactive: true));
            checkNow.AddToClassList("settings-wide-secondary");
            updateCard.Add(checkNow);
            root.Add(updateCard);

            return root;
        }

        private void ShowSettingsSubTab(int idx)
        {
            if (idx < 0 || idx >= _settingsSubPanels.Count)
                return;

            _settingsSubTab = idx;
            var panel = EnsureSettingsPanel(idx);
            for (int i = 0; i < _settingsSubPanels.Count; i++)
            {
                if (_settingsSubPanels[i] != null)
                    _settingsSubPanels[i].style.display =
                        i == idx ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var tabButtons = rootVisualElement
                .Query<Button>(className: "settings-sub-tab")
                .ToList();
            for (int i = 0; i < tabButtons.Count; i++)
                tabButtons[i].EnableInClassList("selected", i == idx);

            if (panel is IMGUIContainer imGui)
                imGui.MarkDirtyRepaint();
        }

        private void ScheduleLocaleRebuild()
        {
            _selectedTab = 1;
            _settingsSubTab = Mathf.Clamp(_settingsSubTab, 0, _settingsSubPanels.Count - 1);

            if (_localeRebuildPending)
                return;

            _localeRebuildPending = true;
            rootVisualElement.schedule.Execute(() =>
            {
                _localeRebuildPending = false;
                if (this == null || rootVisualElement.panel == null)
                    return;

                CreateGUI();
                ShowTab(_selectedTab);
                ShowSettingsSubTab(_settingsSubTab);
                Repaint();
            }).StartingIn(1);
        }

        private void RefreshSettingsUI()
        {
            RefreshItchAccountControls();
            RefreshItchPageConnectionCard();
        }

        private static void AddSettingsSectionTitle(VisualElement parent, string title, string icon)
        {
            var sectionTitle = new VisualElement();
            sectionTitle.AddToClassList("settings-section-title");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("settings-inline-heading");
            sectionTitle.Add(titleLabel);
            parent.Add(sectionTitle);
        }

        private static void AddSettingsSpacer(VisualElement parent)
        {
            var spacer = new VisualElement();
            spacer.style.height = 10;
            parent.Add(spacer);
        }

        private void OnDashboardUpdate()
        {
            if (_dashboardUI == null || _publishUI == null) return;
            var dashboardVisible = _dashboardUI.style.display != DisplayStyle.None;
            var publishVisible = _publishUI.style.display != DisplayStyle.None;
            if (!dashboardVisible && !publishVisible) return;
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastDashboardRefresh < DashboardPollIntervalSeconds) return;
            _lastDashboardRefresh = now;

            _butlerCached = ButlerBridge.IsButlerAvailable;
            if (dashboardVisible) RefreshDashboard();
            if (publishVisible) RefreshPublishUI();
        }

        private static string[] SettingsSubTabs => new[]
        {
            ItchDeployLocale._("itch.io project", "Проєкт itch.io"),
            ItchDeployLocale._("Build", "Збірка"),
            ItchDeployLocale._("Triggers", "Тригери"),
            "Butler",
            "GitHub Actions",
            ItchDeployLocale._("Unity Automation", "Unity Automation"),
            ItchDeployLocale._("System", "Система")
        };

        private string ResolveChannel(BuildTarget target)
            => PlatformChannelMap.ResolveChannel(_settings, target);
    }
}
