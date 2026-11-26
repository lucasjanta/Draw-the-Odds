using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum TouchMode { Auto, On, Off }

// Controles de toque → personagem. O joystick e os botões da tela escrevem aqui, e o PlayerController
// lê junto com o teclado (os dois funcionam ao mesmo tempo).
// "Pressionou" (pulo, dash) fica guardado até o PlayerController consumir: assim não importa se o
// toque chegou antes ou depois do Update dele no mesmo frame.
public static class VirtualInput
{
    private const string PrefKey = "dto_touch";

    public static Vector2 Move { get; set; }
    public static bool JumpHeld { get; set; }
    private static bool jumpPressed, dashPressed;

    public static void PressJump() => jumpPressed = true;
    public static void PressDash() => dashPressed = true;

    public static bool ConsumeJump() { bool v = jumpPressed; jumpPressed = false; return v; }
    public static bool ConsumeDash() { bool v = dashPressed; dashPressed = false; return v; }

    // Solta tudo (fim da luta, pausa, controles escondidos).
    public static void Clear()
    {
        Move = Vector2.zero;
        JumpHeld = jumpPressed = dashPressed = false;
    }

    // ===================== Ligado ou não =====================
    // AUTO = liga em celular/tablet ou assim que a tela é tocada (notebook com tela de toque).
    public static event Action OnModeChanged;
    private static bool touchSeen;

    // Debug (ponte do Editor): call VirtualInput.DebugTouch on|off|auto
    public static string DebugTouch(string arg)
    {
        Mode = arg == "on" ? TouchMode.On : arg == "off" ? TouchMode.Off : TouchMode.Auto;
        return "touch " + Mode;
    }

    public static TouchMode Mode
    {
        get => (TouchMode)PlayerPrefs.GetInt(PrefKey, 0);
        set { PlayerPrefs.SetInt(PrefKey, (int)value); PlayerPrefs.Save(); OnModeChanged?.Invoke(); }
    }

    public static bool Active => Mode == TouchMode.On ||
        (Mode == TouchMode.Auto && (Application.isMobilePlatform || Portal.IsMobileDevice || touchSeen));

    // Algo externo mudou (ex.: o portal disse que é celular): avisa quem depende do modo toque.
    public static void NotifyChanged() => OnModeChanged?.Invoke();

    // Chamado todo frame pelos controles: o primeiro toque de verdade liga o modo AUTO.
    public static void DetectTouch()
    {
        if (touchSeen) return;
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
        {
            touchSeen = true;
            OnModeChanged?.Invoke();
        }
    }

    // Um toque/clique novo neste frame (para "clique para continuar" funcionar no celular também).
    public static bool AnyPressThisFrame(out Vector2 screenPos)
    {
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = ts.primaryTouch.position.ReadValue();
            return true;
        }
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPos = mouse.position.ReadValue();
            return true;
        }
        screenPos = default;
        return false;
    }

    // ===================== UI debaixo do dedo/mouse =====================
    private static readonly List<RaycastResult> hits = new List<RaycastResult>();

    // Tem um controle de UI (botão, carta da mão, slot da fusão, joystick) nesta posição?
    // Fundo transparente e painéis decorativos não contam: clicar neles ainda vale como "clique no jogo".
    public static bool OverInteractive(Vector2 screenPos)
    {
        var es = EventSystem.current;
        if (es == null) return false;
        var data = new PointerEventData(es) { position = screenPos };
        hits.Clear();
        es.RaycastAll(data, hits);
        foreach (var h in hits)
        {
            if (h.gameObject == null) continue;
            if (h.gameObject.GetComponentInParent<Selectable>() != null) return true;
            if (h.gameObject.GetComponentInParent<IInteractiveUi>() != null) return true;
        }
        return false;
    }

    public static bool MouseOverInteractive()
    {
        var mouse = Mouse.current;
        return mouse != null && OverInteractive(mouse.position.ReadValue());
    }
}
