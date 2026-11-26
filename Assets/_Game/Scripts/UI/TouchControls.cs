using UnityEngine;

// Controles de toque (celular/tablet): joystick à esquerda, PULO e DASH à direita, NOVA MÃO (carta) em cima
// deles e PAUSA no topo. As cartas ficam no MEIO embaixo (HandView) e são arrastadas para arremessar
// ou para os slots da fusão. Tocar no cenário arremessa a carta selecionada (WorldTapCatcher).
// Só aparece com VirtualInput.Active (opção TOUCH CONTROLS: AUTO / ON / OFF).
public class TouchControls : MonoBehaviour
{
    private static readonly Color JumpColor = new Color32(0x3C, 0x8C, 0xE8, 0xD0);
    private static readonly Color DashColor = new Color32(0x2F, 0xB8, 0xC9, 0xD0);
    private static readonly Color MiscColor = new Color32(0x4A, 0x3B, 0x7A, 0xD0);

    private GameManager gm;
    private GameObject tapLayer, playLayer;
    private TouchButton dash, redraw;
    private int shownRedraws = -1;

    // Criado em duas partes: o fundo de toque fica ATRÁS de toda a UI (primeiro filho do Canvas),
    // os controles ficam na frente do HUD (chamar CreateControls depois do resto da UI).
    public static TouchControls Create(Transform root, GameManager gm)
    {
        var tc = new GameObject("TouchControls", typeof(RectTransform)).AddComponent<TouchControls>();
        tc.transform.SetParent(root, false);
        ((RectTransform)tc.transform).Stretch();
        tc.gm = gm;

        tc.tapLayer = WorldTapCatcher.Create(root).gameObject;
        tc.tapLayer.transform.SetAsFirstSibling();

        var play = UIFactory.Rect("Play", tc.transform).Stretch();
        tc.playLayer = play.gameObject;
        TouchJoystick.Create(play);

        var jump = TouchButton.Create(play, "Jump", "▲", 58f, new Vector2(1f, 0f), new Vector2(-44f, 50f), JumpColor, 20f);
        jump.OnDown += () => { VirtualInput.PressJump(); VirtualInput.JumpHeld = true; };
        jump.OnUp += () => VirtualInput.JumpHeld = false;

        tc.dash = TouchButton.Create(play, "Dash", "»", 44f, new Vector2(1f, 0f), new Vector2(-104f, 36f), DashColor, 18f);
        tc.dash.OnDown += VirtualInput.PressDash;

        // Nova mão: carta (verso do baralho) com setas curvas; o número embaixo = trocas que restam.
        tc.redraw = TouchButton.CreateCard(play, "Redraw", gm.Config.CardBack, PixelIcons.Redraw, 48f, new Vector2(1f, 0f), new Vector2(-44f, 116f));
        tc.redraw.OnDown += gm.Redraw;

        var pause = TouchButton.Create(play, "Pause", "II", 32f, new Vector2(0.5f, 1f), new Vector2(0f, -22f), MiscColor, 12f); // 32: dedo acerta fácil
        pause.OnDown += gm.TogglePauseFromButton;

        VirtualInput.OnModeChanged += tc.Refresh;
        tc.Refresh();
        return tc;
    }

    private void OnDestroy() => VirtualInput.OnModeChanged -= Refresh;

    private void Refresh()
    {
        if (this == null) return;
        tapLayer.SetActive(VirtualInput.Active);
    }

    private void Update()
    {
        VirtualInput.DetectTouch();
        bool active = VirtualInput.Active;
        if (tapLayer.activeSelf != active) tapLayer.SetActive(active);

        // Controles só durante a luta; na fusão o painel tem os próprios botões (MERGE / SKIP).
        bool show = active && gm.State == GameManager.GameState.Playing && !gm.Fusion.Active && !GameTime.Paused && !gm.InputLocked;
        if (playLayer.activeSelf != show)
        {
            playLayer.SetActive(show);
            if (!show) VirtualInput.Clear();
        }
        if (!show) return;
        transform.SetAsLastSibling(); // sempre por cima do HUD (abaixo dos menus, que se reordenam ao abrir)
        dash.SetReady(gm.Player.DashCooldown01 >= 1f);
        redraw.SetReady(gm.RedrawsLeft > 0);
        if (gm.RedrawsLeft != shownRedraws) { shownRedraws = gm.RedrawsLeft; redraw.SetLabel("x" + shownRedraws); }
    }
}
