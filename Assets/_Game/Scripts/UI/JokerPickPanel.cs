using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tela "ESCOLHA UM CORINGA": 3 cartas grandes (ícone, nome, efeito). Clique ou toque escolhe.
// Aparece no começo da run e entre os chefes (o jogo fica parado, nada mais na tela).
public class JokerPickPanel : MonoBehaviour
{
    private const float CardW = 168f, CardH = 176f, Gap = 186f;

    private readonly List<GameObject> cards = new List<GameObject>();
    private Transform row;
    private TextMeshProUGUI beltText;
    private Action<JokerId> onPick;
    private float openTime;

    public static JokerPickPanel Create(Transform parent)
    {
        var dim = UIFactory.Image("JokerPick", parent, new Color(0.04f, 0.02f, 0.09f, 0.92f));
        dim.raycastTarget = true;
        dim.rectTransform.Stretch();
        var p = dim.gameObject.AddComponent<JokerPickPanel>();

        var title = UIFactory.Text("Title", dim.transform, "CHOOSE A JOKER", 24f, UIFactory.Gold, TextAlignmentOptions.Center);
        title.Outline(2f, new Color32(0x7A, 0x2E, 0x0E, 0xFF));
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(600f, 30f));

        p.row = UIFactory.Rect("Row", dim.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(600f, CardH));

        p.beltText = UIFactory.Text("Belt", dim.transform, "", 9f, new Color(1f, 1f, 1f, 0.65f), TextAlignmentOptions.Center).NoLoc();
        p.beltText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(600f, 14f));

        dim.gameObject.SetActive(false);
        return p;
    }

    public void Open(List<JokerId> choices, Action<JokerId> onPick)
    {
        this.onPick = onPick;
        foreach (var c in cards) Destroy(c);
        cards.Clear();
        for (int i = 0; i < choices.Count; i++)
        {
            float x = (i - (choices.Count - 1) / 2f) * Gap;
            cards.Add(BuildCard(JokerCatalog.Get(choices[i]), x, i));
        }
        beltText.text = Loc.Pick("Jokers last the whole run", "Os coringas duram a run inteira", "Los comodines duran toda la partida",
                                 "Les jokers durent toute la partie", "Joker gelten den ganzen Lauf", "I jolly durano tutta la partita")
                        + $"  ·  {Jokers.Belt.Count}/{Jokers.MaxBelt}";
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        openTime = Time.unscaledTime;
    }

    public void Close() => gameObject.SetActive(false);

    private GameObject BuildCard(JokerDef def, float x, int index)
    {
        var button = UIFactory.Button("Joker" + index, row, "", new Vector2(CardW, CardH), () => Pick(def.id));
        var rt = button.GetComponent<RectTransform>();
        rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(CardW, CardH));
        // Moldura na cor do coringa (por baixo do miolo, que é quem muda de cor no hover)
        var frame = UIFactory.Image("Frame", rt, def.color);
        frame.rectTransform.Stretch();
        frame.transform.SetAsFirstSibling();
        var inner = button.GetComponent<Image>();
        inner.color = new Color(0.12f, 0.08f, 0.2f, 1f);
        var face = UIFactory.Image("Face", rt, new Color(0.12f, 0.08f, 0.2f, 1f));
        face.rectTransform.Stretch(3f);
        face.raycastTarget = false;

        JokerIcon.Create(rt, def, 52f).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(52f, 52f));

        var name = UIFactory.Text("Name", rt, def.Name, 13f, def.color, TextAlignmentOptions.Center).NoLoc();
        name.Outline(1f);
        name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(CardW - 12f, 18f));

        var desc = UIFactory.Text("Desc", rt, def.Desc, 9f, Color.white, TextAlignmentOptions.Top).NoLoc();
        desc.textWrappingMode = TextWrappingModes.Normal;
        desc.lineSpacing = 4f;
        var fit = desc.GetComponent<TextFit>();
        if (fit != null) Destroy(fit); // quebra linha em vez de encolher
        desc.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(CardW - 20f, 70f));
        return button.gameObject;
    }

    private void Pick(JokerId id)
    {
        if (Time.unscaledTime < openTime + 0.35f) return; // evita escolher sem querer com o clique anterior
        AudioManager.Sfx(GameManager.Instance != null ? GameManager.Instance.Config.SfxPowerUp : null, 0.9f, 0f);
        onPick?.Invoke(id);
    }

    // Debug (ponte): escolhe a carta n (0..2) sem clicar.
    public static string DebugPick(string index)
    {
        var p = FindAnyObjectByType<JokerPickPanel>();
        if (p == null || !p.gameObject.activeSelf || p.onPick == null) return "panel closed";
        var name = p.cards[Mathf.Clamp(int.Parse(index), 0, p.cards.Count - 1)].name;
        // o nome do botão não guarda o id: reabre pelo texto não é confiável, então usa a ordem do Open
        p.openTime = -1f;
        p.cards[Mathf.Clamp(int.Parse(index), 0, p.cards.Count - 1)].GetComponent<Button>().onClick.Invoke();
        return "picked " + name;
    }
}

// Ícone de um coringa: uma cartinha com moldura na cor dele e o símbolo no meio.
public static class JokerIcon
{
    public static RectTransform Create(Transform parent, JokerDef def, float size)
    {
        var frame = UIFactory.Image("JokerIcon", parent, UIFactory.OutlineColor);
        frame.raycastTarget = false;
        var color = UIFactory.Image("Color", frame.transform, def.color);
        color.rectTransform.Stretch(Mathf.Max(1f, size / 26f));
        color.raycastTarget = false;
        var face = UIFactory.Image("Face", frame.transform, new Color(0.1f, 0.07f, 0.16f, 1f));
        face.rectTransform.Stretch(Mathf.Max(2f, size / 9f));
        face.raycastTarget = false;
        var sym = UIFactory.Text("Symbol", frame.transform, def.symbol, size * 0.42f, def.color, TextAlignmentOptions.Center).NoLoc();
        sym.fontStyle = FontStyles.Bold;
        sym.raycastTarget = false;
        sym.rectTransform.Stretch();
        return frame.rectTransform;
    }
}
