#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Só no Editor: simula teclado/mouse para testes automáticos (chamado pela ClaudeBridge).
// Ex.: call DebugInput.Keys "d 0.6"   call DebugInput.Keys "s+space 0.1"   call DebugInput.Mouse "left"
public static class DebugInput
{
    public static string Keys(string arg)
    {
        var parts = arg.Split(' ');
        Key[] keys = parts[0].Split('+').Select(k => (Key)Enum.Parse(typeof(Key), k, true)).ToArray();
        float hold = parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 0.1f;
        GameManager.Instance.StartCoroutine(HoldKeys(keys, hold));
        return "keys " + parts[0];
    }

    private static IEnumerator HoldKeys(Key[] keys, float seconds)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
        yield return new WaitForSecondsRealtime(seconds);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
    }

    // "left" | "right" | "scrollup" | "scrolldown" | "left x y" (posição em pixels da tela)
    public static string Mouse(string arg)
    {
        var parts = arg.Split(' ');
        var mouse = UnityEngine.InputSystem.Mouse.current;
        Vector2 pos = parts.Length >= 3 ? new Vector2(float.Parse(parts[1]), float.Parse(parts[2])) : mouse.position.ReadValue();
        var state = new MouseState { position = pos };
        switch (parts[0])
        {
            case "left": state.buttons = 1; break;
            case "right": state.buttons = 2; break;
            case "scrollup": state.scroll = new Vector2(0f, 120f); break;
            case "scrolldown": state.scroll = new Vector2(0f, -120f); break;
        }
        GameManager.Instance.StartCoroutine(MouseRoutine(state, pos));
        return "mouse " + parts[0];
    }

    private static IEnumerator MouseRoutine(MouseState down, Vector2 pos)
    {
        InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, down);
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, new MouseState { position = pos });
    }

    // Mira no chefe e clica (arremessa a carta selecionada).
    public static string ClickBoss()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Boss == null) return "no boss";
        Vector3 sp = Camera.main.WorldToScreenPoint(gm.Boss.transform.position);
        return Mouse($"left {sp.x:0} {sp.y:0}");
    }
}
#endif
