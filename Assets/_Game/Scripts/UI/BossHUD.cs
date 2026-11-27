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

    private GameManager gm;
    private int slot;   // 0 = chefe principal · 1 = 2º gêmeo (painel à esquerda do 1º)

    public static BossHUD Create(Transform parent, GameManager gm, int slot = 0)
    {
        var panel = UIFactory.Image("BossHUD" + (slot > 0 ? slot.ToString() : ""), parent, UIFactory.PanelColor);
        panel.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(slot == 0 ? -8f : -220f, -8f), new Vector2(206f, 58f));
        var hud = panel.gameObject.AddComponent<BossHUD>();
        hud.gm = gm;
        hud.slot = slot;
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
        return hud;
    }

    private void Bind(BossController b)
    {
        // Cada painel fica com um chefe: o 1º surgido na luta (slot 0) ou o 2º gêmeo (slot 1).
        int index = -1;
        for (int i = 0; i < gm.ActiveBosses.Count; i++) if (gm.ActiveBosses[i] == b) index = i;
        if (index < 0) index = 0;
        if (index != slot)
        {
            if (index == 0 && slot > 0) boss = null; // luta nova sem gêmeo: o 2º painel some
            return;
        }
        boss = b;
        history.Clear();
        historyText.text = "-";
        RefreshName();
        RefreshRange();
        b.OnSuitChanged += RefreshName;
        fillValue = ghostValue = targetFill = 1f;
        flying = 0;
        UIFactory.SetBar(fill, 1f);
        // A barra só desce quando o número de dano chega nela (se tiver algum voando).
        b.OnHealthChanged += (cur, max) => { targetFill = cur / (float)max; if (flying == 0) ApplyFill(); };
        b.OnHit += AddHistory; // cada painel mostra as tentativas no SEU chefe (gêmeos)
        b.OnHit += r => { if (!r.isHeal && r.amount > 0 && b != null) StartCoroutine(FlyDamage(b.transform.position, r.amount, r.isCritical)); };
        b.OnNumberChanged += () =>
        {
            history.Clear();
            historyText.text = "<color=#B9A0FF>NEW NUMBER!</color>";
            flash = 1f;
            RefreshRange();
        };
    }

    // Nome com o naipe atual; Dama: fase atual (ex.: "QUEEN ♥ 2/4").
    private void RefreshName()
    {
        if (boss == null) return;
        nameText.text = boss.PhaseCount > 1 ? $"{boss.Title} <size=80%>{boss.Phase + 1}/{boss.PhaseCount}</size>" : boss.Title;
    }

    // "NUMBER ? (1–13)". Ás: "1º 7 · 2º ?" depois de travar o 1º. Dado Viciado: PAR / ÍMPAR do número atual.
    private void RefreshRange()
    {
        if (boss == null) return;
        string range = $"({boss.Data.MinNumber}–{boss.Data.MaxNumber})";
        string text;
        if (boss.Data.TwoNumbers)
            text = boss.OnSecondNumber ? $"<color=#4CFF9A>{boss.LockedNumber}</color> + ? {range}" : $"? + ? {range}";
        else text = $"NUMBER ? {range}";
        if (Jokers.ShowHalf)
        {
            // Olho de Lince: metade de baixo ou de cima da faixa
            int mid = (boss.Data.MinNumber + boss.Data.MaxNumber) / 2;
            text += boss.SecretNumber <= mid
                ? " <color=#FFD080>" + Loc.Pick("LOW", "BAIXO", "BAJO", "BAS", "TIEF", "BASSO") + "</color>"
                : " <color=#FFD080>" + Loc.Pick("HIGH", "ALTO", "ALTO", "HAUT", "HOCH", "ALTO") + "</color>";
        }
        if (Jokers.ShowParity)
            text += boss.SecretNumber % 2 == 0
                ? " <color=#FFB040>" + Loc.Pick("EVEN", "PAR", "PAR", "PAIR", "GERADE", "PARI") + "</color>"
                : " <color=#FFB040>" + Loc.Pick("ODD", "ÍMPAR", "IMPAR", "IMPAIR", "UNGERADE", "DISPARI") + "</color>";
        rangeText.text = text;
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
