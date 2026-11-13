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
    private LeaderboardPanel victoryBoard;
    public PauseMenu Pause { get; private set; }
    public DifficultyPanel Difficulty { get; private set; }
    public CardCurtain Curtain { get; private set; }
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
        PlayerHUD.Create(root, gm);
        BossHUD.Create(root, gm);
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
        ui.endGroup = BuildEndPanel(root, out ui.endTitle, out ui.endBody);
        ui.victoryBoard = LeaderboardPanel.Create(root, "MAIN MENU", gm.BackToMainMenu);
        ui.mainMenu = MainMenu.Create(root, gm);
        ui.Difficulty = DifficultyPanel.Create(root);
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
        promptRect.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 8f), new Vector2(PromptWidth, height));

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

    public void ShowGameOver(string bossName)
    {
        endTitle.text = "<color=#E84A5F>YOU FOLDED</color>";
        endBody.text = $"The {bossName} called your bluff.\n\n<color=#FFD447>CLICK TO START OVER FROM THE JACK</color>";
        StartCoroutine(Fade(endGroup, 1f, 0.5f));
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

    private void Update()
    {
        // Entrada/saída da caixa do tutorial: fade + desliza para a direita (tempo real)
        float dt = Time.unscaledDeltaTime;
        promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, promptVisible ? 1f : 0f, dt * 5f);
        promptSlide = Mathf.MoveTowards(promptSlide, promptVisible ? 0f : 1f, dt * 4f);
        float eased = promptSlide * promptSlide; // desacelera ao chegar
        promptRect.anchoredPosition = new Vector2(-8f + eased * PromptSlideDistance, 8f);
        promptGroup.blocksRaycasts = promptVisible; // a seta só recebe clique com a caixa à mostra

        float target = gm.Fusion.Active ? 0.5f : 0f; // escurece a tela com o tempo parado
        var c = slowTint.color;
        c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 0.8f);
        slowTint.color = c;
    }
}
