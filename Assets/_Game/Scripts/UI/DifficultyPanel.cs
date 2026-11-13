using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tela "CHOOSE YOUR ODDS": 3 cartões clicáveis (EASY, NORMAL recomendado, HARDCORE).
// Aparece no fim do tutorial e no Skip Tutorial. Quem guarda a escolha é o GameManager.
public class DifficultyPanel : MonoBehaviour
{
    private static readonly Color EasyColor = new Color32(0x3C, 0xC8, 0x6E, 0xFF);
    private static readonly Color HardColor = new Color32(0xE8, 0x4A, 0x5F, 0xFF);

    private Action<Difficulty> onPick;
    private readonly Image[] frames = new Image[3];
    private readonly RectTransform[] cards = new RectTransform[3];
    private int hovered = -1;

    public static DifficultyPanel Create(Transform parent)
    {
        var dim = UIFactory.Image("DifficultyPanel", parent, new Color(0.04f, 0.02f, 0.09f, 0.9f));
        dim.raycastTarget = true;
        dim.rectTransform.Stretch();
        var p = dim.gameObject.AddComponent<DifficultyPanel>();

        var title = UIFactory.Text("Title", dim.transform, "CHOOSE YOUR ODDS", 24f, UIFactory.Gold, TextAlignmentOptions.Center);
        title.Outline(2f, new Color32(0x7A, 0x2E, 0x0E, 0xFF));
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 30f));

        p.Card(dim.transform, Difficulty.Easy, -196f, "EASY", EasyColor, null,
            "See the merge result\nNo merge timer\nMore orbs\nBosses attack less\nOwn ranking");
        p.Card(dim.transform, Difficulty.Normal, 0f, "NORMAL", UIFactory.Gold, "RECOMMENDED",
            "The game as designed\n10s merge timer\nFair, fast bosses\nOwn ranking");
        p.Card(dim.transform, Difficulty.Hardcore, 196f, "HARDCORE", HardColor, null,
            "6s merge timer\nFaster attacks\nAttacks more often\nUp to 5 charges\nOwn ranking");

        var hint = UIFactory.Text("Hint", dim.transform, "Click a card. Dying restarts from the JACK.", 9f,
            new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
        hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(600f, 14f));

        dim.gameObject.SetActive(false);
        return p;
    }

    // Um cartão: moldura colorida + nome + selo "RECOMMENDED" + lista do que muda.
    private void Card(Transform parent, Difficulty d, float x, string name, Color color, string badge, string lines)
    {
        int i = (int)d;
        var button = UIFactory.Button(name, parent, "", new Vector2(180f, 156f), () => onPick?.Invoke(d));
        Destroy(button.GetComponent<ButtonJuice>()); // o cartão já tem a própria animação de hover
        var rt = button.GetComponent<RectTransform>();
        rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -10f), new Vector2(180f, 156f));
        cards[i] = rt;

        // O fundo do botão fica invisível (só recebe o clique); por cima vão a moldura de 2 px na cor
        // do modo e o miolo, que é quem muda de cor no hover/clique.
        button.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        var frame = UIFactory.Image("Frame", rt, color);
        frame.rectTransform.Stretch();
        frames[i] = frame;
        var inner = UIFactory.Image("Inner", rt, Color.white);
        inner.rectTransform.Stretch(2f);
        button.targetGraphic = inner;

        var title = UIFactory.Text("Name", rt, name, 18f, color, TextAlignmentOptions.Center);
        title.Outline(1f);
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(170f, 24f));

        if (badge != null)
        {
            var tag = UIFactory.Image("Badge", rt, color);
            tag.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(110f, 14f));
            var tagText = UIFactory.Text("BadgeText", tag.transform, badge, 9f, new Color32(0x1A, 0x10, 0x05, 0xFF), TextAlignmentOptions.Center);
            tagText.rectTransform.Stretch();
        }

        var body = UIFactory.Text("Lines", rt, lines, 9f, Color.white, TextAlignmentOptions.Top);
        body.lineSpacing = 12f;
        body.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(166f, 120f));
    }

    public void Open(Difficulty current, Action<Difficulty> onPick)
    {
        this.onPick = onPick;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        hovered = (int)current;
    }

    public void Close() => gameObject.SetActive(false);

    // O cartão sob o mouse cresce um pouco e a moldura pisca de leve.
    private void Update()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            bool over = RectTransformUtility.RectangleContainsScreenPoint(cards[i], UnityEngine.InputSystem.Mouse.current != null
                ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero);
            float target = over ? 1.06f : 1f;
            float s = Mathf.MoveTowards(cards[i].localScale.x, target, Time.unscaledDeltaTime * 1.5f);
            cards[i].localScale = new Vector3(s, s, 1f);
            var c = frames[i].color;
            c.a = over ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
            frames[i].color = c;
        }
    }

    // Debug (ponte): escolhe sem clicar.
    public string DebugPick(Difficulty d)
    {
        if (!gameObject.activeSelf) return "panel closed";
        onPick?.Invoke(d);
        return "picked " + d;
    }
}
