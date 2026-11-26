using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Ações do teclado que o jogador pode trocar de tecla (Opções → CONTROLS).
public enum GameKey { Left, Right, Down, Jump, Dash, Redraw, SlotA, SlotB, Pause }

// Teclas configuráveis do computador. O jogo inteiro lê o teclado por aqui (Held / Pressed),
// e os textos mostram a tecla escolhida com Name(...). Cada tecla fica salva no PlayerPrefs.
// Teclas extras FIXAS (não mudam): setas ← → ↓ para andar/descer e ESC para pausar.
// Mouse (escolher/arremessar) e 1-0 (escolher carta) também são fixos.
public static class KeyBindings
{
    public static readonly GameKey[] All = (GameKey[])Enum.GetValues(typeof(GameKey));

    private static readonly Key[] Defaults =
        { Key.A, Key.D, Key.S, Key.Space, Key.LeftShift, Key.R, Key.Q, Key.E, Key.P };

    // Extra fixa de cada ação (Key.None = nenhuma).
    private static readonly Key[] Extras =
        { Key.LeftArrow, Key.RightArrow, Key.DownArrow, Key.None, Key.None, Key.None, Key.None, Key.None, Key.Escape };

    // Teclas que não podem ser escolhidas (têm outro uso fixo).
    public static bool IsReserved(Key k) =>
        k == Key.None || k == Key.Escape || k == Key.Enter || k == Key.Backspace || k == Key.Tab
        || (k >= Key.Digit1 && k <= Key.Digit0) || (k >= Key.F1 && k <= Key.F12);

    public static event Action OnChanged;

    // A tela de teclas está esperando uma tecla nova (o resto do jogo ignora o teclado).
    private static bool capturing;
    private static int captureEndFrame = -1;
    public static bool Capturing
    {
        get => capturing;
        set { if (capturing && !value) captureEndFrame = Time.frameCount + 1; capturing = value; }
    }
    // Ocupado = escolhendo tecla, ou acabou de escolher (a tecla apertada não vale como comando).
    public static bool Busy => capturing || Time.frameCount <= captureEndFrame;

    private static Key[] current;
    private static Key[] Current
    {
        get
        {
            if (current != null) return current;
            current = new Key[Defaults.Length];
            for (int i = 0; i < current.Length; i++)
            {
                int saved = PlayerPrefs.GetInt(PrefKey((GameKey)i), (int)Defaults[i]);
                current[i] = Enum.IsDefined(typeof(Key), saved) && !IsReserved((Key)saved) ? (Key)saved : Defaults[i];
            }
            return current;
        }
    }

    private static string PrefKey(GameKey a) => "dto_key_" + a;

    public static Key Get(GameKey a) => Current[(int)a];

    // Troca a tecla de uma ação. Se outra ação já usava essa tecla, as duas trocam entre si.
    public static void Set(GameKey a, Key key)
    {
        if (IsReserved(key)) return;
        var keys = Current;
        Key old = keys[(int)a];
        for (int i = 0; i < keys.Length; i++)
            if (i != (int)a && keys[i] == key) { keys[i] = old; PlayerPrefs.SetInt(PrefKey((GameKey)i), (int)old); }
        keys[(int)a] = key;
        PlayerPrefs.SetInt(PrefKey(a), (int)key);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static void ResetDefaults()
    {
        for (int i = 0; i < Defaults.Length; i++) PlayerPrefs.DeleteKey(PrefKey((GameKey)i));
        PlayerPrefs.Save();
        current = null;
        OnChanged?.Invoke();
    }

    // A extra fixa só vale se nenhuma ação estiver usando aquela tecla.
    private static Key ExtraFor(GameKey a)
    {
        Key extra = Extras[(int)a];
        if (extra == Key.None) return Key.None;
        foreach (var k in Current) if (k == extra) return Key.None;
        return extra;
    }

    public static bool Held(GameKey a)
    {
        var kb = Keyboard.current;
        if (kb == null || Busy) return false;
        Key extra = ExtraFor(a);
        return kb[Get(a)].isPressed || (extra != Key.None && kb[extra].isPressed);
    }

    public static bool Pressed(GameKey a)
    {
        var kb = Keyboard.current;
        if (kb == null || Busy) return false;
        Key extra = ExtraFor(a);
        return kb[Get(a)].wasPressedThisFrame || (extra != Key.None && kb[extra].wasPressedThisFrame);
    }

    // Nome curto da tecla para os textos ("A", "SPACE", "SHIFT"...). Em teclados AZERTY/QWERTZ
    // o Unity devolve a letra impressa na tecla (a posição física é a mesma).
    public static string Name(GameKey a) => KeyName(Get(a));

    public static string KeyName(Key k)
    {
        switch (k)
        {
            case Key.Space: return Loc.T("SPACE");
            case Key.LeftShift: case Key.RightShift: return "SHIFT";
            case Key.LeftCtrl: case Key.RightCtrl: return "CTRL";
            case Key.LeftAlt: case Key.RightAlt: return "ALT";
            case Key.LeftArrow: return "←";
            case Key.RightArrow: return "→";
            case Key.UpArrow: return "↑";
            case Key.DownArrow: return "↓";
        }
        var kb = Keyboard.current;
        string name = null;
        if (kb != null)
        {
            try { name = kb[k].displayName; } catch { name = null; }
        }
        if (string.IsNullOrEmpty(name)) name = k.ToString();
        return name.ToUpperInvariant();
    }

    // Primeira tecla apertada neste quadro (para a tela de trocar teclas). Key.None = nenhuma.
    public static Key AnyKeyPressed()
    {
        var kb = Keyboard.current;
        if (kb == null) return Key.None;
        foreach (var control in kb.allKeys)
            if (control != null && control.wasPressedThisFrame) return control.keyCode;
        return Key.None;
    }
}
