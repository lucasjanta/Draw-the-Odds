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
    private readonly List<string> history = new List<string>();

    public static BossHUD Create(Transform parent, GameManager gm)
    {
        var panel = UIFactory.Image("BossHUD", parent, UIFactory.PanelColor);
        panel.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(206f, 58f));
        var hud = panel.gameObject.AddComponent<BossHUD>();
        hud.group = panel.gameObject.AddComponent<CanvasGroup>();
        hud.group.alpha = 0f;

        hud.nameText = UIFactory.Text("Name", panel.transform, "", 12f, Color.white);
        hud.nameText.fontStyle = FontStyles.Bold;
        hud.nameText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -3f), new Vector2(100f, 16f));

        hud.rangeText = UIFactory.Text("Range", panel.transform, "", 10f, new Color32(0xFF, 0xD4, 0x47, 0xFF), TextAlignmentOptions.Right);
        hud.rangeText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -4f), new Vector2(110f, 14f));

        UIFactory.Bar(panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -21f), new Vector2(190f, 9f),
            new Color32(0xB9, 0x4C, 0xFF, 0xFF), out hud.fill, out hud.ghost);

        var lastLabel = UIFactory.Text("LastLabel", panel.transform, "LAST", 8f, new Color(1f, 1f, 1f, 0.5f));
        lastLabel.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 6f), new Vector2(30f, 14f));
        hud.historyText = UIFactory.Text("History", panel.transform, "", 11f, Color.white);
        hud.historyText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 4f), new Vector2(170f, 16f));

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
        fillValue = ghostValue = 1f;
        UIFactory.SetBar(fill, 1f);
        b.OnHealthChanged += (cur, max) => { fillValue = cur / (float)max; UIFactory.SetBar(fill, fillValue); };
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

    private void Update()
    {
        bool visible = boss != null && !boss.IsDead;
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        ghostValue = Mathf.MoveTowards(ghostValue, fillValue, Time.unscaledDeltaTime * 0.5f);
        UIFactory.SetBar(ghost, ghostValue);
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 2f);
        rangeText.transform.localScale = Vector3.one * (1f + flash * 0.3f);
    }
}
