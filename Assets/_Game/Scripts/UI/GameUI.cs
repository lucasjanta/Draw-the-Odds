using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Monta o Canvas inteiro em código e controla as telas (menu principal, pausa, banner, derrota, vitória).
public class GameUI : MonoBehaviour
{
    private GameManager gm;
    private Image slowTint;
    private CanvasGroup bannerGroup, endGroup;
    private MainMenu mainMenu;
    private LeaderboardPanel victoryBoard, endlessBoard;
    private GameObject endButtons;
    private Button continueButton, rankingButton;
    private TextMeshProUGUI continueLabel;
    public QuickTips Tips { get; private set; }
    public bool GameOverBoardOpen => endlessBoard != null && endlessBoard.gameObject.activeSelf;
    public PauseMenu Pause { get; private set; }
    public DifficultyPanel Difficulty { get; private set; }
    public CardCurtain Curtain { get; private set; }
    public StoryDialog Story { get; private set; }
    public JokerPickPanel JokerPick { get; private set; }
    public RaiseUi RaiseUi { get; private set; }
    public EndingPanel Ending { get; private set; }
    private TextMeshProUGUI bannerTitle, bannerSub, endTitle, endBody;
    private Coroutine bannerRoutine;
    private CanvasGroup promptGroup;
    private RectTransform promptRect;
    private TextMeshProUGUI promptText, promptContinue;
    private Button promptBack;
    private bool promptVisible;
    private float promptSlide;               // 0 = no lugar, 1 = fora (à direita)

    private const float PromptWidth = 250f;
    private const float PromptSlideDistance = 60f;

    // Clique na seta "◄" do tutorial (volta uma dica).
    public event System.Action OnPromptBack;

    public static GameUI Build(GameManager gm)
    {
        var go = new GameObject("UI", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvas.pixelPerfect = false;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 360f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>(); // sem isto os botões dos menus não recebem clique

        var ui = go.AddComponent<GameUI>();
        ui.gm = gm;
        Transform root = go.transform;

        // Tinta azulada durante a câmera lenta
        ui.slowTint = UIFactory.Image("SlowTint", root, new Color(0.02f, 0.02f, 0.08f, 0f));
        ui.slowTint.rectTransform.Stretch();

        LowHpVignette.Create(root, gm);
        HandView.Create(root, gm);
        // Painéis do HUD (cantos de cima) ficam dentro da área segura do celular (fora do furo da câmera).
        var safe = UIFactory.Rect("SafeHUD", root).Stretch();
        SafeArea.Add(safe);
        PlayerHUD.Create(safe, gm);
        JokerBelt.Create(safe, gm); // coringas da run, embaixo do painel do player
        BossHUD.Create(safe, gm);
        FusionPanel.Create(root, gm);

        // Banner central ("BOSS 1/3 — JACK")
        var banner = UIFactory.Rect("Banner", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 80f)); // centro exato da tela
        ui.bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        ui.bannerGroup.alpha = 0f;
        var bannerBack = UIFactory.Image("Back", banner, new Color(0f, 0f, 0f, 0.55f));
        bannerBack.rectTransform.Stretch();
        ui.bannerSub = UIFactory.Text("Sub", banner, "", 12f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
        ui.bannerSub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(600f, 16f));
        ui.bannerTitle = UIFactory.Text("Title", banner, "", 34f, Color.white, TextAlignmentOptions.Center);
        // Títulos dos banners (nomes dos chefes) na fonte do título, se existir
        if (gm.Config.TitleFont != null) ui.bannerTitle.font = gm.Config.TitleFont;
        else ui.bannerTitle.fontStyle = FontStyles.Bold;
        ui.bannerTitle.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(600f, 44f));

