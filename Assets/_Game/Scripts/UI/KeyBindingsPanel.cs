using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Tela de teclas (Opções → CONTROLS): uma linha por ação com a tecla atual.
// Clicar na tecla → "APERTE UMA TECLA..." → a próxima tecla apertada vira a nova (ESC cancela).
// Se a tecla já era de outra ação, as duas trocam. RESET volta ao padrão (A/D/S, ESPAÇO, SHIFT, R, Q/E, P).
public class KeyBindingsPanel : MonoBehaviour
{
    private static readonly string[] ActionNames =
        { "MOVE LEFT", "MOVE RIGHT", "DROP DOWN", "JUMP", "DASH", "NEW HAND", "MERGE SLOT 1", "MERGE SLOT 2", "PAUSE" };

    private TextMeshProUGUI[] keyLabels;
    private int waiting = -1; // ação esperando tecla nova (-1 = nenhuma)

    public static KeyBindingsPanel Create(Transform parent, Action onBack)
    {
        const float rowH = 19f, top = -40f;
        var panel = UIFactory.MenuPanel("Keys", parent, new Vector2(300f, 300f));
        var p = panel.gameObject.AddComponent<KeyBindingsPanel>();

        var title = UIFactory.Text("Title", panel.transform, "CONTROLS", 20f, UIFactory.Gold, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(280f, 26f));

        p.keyLabels = new TextMeshProUGUI[KeyBindings.All.Length];
        for (int i = 0; i < KeyBindings.All.Length; i++)
        {
            int index = i;
            float y = top - i * rowH;
            var name = UIFactory.Text("Action" + i, panel.transform, ActionNames[i], 10f, Color.white);
            name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, y), new Vector2(150f, 16f));
            var button = UIFactory.Button("Key" + i, panel.transform, "", new Vector2(100f, 16f), () => p.StartWaiting(index), 10f);
            button.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-18f, y), new Vector2(100f, 16f));
            p.keyLabels[i] = button.transform.Find("Label").GetComponent<TextMeshProUGUI>().NoLoc();
            p.keyLabels[i].enableAutoSizing = true; // "APPUIE SUR UNE TOUCHE..." é comprido
            p.keyLabels[i].fontSizeMin = 5f;
            p.keyLabels[i].fontSizeMax = 10f;
        }

        var help = UIFactory.Text("Help", panel.transform, "Click an action, then press the new key. ESC cancels.", 7f,
            new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
        help.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(280f, 12f));
        var help2 = UIFactory.Text("Help2", panel.transform, "Arrows and ESC always work. Mouse and 1-0 pick cards.", 7f,
            new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
        help2.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(280f, 12f));

        var reset = UIFactory.Button("Reset", panel.transform, "RESET", new Vector2(110f, 22f), () => { p.StopWaiting(); KeyBindings.ResetDefaults(); }, 11f);
        reset.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-62f, 12f), new Vector2(110f, 22f));
        var back = UIFactory.Button("Back", panel.transform, "BACK", new Vector2(110f, 22f), () => { p.StopWaiting(); onBack(); }, 11f);
        back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(62f, 12f), new Vector2(110f, 22f));

        KeyBindings.OnChanged += p.RefreshLabels;
        Loc.OnChanged += p.RefreshLabels; // "ESPAÇO" / "SPACE"...
        panel.gameObject.SetActive(false);
        return p;
    }

    private void OnDestroy()
    {
        KeyBindings.OnChanged -= RefreshLabels;
        Loc.OnChanged -= RefreshLabels;
        KeyBindings.Capturing = false;
    }

    public void Show(bool show)
    {
        if (!show) StopWaiting();
        gameObject.SetActive(show);
        if (show) { transform.SetAsLastSibling(); RefreshLabels(); }
    }

    private void OnDisable() => StopWaiting();

    private void StartWaiting(int index)
    {
        waiting = index;
        KeyBindings.Capturing = true;
        RefreshLabels();
    }

    private void StopWaiting()
    {
        if (waiting < 0 && !KeyBindings.Capturing) return;
        waiting = -1;
        KeyBindings.Capturing = false;
        RefreshLabels();
    }

    private void Update()
    {
        if (waiting < 0) return;
        Key key = KeyBindings.AnyKeyPressed();
        if (key == Key.None) return;
        if (key == Key.Escape) { StopWaiting(); return; }
        if (KeyBindings.IsReserved(key)) return; // Enter, 1-0, F1-F12...: ignora e continua esperando
        var action = KeyBindings.All[waiting];
        StopWaiting();
        KeyBindings.Set(action, key);
    }

    private void RefreshLabels()
    {
        if (this == null || keyLabels == null) return;
        for (int i = 0; i < keyLabels.Length; i++)
            keyLabels[i].text = i == waiting
                ? $"<color=#FFD447>{Loc.T("PRESS A KEY...")}</color>"
                : KeyBindings.Name(KeyBindings.All[i]);
    }
}
