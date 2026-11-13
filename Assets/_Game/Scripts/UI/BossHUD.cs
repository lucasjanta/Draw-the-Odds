using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Canto superior direito: nome do chefe, faixa do número secreto, vida e últimas tentativas.
public class BossHUD : MonoBehaviour
{
    private const int HistorySize = 5;

    private BossController boss;
    private CanvasGroup group;
    private RectTransform fill, ghost;
    private TextMeshProUGUI nameText, rangeText, historyText;
    private float ghostValue = 1f, fillValue = 1f, flash;
    private float targetFill = 1f, shake;
    private int flying;                       // números de dano ainda voando até a barra
    private RectTransform panelRect, canvasRect, barRect;
    private Vector2 panelBase;
    private UnityEngine.UI.Image fillImage;
    private readonly List<string> history = new List<string>();

    public static BossHUD Create(Transform parent, GameManager gm)
    {
        var panel = UIFactory.Image("BossHUD", parent, UIFactory.PanelColor);
        panel.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(206f, 58f));
        var hud = panel.gameObject.AddComponent<BossHUD>();
        hud.group = panel.gameObject.AddComponent<CanvasGroup>();
        hud.group.alpha = 0f;

        hud.nameText = UIFactory.Text("Name", panel.transform, "", 12f, Color.white);
        // Nome do chefe na fonte do título (Alagard), se ela existir no GameConfig
        if (gm.Config.TitleFont != null) hud.nameText.font = gm.Config.TitleFont;
        else hud.nameText.fontStyle = FontStyles.Bold;
        hud.nameText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -3f), new Vector2(100f, 16f));

        hud.rangeText = UIFactory.Text("Range", panel.transform, "", 10f, new Color32(0xFF, 0xD4, 0x47, 0xFF), TextAlignmentOptions.Right);
        hud.rangeText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -4f), new Vector2(110f, 14f));

        UIFactory.Bar(panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -21f), new Vector2(190f, 9f),
            new Color32(0xB9, 0x4C, 0xFF, 0xFF), out hud.fill, out hud.ghost);

        var lastLabel = UIFactory.Text("LastLabel", panel.transform, "LAST", 8f, new Color(1f, 1f, 1f, 0.5f));
        lastLabel.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 6f), new Vector2(30f, 14f));
        hud.historyText = UIFactory.Text("History", panel.transform, "", 11f, Color.white);
        hud.historyText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 4f), new Vector2(170f, 16f));

        hud.panelRect = panel.rectTransform;
        hud.panelBase = panel.rectTransform.anchoredPosition;
        hud.canvasRect = (RectTransform)parent;
        hud.barRect = (RectTransform)hud.fill.parent;
        hud.fillImage = hud.fill.GetComponent<UnityEngine.UI.Image>();
        gm.OnBossSpawned += hud.Bind;
        gm.OnBossHit += hud.AddHistory;
        return hud;
    }

    private void Bind(BossController b)
    {
        boss = b;
        history.Clear();
        historyText.text = "-";
        nameText.text = b.Title;
        rangeText.text = $"NUMBER ? ({b.Data.MinNumber}–{b.Data.MaxNumber})";
        fillValue = ghostValue = targetFill = 1f;
        flying = 0;
        UIFactory.SetBar(fill, 1f);
        // A barra só desce quando o número de dano chega nela (se tiver algum voando).
        b.OnHealthChanged += (cur, max) => { targetFill = cur / (float)max; if (flying == 0) ApplyFill(); };
        b.OnHit += r => { if (!r.isHeal && r.amount > 0 && b != null) StartCoroutine(FlyDamage(b.transform.position, r.amount, r.isCritical)); };
        b.OnNumberChanged += () =>
        {
            history.Clear();
            historyText.text = "<color=#B9A0FF>NEW NUMBER!</color>";
            flash = 1f;
        };
    }

    private void AddHistory(HitResult r)
    {
        string arrow = r.hint switch
        {
            HintDirection.Higher => "▲",
            HintDirection.Lower => "▼",
            _ => "!",
        };
        string warm = ColorUtility.ToHtmlStringRGB(LuckyMath.WarmthColor(r.distance));
        history.Insert(0, $"{r.card.ColoredLabel}<color=#{warm}>{arrow}</color>");
        if (history.Count > HistorySize) history.RemoveAt(history.Count - 1);
        historyText.text = string.Join("  ", history);
    }

    private void ApplyFill()
    {
        fillValue = targetFill;
        UIFactory.SetBar(fill, fillValue);
    }

    // Número de dano: nasce em cima do chefe (pula grande), voa em arco até o fim da barra de vida;
    // ao chegar a barra desce, pisca branco e o painel treme.
    private IEnumerator FlyDamage(Vector3 worldPos, int amount, bool critical)
    {
        flying++;
        var cam = Camera.main;
        Vector2 start = Vector2.zero;
        if (cam != null)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cam.WorldToScreenPoint(worldPos + Vector3.right * 1.2f), null, out start);

        var text = UIFactory.Text("Damage", canvasRect, "-" + amount, critical ? 22f : 15f,
            critical ? (Color)new Color32(0xFF, 0xD4, 0x47, 0xFF) : Color.white, TextAlignmentOptions.Center).NoLoc();
        text.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), start, new Vector2(80f, 30f));
        text.Outline(2f);

        // 1) pulo no lugar do acerto
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.18f;
            text.rectTransform.anchoredPosition = start + Vector2.up * 14f * Mathf.Sin(k * Mathf.PI * 0.5f);
            text.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.5f, Mathf.Sin(k * Mathf.PI * 0.5f));
            yield return null;
        }
        // 2) voa em arco até a ponta atual da barra
        Vector2 from = text.rectTransform.anchoredPosition;
        const float flyTime = 0.4f;
        for (float t = 0f; t < flyTime; t += Time.unscaledDeltaTime)
        {
            float k = t / flyTime;
            float e = k * k;                                     // acelera chegando
            Vector2 to = BarTip();
            Vector2 control = new Vector2(from.x, Mathf.Max(from.y, to.y) + 60f);
            Vector2 p = Vector2.Lerp(Vector2.Lerp(from, control, e), Vector2.Lerp(control, to, e), e);
            text.rectTransform.anchoredPosition = p;
            text.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.5f, 0.7f, e);
            yield return null;
        }
        Destroy(text.gameObject);

        // 3) chegou: barra desce, pisca e treme
        flying = Mathf.Max(0, flying - 1);
        ApplyFill();
        shake = critical ? 0.45f : 0.25f;
        flash = Mathf.Max(flash, 0.6f);
        if (fillImage != null) StartCoroutine(FlashFill());
    }

    private IEnumerator FlashFill()
    {
        Color baseColor = new Color32(0xB9, 0x4C, 0xFF, 0xFF);
        fillImage.color = Color.white;
        yield return new WaitForSecondsRealtime(0.08f);
        fillImage.color = baseColor;
    }

    // Ponta direita do preenchimento da barra, em coordenadas do Canvas.
    private Vector2 BarTip()
    {
        var corners = new Vector3[4];
        barRect.GetWorldCorners(corners);                       // 0 = baixo-esq, 2 = cima-dir
        Vector3 world = Vector3.Lerp((corners[0] + corners[1]) / 2f, (corners[2] + corners[3]) / 2f, fillValue);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(null, world), null, out var local);
        return local;
    }

    private void Update()
    {
        // Tremida do painel quando o dano chega
        shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime);
        panelRect.anchoredPosition = panelBase + (shake > 0f ? Random.insideUnitCircle * 10f * shake : Vector2.zero);

        bool visible = boss != null && !boss.IsDead;
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        ghostValue = Mathf.MoveTowards(ghostValue, fillValue, Time.unscaledDeltaTime * 0.5f);
        UIFactory.SetBar(ghost, ghostValue);
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 2f);
        rangeText.transform.localScale = Vector3.one * (1f + flash * 0.3f);
    }
}