        ui.BuildPrompt(root);
        ui.Tips = QuickTips.Create(root, gm);
        ui.endGroup = BuildEndPanel(root, out ui.endTitle, out ui.endBody);
        ui.BuildEndButtons(ui.endGroup.transform);
        ui.victoryBoard = LeaderboardPanel.Create(root, "MAIN MENU", gm.BackToMainMenu);
        ui.endlessBoard = LeaderboardPanel.Create(root, "BACK", () => ui.endlessBoard.Hide());
        TouchControls.Create(root, gm);
        ui.RaiseUi = RaiseUi.Create(root, gm);
        ui.mainMenu = MainMenu.Create(root, gm);
        ui.Difficulty = DifficultyPanel.Create(root);
        ui.JokerPick = JokerPickPanel.Create(root);
        ui.Ending = EndingPanel.Create(root);
        ui.Story = StoryDialog.Create(root, gm.Config); // abaixo da pausa: o menu de pausa abre por cima do diálogo
        ui.Pause = PauseMenu.Create(root, gm);
        ui.Curtain = CardCurtain.Create(root, gm.Config);
        EnsureEventSystem();
        return ui;
    }

    // Botões e sliders precisam de um EventSystem na cena (com o módulo do Input System novo).
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    private static CanvasGroup BuildEndPanel(Transform root, out TextMeshProUGUI title, out TextMeshProUGUI body)
    {
        var back = UIFactory.Image("End", root, new Color(0.04f, 0.02f, 0.09f, 0.85f));
        back.rectTransform.Stretch();
        var group = back.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        title = UIFactory.Text("Title", back.transform, "", 36f, Color.white, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(600f, 50f));
        body = UIFactory.Text("Body", back.transform, "", 12f, Color.white, TextAlignmentOptions.Top);
        body.lineSpacing = 6f;
        body.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 15f), new Vector2(600f, 140f));
        return group;
    }

    // Caixa de instruções do tutorial: canto inferior DIREITO (longe da mão e do chefe),
    // entra deslizando da direita com fade. Tem uma seta "◄" para voltar uma dica.
    private void BuildPrompt(Transform root)
    {
        var back = UIFactory.Image("Prompt", root, new Color(0.05f, 0.03f, 0.12f, 0.92f));
        promptRect = back.rectTransform;
        promptGroup = back.gameObject.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
        promptGroup.blocksRaycasts = false;
        promptSlide = 1f;
        // Faixa amarela no topo da caixa (destaque)
        var stripe = UIFactory.Image("Stripe", back.transform, new Color32(0xFF, 0xD4, 0x47, 0xFF));
        stripe.rectTransform.anchorMin = new Vector2(0f, 1f);
        stripe.rectTransform.anchorMax = new Vector2(1f, 1f);
        stripe.rectTransform.pivot = new Vector2(0.5f, 1f);
        stripe.rectTransform.sizeDelta = new Vector2(0f, 2f);
        stripe.rectTransform.anchoredPosition = Vector2.zero;

        promptText = UIFactory.Text("Text", back.transform, "", 9f, Color.white, TextAlignmentOptions.TopLeft);
        promptText.textWrappingMode = TextWrappingModes.Normal;
        promptText.lineSpacing = 4f;
        promptText.rectTransform.Stretch();
        promptText.rectTransform.offsetMin = new Vector2(24f, 16f);
        promptText.rectTransform.offsetMax = new Vector2(-8f, -8f);

        promptContinue = UIFactory.Text("Continue", back.transform, "CLICK TO CONTINUE", 8f, new Color32(0xFF, 0xD4, 0x47, 0xFF), TextAlignmentOptions.BottomRight);
        promptContinue.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 4f), new Vector2(200f, 12f));
        promptContinue.gameObject.AddComponent<Blink>();

        // Seta para voltar uma dica (botão pequeno no canto esquerdo da caixa)
        promptBack = UIFactory.Button("Back", back.transform, "◄", new Vector2(16f, 16f), () => OnPromptBack?.Invoke(), 9f);
        promptBack.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -6f), new Vector2(16f, 16f));
    }

    // Com os controles de toque, o canto de baixo à direita é dos botões PULO/DASH: a caixa iria para trás deles.
    private const float PromptTopY = -44f; // abaixo do botão de pausa
    private bool promptOnTop;

    private void PlacePrompt(float height)
    {
        promptOnTop = VirtualInput.Active;
        if (promptOnTop)
            promptRect.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, PromptTopY), new Vector2(PromptWidth, height));
        else
            promptRect.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 8f), new Vector2(PromptWidth, height));
    }

    public void ShowPrompt(string text, bool clickToContinue, bool low = false)
    {
        // low era usado para fugir do painel de fusão; no canto de baixo a caixa já não atrapalha.
        promptText.text = text;
        promptContinue.gameObject.SetActive(clickToContinue);
        promptText.rectTransform.offsetMin = new Vector2(24f, clickToContinue ? 16f : 7f);

        // Altura = altura que o texto precisa com a largura da caixa (quebra de linha automática)
        float textWidth = PromptWidth - 24f - 8f;
        float textHeight = promptText.GetPreferredValues(text, textWidth, 0f).y;
        float height = Mathf.Max(30f, textHeight + 15f + (clickToContinue ? 10f : 0f));
        PlacePrompt(height);

        // Texto novo: recomeça a animação de entrada (vem da direita, aparecendo)
        promptVisible = true;
        promptSlide = 1f;
        promptGroup.alpha = 0f;
        promptBack.gameObject.SetActive(CanGoBack);
    }

    // O TutorialDirector diz se existe uma dica anterior.
    public bool CanGoBack { get; set; }

    public void HidePrompt() => promptVisible = false;

    public void ShowTitle(bool show) => mainMenu.Show(show);

    public void ShowBanner(string subtitle, string title, float duration)
    {
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerRoutine(subtitle, title, duration));
    }

    private IEnumerator BannerRoutine(string subtitle, string title, float duration)
    {
        bannerSub.text = subtitle;
        bannerTitle.text = title;
        var rt = (RectTransform)bannerGroup.transform;
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            bannerGroup.alpha = t / 0.2f;
            rt.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, t / 0.2f), 1f);
            yield return null;
        }
        bannerGroup.alpha = 1f;
        rt.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(duration);
        yield return Fade(bannerGroup, 0f, 0.3f);
    }

    // Botões da derrota: CONTINUAR (anúncio) e RECOMEÇAR do mesmo tamanho, lado a lado
    // (regra dos portais: a opção sem anúncio nunca é menor), MENU embaixo e RANKING no infinito.
    private void BuildEndButtons(Transform parent)
    {
        var row = UIFactory.Rect("EndButtons", parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(380f, 90f));
        endButtons = row.gameObject;
        var size = new Vector2(176f, 28f);

        continueButton = UIFactory.Button("Continue", row, "", size, gm.ContinueWithAd, 11f);
        continueButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-94f, 0f), size);
        continueLabel = continueButton.transform.Find("Label").GetComponent<TextMeshProUGUI>().NoLoc();

        var restart = UIFactory.Button("Restart", row, "", size, gm.RestartRun, 11f);
        restart.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(94f, 0f), size);
        restartLabel = restart.transform.Find("Label").GetComponent<TextMeshProUGUI>().NoLoc();

        var menu = UIFactory.Button("Menu", row, "MAIN MENU", new Vector2(120f, 22f), gm.BackToMainMenu, 10f);
        menu.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-66f, -40f), new Vector2(120f, 22f));
        rankingButton = UIFactory.Button("Ranking", row, "RANKING", new Vector2(120f, 22f), ShowEndlessBoard, 10f);
        rankingButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(66f, -40f), new Vector2(120f, 22f));
        endButtons.SetActive(false);
    }

    private TextMeshProUGUI restartLabel;

    public void ShowGameOver(GameManager gm)
    {
        endTitle.text = "<color=#E84A5F>YOU FOLDED</color>";
        if (GameManager.EndlessMode)
        {
            int r = gm.EndlessRound, b = gm.EndlessBeaten;
            bool one = b == 1;
            endBody.text = Loc.Pick(
                $"ENDLESS - ROUND {r}\n<color=#FFD447>{b} {(one ? "BOSS" : "BOSSES")} BEATEN</color>",
                $"INFINITO - RODADA {r}\n<color=#FFD447>{b} {(one ? "CHEFE VENCIDO" : "CHEFES VENCIDOS")}</color>",
                $"INFINITO - RONDA {r}\n<color=#FFD447>{b} {(one ? "JEFE VENCIDO" : "JEFES VENCIDOS")}</color>",
                $"INFINI - MANCHE {r}\n<color=#FFD447>{b} {(one ? "BOSS VAINCU" : "BOSS VAINCUS")}</color>",
                $"ENDLOS - RUNDE {r}\n<color=#FFD447>{b} {(one ? "BOSS BESIEGT" : "BOSSE BESIEGT")}</color>",
                $"INFINITO - ROUND {r}\n<color=#FFD447>{b} BOSS {(one ? "SCONFITTO" : "SCONFITTI")}</color>");
        }
        else
        {
            string boss = Loc.T(gm.CurrentBossData.DisplayName);
            endBody.text = Loc.Pick($"The {gm.CurrentBossData.DisplayName} called your bluff.", $"O {boss} pagou pra ver o seu blefe.",
                                    $"{boss} descubrió tu farol.", $"{boss} a démasqué ton bluff.",
                                    $"{boss} hat deinen Bluff durchschaut.", $"{boss} ha visto il tuo bluff.");
        }
        // Fichas ganhas nesta run (ficam guardadas para a taverna mesmo perdendo)
        if (gm.Stats.chipsEarned > 0) endBody.text += "\n" + $"<size=10><color=#FFD447>● +{gm.Stats.chipsEarned}</color></size>";
        restartLabel.text = GameManager.EndlessMode
            ? Loc.Pick("RESTART (ROUND 1)", "RECOMEÇAR (RODADA 1)", "REINICIAR (RONDA 1)", "RECOMMENCER (MANCHE 1)", "NEUSTART (RUNDE 1)", "RICOMINCIA (ROUND 1)")
            : Loc.Pick("RESTART FROM THE JACK", "RECOMEÇAR DO VALETE", "REINICIAR DESDE LA J", "RECOMMENCER AU VALET", "NEUSTART BEIM BUBEN", "RICOMINCIA DAL FANTE");
        endButtons.SetActive(true);
        RefreshGameOverButtons();
        StartCoroutine(Fade(endGroup, 1f, 0.5f));
    }

    // CONTINUAR só aparece com anúncio disponível e uma vez por chefe/rodada.
    public void RefreshGameOverButtons()
    {
        bool canContinue = gm.CanContinue;
        continueButton.gameObject.SetActive(canContinue);
        continueLabel.text = GameManager.EndlessMode
            ? Loc.Pick("► AD: RETRY THIS ROUND", "► ANÚNCIO: REPETIR RODADA", "► ANUNCIO: REPETIR RONDA",
                       "► PUB : REJOUER LA MANCHE", "► WERBUNG: RUNDE WIEDERHOLEN", "► PUBBLICITÀ: RIPROVA IL ROUND")
            : Loc.Pick("► AD: RETRY THIS BOSS", "► ANÚNCIO: REPETIR CHEFE", "► ANUNCIO: REPETIR JEFE",
                       "► PUB : REJOUER CE BOSS", "► WERBUNG: BOSS WIEDERHOLEN", "► PUBBLICITÀ: RIPROVA IL BOSS");
        // Sem o CONTINUAR, o RECOMEÇAR vai para o centro.
        var restartRect = (RectTransform)restartLabel.transform.parent;
        restartRect.anchoredPosition = new Vector2(canContinue ? 94f : 0f, 0f);
        rankingButton.gameObject.SetActive(GameManager.EndlessMode && gm.EndlessBeaten > 0);
        var menuRect = (RectTransform)endButtons.transform.Find("Menu");
        menuRect.anchoredPosition = new Vector2(rankingButton.gameObject.activeSelf ? -66f : 0f, -40f);
    }

    // Infinito: ranking próprio (chefes vencidos). Abre por cima da tela de derrota.
    private void ShowEndlessBoard()
    {
        int time = Mathf.Max(1, Mathf.RoundToInt(gm.Stats.Elapsed));
        var run = new LeaderboardClient.Entry
        {
            time_sec = time,
            cards_thrown = gm.Stats.cardsThrown,
            criticals = gm.Stats.criticals,
            fusions = gm.Stats.fusions,
            damage_taken = gm.Stats.damageTaken,
            deaths = gm.Stats.deaths,
            score = LeaderboardClient.EndlessScore(gm.EndlessBeaten, time),
        };
        endlessBoard.ShowEndless(run);
    }

    public void ShowVictory(GameManager.RunStats stats, float seconds, global::Difficulty difficulty)
    {
        // Título no topo e, embaixo, o ranking com a run do jogador (nome se entrar no top 10).
        // Cada modo tem o seu ranking.
        string mode = difficulty == global::Difficulty.Hardcore ? "\n<size=10><color=#E84A5F>HARDCORE CLEAR!</color></size>"
            : difficulty == global::Difficulty.Easy ? "\n<size=10><color=#3CC86E>EASY MODE</color></size>" : "";
        endTitle.text = "<color=#FFD447>YOU BEAT THE ODDS!</color>" + mode;
        endTitle.fontSize = 24f;
        endTitle.rectTransform.anchoredPosition = new Vector2(0f, 156f);
        endBody.text = "";
        StartCoroutine(Fade(endGroup, 1f, 0.6f));

        int time = Mathf.Max(1, Mathf.RoundToInt(seconds));
        var run = new LeaderboardClient.Entry
        {
            time_sec = time,
            cards_thrown = stats.cardsThrown,
            criticals = stats.criticals,
            fusions = stats.fusions,
            damage_taken = stats.damageTaken,
            deaths = stats.deaths,
            score = LeaderboardClient.Score(time, stats.cardsThrown, stats.criticals),
        };
        victoryBoard.Show(run, difficulty);
        ((RectTransform)victoryBoard.transform).anchoredPosition = new Vector2(0f, -22f);
    }

    private static IEnumerator Fade(CanvasGroup group, float target, float duration)
    {
        float start = group.alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        group.alpha = target;
    }

    private CanvasScaler scaler;

    // A UI foi desenhada para 640×360. Tela mais larga que 16:9 (celular 20:9): segue a ALTURA, senão
    // os menus em coluna passam da borda de baixo. Nas outras telas fica como sempre foi (meio a meio).
    private void FitCanvas()
    {
        if (scaler == null) scaler = GetComponent<CanvasScaler>();
        float match = (float)Screen.width / Mathf.Max(1, Screen.height) > 16f / 9f + 0.01f ? 1f : 0.5f;
        if (scaler.matchWidthOrHeight != match) scaler.matchWidthOrHeight = match;
    }

    private void Update()
    {
        FitCanvas();
        // Entrada/saída da caixa do tutorial: fade + desliza para a direita (tempo real)
        float dt = Time.unscaledDeltaTime;
        promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, promptVisible ? 1f : 0f, dt * 5f);
        promptSlide = Mathf.MoveTowards(promptSlide, promptVisible ? 0f : 1f, dt * 4f);
        float eased = promptSlide * promptSlide; // desacelera ao chegar
        // No toque a caixa fica no topo (embaixo do botão de pausa) e desce; no PC, canto de baixo vindo da direita.
        bool top = VirtualInput.Active;
        if (top != promptOnTop) PlacePrompt(promptRect.sizeDelta.y);
        promptRect.anchoredPosition = top
            ? new Vector2(0f, PromptTopY + eased * PromptSlideDistance)
            : new Vector2(-8f + eased * PromptSlideDistance, 8f);
        promptGroup.blocksRaycasts = promptVisible; // a seta só recebe clique com a caixa à mostra

        float target = gm.Fusion.Active ? 0.5f : 0f; // escurece a tela com o tempo parado
        var c = slowTint.color;
        c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 0.8f);
        slowTint.color = c;
    }
}
