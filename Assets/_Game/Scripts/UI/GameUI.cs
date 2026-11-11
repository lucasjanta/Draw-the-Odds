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
        ui.slowTint = UIFactory.Image("SlowTint", root, new Color(0.3f, 0.45f, 1f, 0f));
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
            "<color=#FFD447>A / D</color> move   <color=#FFD447>SPACE</color> jump   <color=#FFD447>S + SPACE</color> drop down\n" +
            "<color=#FFD447>SCROLL</color> pick a card   <color=#FFD447>LEFT CLICK</color> throw it   <color=#FFD447>R</color> redraw hand (3x per boss)\n\n" +
            "Each hit tells you <color=#FF8A3D>▲ HIGHER</color> or <color=#5A9BFF>▼ LOWER</color> (warm = close).\n" +
            "EXACT = critical + new number   ±2 = 2 dmg   ±5 = 1 dmg   further = 0\n\n" +
            suits + "\n\n" +
            "Grab a <color=#3CC86E>+</color> <color=#E84A5F>−</color> <color=#A85CF0>↔</color> orb: time slows down!\n" +
            "<color=#FFD447>Q</color> / <color=#FFD447>E</color> put the selected card in a slot, <color=#FFD447>RIGHT CLICK</color> to fuse.\n" +
            "Mixed suits fuse into one of the two suits at random.";
        var text = UIFactory.Text("Body", back.transform, body, 10f, Color.white, TextAlignmentOptions.Top);
        text.lineSpacing = 4f;
        text.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(600f, 220f));

        var start = UIFactory.Text("Start", back.transform, "CLICK TO START", 14f, Color.white, TextAlignmentOptions.Center);
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
        float target = gm.Fusion.Active ? 0.16f : 0f;
        var c = slowTint.color;
        c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 0.8f);
        slowTint.color = c;
    }
}
