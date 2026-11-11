using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Monta o Canvas inteiro em código e controla as telas (título, banner, derrota, vitória).
public class GameUI : MonoBehaviour
{
    private GameManager gm;
    private Image slowTint;
    private CanvasGroup bannerGroup, titleGroup, endGroup;
    private TextMeshProUGUI bannerTitle, bannerSub, endTitle, endBody;
    private Coroutine bannerRoutine;
    private CanvasGroup promptGroup;
    private RectTransform promptRect;
    private TextMeshProUGUI promptText, promptContinue;
    private bool promptVisible;

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

        var ui = go.AddComponent<GameUI>();
        ui.gm = gm;
        Transform root = go.transform;

        // Tinta azulada durante a câmera lenta
        ui.slowTint = UIFactory.Image("SlowTint", root, new Color(0.02f, 0.02f, 0.08f, 0f));
        ui.slowTint.rectTransform.Stretch();

        HandView.Create(root, gm);
        PlayerHUD.Create(root, gm);
        BossHUD.Create(root, gm);
        FusionPanel.Create(root, gm);

        // Banner central ("BOSS 1/3 — JACK")
        var banner = UIFactory.Rect("Banner", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(640f, 80f));
        ui.bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        ui.bannerGroup.alpha = 0f;
        var bannerBack = UIFactory.Image("Back", banner, new Color(0f, 0f, 0f, 0.55f));
        bannerBack.rectTransform.Stretch();
        ui.bannerSub = UIFactory.Text("Sub", banner, "", 12f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
        ui.bannerSub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(600f, 16f));
        ui.bannerTitle = UIFactory.Text("Title", banner, "", 34f, Color.white, TextAlignmentOptions.Center);
        ui.bannerTitle.fontStyle = FontStyles.Bold;
        ui.bannerTitle.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(600f, 44f));

        ui.BuildPrompt(root);
        ui.titleGroup = BuildTitle(root);
        ui.endGroup = BuildEndPanel(root, out ui.endTitle, out ui.endBody);
        return ui;
    }

    private static CanvasGroup BuildTitle(Transform root)
    {
        var back = UIFactory.Image("Title", root, new Color(0.04f, 0.02f, 0.09f, 0.93f));
        back.rectTransform.Stretch();
        var group = back.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var title = UIFactory.Text("Name", back.transform, "DRAW THE ODDS", 40f, new Color32(0xFF, 0xD4, 0x47, 0xFF), TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(600f, 50f));

        var sub = UIFactory.Text("Sub", back.transform, "Every boss hides a secret number. Hit it with the right card.", 11f,
            new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
        sub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(600f, 16f));

        string suits =
            $"{SuitInfo.Colored(SuitInfo.Symbol(Suit.Clubs) + " CLUBS", Suit.Clubs)} normal damage     " +
            $"{SuitInfo.Colored(SuitInfo.Symbol(Suit.Spades) + " SPADES", Suit.Spades)} x1.5 damage\n" +
            $"{SuitInfo.Colored(SuitInfo.Symbol(Suit.Hearts) + " HEARTS", Suit.Hearts)} heal you instead     " +
            $"{SuitInfo.Colored(SuitInfo.Symbol(Suit.Diamonds) + " DIAMONDS", Suit.Diamonds)} damage + draw a card";

        string body =
            "<color=#FFD447>A / D</color> move   <color=#FFD447>SPACE</color> jump   <color=#FFD447>S + SPACE</color> drop down   <color=#FFD447>SHIFT</color> dash\n" +
            "<color=#FFD447>SCROLL</color> pick a card   <color=#FFD447>LEFT CLICK</color> throw it   <color=#FFD447>R</color> redraw hand (3x per boss)\n\n" +
            "Each hit tells you <color=#FF8A3D>▲ HIGHER</color> or <color=#5A9BFF>▼ LOWER</color> (warm = close).\n" +
            "EXACT = critical + new number   ±2 = 2 dmg   ±5 = 1 dmg   further = 0\n\n" +
            suits + "\n\n" +
            "Grab a <color=#3CC86E>+</color> <color=#E84A5F>−</color> <color=#FFD447>×</color> orb: TIME STOPS!\n" +
            "<color=#FFD447>Q</color> / <color=#FFD447>E</color> put the selected card in a slot, <color=#FFD447>RIGHT CLICK</color> to merge.\n" +
            "The result is a surprise. Mixed suits give one of the two at random.";
        var text = UIFactory.Text("Body", back.transform, body, 10f, Color.white, TextAlignmentOptions.Top);
        text.lineSpacing = 4f;
        text.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(600f, 220f));

        var start = UIFactory.Text("Start", back.transform,
            "CLICK  <size=10><color=#AAAACC>play with tutorial</color></size>      ENTER  <size=10><color=#AAAACC>skip tutorial</color></size>",
            14f, Color.white, TextAlignmentOptions.Center);
        start.fontStyle = FontStyles.Bold;
        start.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(400f, 20f));
        start.gameObject.AddComponent<Blink>();
        return group;
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

    // Caixa de instruções do tutorial. low = perto da mão (para não cobrir o painel de fusão).
    private void BuildPrompt(Transform root)
    {
        var back = UIFactory.Image("Prompt", root, new Color(0.05f, 0.03f, 0.12f, 0.92f));
        promptRect = back.rectTransform;
        promptGroup = back.gameObject.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
        // Faixa amarela no topo da caixa (destaque)
        var stripe = UIFactory.Image("Stripe", back.transform, new Color32(0xFF, 0xD4, 0x47, 0xFF));
        stripe.rectTransform.anchorMin = new Vector2(0f, 1f);
        stripe.rectTransform.anchorMax = new Vector2(1f, 1f);
        stripe.rectTransform.pivot = new Vector2(0.5f, 1f);
        stripe.rectTransform.sizeDelta = new Vector2(0f, 2f);
        stripe.rectTransform.anchoredPosition = Vector2.zero;

        promptText = UIFactory.Text("Text", back.transform, "", 11f, Color.white, TextAlignmentOptions.Center);
        promptText.textWrappingMode = TextWrappingModes.Normal;
        promptText.lineSpacing = 4f;
        promptText.rectTransform.Stretch(8f);
        promptText.rectTransform.offsetMin = new Vector2(10f, 16f);

        promptContinue = UIFactory.Text("Continue", back.transform, "CLICK TO CONTINUE", 8f, new Color32(0xFF, 0xD4, 0x47, 0xFF), TextAlignmentOptions.Bottom);
        promptContinue.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(200f, 12f));
        promptContinue.gameObject.AddComponent<Blink>();
    }

    public void ShowPrompt(string text, bool clickToContinue, bool low = false)
    {
        promptText.text = text;
        promptContinue.gameObject.SetActive(clickToContinue);
        promptText.rectTransform.offsetMin = new Vector2(10f, clickToContinue ? 16f : 6f);
        // Altura proporcional ao número de linhas do texto
        int lines = text.Split('\n').Length;
        float height = 16f + lines * 15f + (clickToContinue ? 12f : 0f);
        promptRect.Place(low ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f), low ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f),
            low ? new Vector2(0f, 84f) : new Vector2(0f, -74f), new Vector2(470f, height));
        promptVisible = true;
        promptGroup.alpha = 0.2f;
        promptRect.localScale = Vector3.one * 0.9f;
    }

    public void HidePrompt() => promptVisible = false;

    public void ShowTitle(bool show) => StartCoroutine(Fade(titleGroup, show ? 1f : 0f, show ? 0f : 0.3f));

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
        endBody.text = $"The {bossName} called your bluff.\n\n<color=#FFD447>CLICK TO RETRY THE {bossName}</color>";
        StartCoroutine(Fade(endGroup, 1f, 0.5f));
    }

    public void ShowVictory(GameManager.RunStats stats, float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f), s = Mathf.FloorToInt(seconds % 60f);
        endTitle.text = "<color=#FFD447>YOU BEAT THE ODDS!</color>";
        endBody.text =
            $"Time  {m}:{s:00}\nCards thrown  {stats.cardsThrown}\nCriticals  {stats.criticals}\n" +
            $"Fusions  {stats.fusions}\nDamage taken  {stats.damageTaken}\n\n<color=#FFD447>CLICK TO PLAY AGAIN</color>";
        StartCoroutine(Fade(endGroup, 1f, 0.6f));
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
        promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, promptVisible ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        promptRect.localScale = Vector3.one * Mathf.MoveTowards(promptRect.localScale.x, 1f, Time.unscaledDeltaTime * 2f);

        float target = gm.Fusion.Active ? 0.5f : 0f; // escurece a tela com o tempo parado
        var c = slowTint.color;
        c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 0.8f);
        slowTint.color = c;
    }
}
