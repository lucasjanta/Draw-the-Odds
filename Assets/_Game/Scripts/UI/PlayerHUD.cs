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
    private bool touchLabels;

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
        hud.redrawText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 18f), new Vector2(104f, 14f)); // o "MÃO 7/10" fica à direita
        FitText(hud.redrawText); // "[R] NUOVA MANO ●●●" é bem mais comprido que "[R] REDRAW ●●●"

        hud.dashText = UIFactory.Text("Dash", panel.transform, DashLabel(false), 9f, new Color(0.85f, 0.85f, 1f));
        hud.dashText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 4f), new Vector2(70f, 14f));
        FitText(hud.dashText); // a barra do dash começa logo depois
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
        KeyBindings.OnChanged += hud.HandleKeysChanged;
        hud.RefreshTexts();
        return hud;
    }

    private void OnDestroy() => KeyBindings.OnChanged -= HandleKeysChanged;

    // Texto que diminui sozinho se não couber (idiomas com palavras compridas, teclas como "ESPACIO").
    private static void FitText(TMPro.TextMeshProUGUI t)
    {
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = 5f;
        t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
    }

    // Trocou uma tecla nas Opções: atualiza "[R] REDRAW" e "[SHIFT] DASH".
    private void HandleKeysChanged()
    {
        if (this == null) return;
        dashText.text = DashLabel(VirtualInput.Active);
        RefreshTexts();
    }

    private static string DashLabel(bool touch) => touch ? "DASH" : $"[{KeyBindings.Name(GameKey.Dash)}] DASH";

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
        // Ficha da Sorte pode passar do máximo: mostra todas as bolinhas
        int total = Mathf.Max(gm.Config.RedrawsPerBoss, gm.RedrawsLeft);
        for (int i = 0; i < total; i++)
            pips += i < gm.RedrawsLeft ? "<color=#FFD447>●</color>" : "<color=#555577>●</color>";
        redrawText.text = (VirtualInput.Active ? "REDRAW " : $"[{KeyBindings.Name(GameKey.Redraw)}] REDRAW ") + pips; // no toque não há tecla
        handText.text = $"HAND {gm.Hand.Count}/{gm.Hand.MaxSize}";
    }

    private void Update()
    {
        ghostValue = Mathf.MoveTowards(ghostValue, fillValue, Time.unscaledDeltaTime * 0.6f);
        UIFactory.SetBar(ghost, ghostValue);

        float dash = gm.Player.DashCooldown01;
        UIFactory.SetBar(dashFill, dash);
        dashText.color = dash >= 1f ? Color.white : new Color(0.6f, 0.6f, 0.75f);
        bool touch = VirtualInput.Active;
        if (touch != touchLabels)
        {
            touchLabels = touch;
            dashText.text = DashLabel(touch);
            RefreshTexts();
        }
    }
}
