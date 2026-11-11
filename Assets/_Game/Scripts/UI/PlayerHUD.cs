using TMPro;
using UnityEngine;

// Canto superior esquerdo: vida do player, trocas de mão (R) restantes e tamanho da mão.
public class PlayerHUD : MonoBehaviour
{
    private GameManager gm;
    private RectTransform fill, ghost, dashFill;
    private TextMeshProUGUI dashText;
    private TextMeshProUGUI hpText, redrawText, handText;
    private float ghostValue = 1f, fillValue = 1f;

    public static PlayerHUD Create(Transform parent, GameManager gm)
    {
        var panel = UIFactory.Image("PlayerHUD", parent, UIFactory.PanelColor);
        panel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -8f), new Vector2(176f, 56f));
        var hud = panel.gameObject.AddComponent<PlayerHUD>();
        hud.gm = gm;

        var label = UIFactory.Text("HP", panel.transform, "HP", 11f, Color.white);
        label.fontStyle = FontStyles.Bold;
        label.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(24f, 14f));

        UIFactory.Bar(panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -6f), new Vector2(138f, 11f),
            new Color32(0xE8, 0x3B, 0x4B, 0xFF), out hud.fill, out hud.ghost);
        hud.hpText = UIFactory.Text("HpText", hud.fill.parent, "", 8f, Color.white, TextAlignmentOptions.Center);
        hud.hpText.rectTransform.Stretch();

        hud.redrawText = UIFactory.Text("Redraw", panel.transform, "", 9f, new Color(0.85f, 0.85f, 1f));
        hud.redrawText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 18f), new Vector2(100f, 14f));

        hud.dashText = UIFactory.Text("Dash", panel.transform, "[SHIFT] DASH", 9f, new Color(0.85f, 0.85f, 1f));
        hud.dashText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 4f), new Vector2(80f, 14f));
        UIFactory.Bar(panel.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(82f, 8f), new Vector2(86f, 6f),
            new Color32(0x73, 0xD8, 0xFF, 0xFF), out hud.dashFill, out var dashGhost);
        dashGhost.gameObject.SetActive(false);

        hud.handText = UIFactory.Text("HandCount", panel.transform, "", 9f, new Color(0.85f, 0.85f, 1f), TextAlignmentOptions.Right);
        hud.handText.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 18f), new Vector2(70f, 14f));

        gm.PlayerHealth.OnHealthChanged += hud.HandleHealth;
        gm.OnRedrawsChanged += hud.RefreshTexts;
        gm.Hand.OnCardAdded += _ => hud.RefreshTexts();
        gm.Hand.OnCardRemoved += (_, __) => hud.RefreshTexts();
        hud.HandleHealth(gm.PlayerHealth.Current, gm.PlayerHealth.Max);
        hud.RefreshTexts();
        return hud;
    }

    private void HandleHealth(int current, int max)
    {
        fillValue = max > 0 ? current / (float)max : 0f;
        hpText.text = $"{current}/{max}";
        UIFactory.SetBar(fill, fillValue);
        if (fillValue > ghostValue) ghostValue = fillValue;
    }

    private void RefreshTexts()
    {
        string pips = "";
        for (int i = 0; i < gm.Config.RedrawsPerBoss; i++)
            pips += i < gm.RedrawsLeft ? "<color=#FFD447>●</color>" : "<color=#555577>●</color>";
        redrawText.text = $"[R] REDRAW {pips}";
        handText.text = $"HAND {gm.Hand.Count}/{gm.Hand.MaxSize}";
    }

    private void Update()
    {
        ghostValue = Mathf.MoveTowards(ghostValue, fillValue, Time.unscaledDeltaTime * 0.6f);
        UIFactory.SetBar(ghost, ghostValue);

        float dash = gm.Player.DashCooldown01;
        UIFactory.SetBar(dashFill, dash);
        dashText.color = dash >= 1f ? Color.white : new Color(0.6f, 0.6f, 0.75f);
    }
}
