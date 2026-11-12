using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Mão em leque na parte de baixo da tela. Ouve os eventos da Hand e da fusão.
public class HandView : MonoBehaviour
{
    // Mão no canto inferior ESQUERDO, cartas lado a lado (sem leque) para todos os números ficarem visíveis.
    private const float BaseY = 12f;          // centro das cartas (px acima da borda de baixo): topo em ~52 px
    private const float SelectedLift = 10f;   // a selecionada só sobe um pouco (não vai para a frente das outras)
    private const float LeftMargin = 8f;      // distância da borda esquerda da tela
    private const float Spacing = 34f;        // passo entre cartas: deixa o número e o naipe do canto sempre à mostra

    private static readonly Color SlotAColor = new Color32(0xFF, 0xA5, 0x2E, 0xFF);
    private static readonly Color SlotBColor = new Color32(0xFF, 0x6E, 0xC7, 0xFF);

    private GameManager gm;
    private Hand hand;
    private RectTransform root;
    private CanvasGroup group;
    private TextMeshProUGUI emptyLabel;
    private readonly List<CardView> views = new List<CardView>();

    public static HandView Create(Transform parent, GameManager gm)
    {
        var rt = UIFactory.Rect("Hand", parent).Place(new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(10f, 10f));
        var v = rt.gameObject.AddComponent<HandView>();
        v.root = rt;
        v.gm = gm;
        v.hand = gm.Hand;
        v.group = rt.gameObject.AddComponent<CanvasGroup>();

        v.emptyLabel = UIFactory.Text("Empty", rt, "NO CARDS - GRAB A PACK!", 10f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
        v.emptyLabel.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(LeftMargin, 26f), new Vector2(300f, 20f));
        v.emptyLabel.alignment = TextAlignmentOptions.Left;

        v.hand.OnCardAdded += v.HandleAdded;
        v.hand.OnCardRemoved += v.HandleRemoved;
        v.hand.OnSelectionChanged += v.Refresh;
        gm.Fusion.OnChanged += v.Refresh;
        gm.Fusion.OnEnded += _ => v.Refresh();
        return v;
    }

    private void HandleAdded(int index)
    {
        var view = CardView.Create(root, gm.Config, true);
        view.SetCard(hand[index]);
        view.Rect.anchoredPosition = new Vector2(SlotX(index), -60f); // sobe de baixo da tela até o lugar dela
        view.Rect.localRotation = Quaternion.Euler(0f, 0f, -12f);
        views.Insert(index, view);
        Refresh();
    }

    private void HandleRemoved(int index, Card card)
    {
        var view = views[index];
        views.RemoveAt(index);
        view.FlyAwayAndDestroy(new Vector2(0f, 260f));
        Refresh();
    }

    private void Refresh()
    {
        var fusion = gm.Fusion;
        for (int i = 0; i < views.Count; i++)
        {
            views[i].SetSelected(i == hand.SelectedIndex);
            if (fusion.Active && i == fusion.SlotA) views[i].SetTag("Q", SlotAColor);
            else if (fusion.Active && i == fusion.SlotB) views[i].SetTag("E", SlotBColor);
            else views[i].SetTag(null, Color.clear);
        }
    }

    // Centro (x) da carta i a partir da borda esquerda.
    private static float SlotX(int i) => LeftMargin + CardView.Width / 2f + i * Spacing;

    private void LateUpdate()
    {
        int n = views.Count;
        emptyLabel.gameObject.SetActive(n == 0 && gm.State == GameManager.GameState.Playing);

        for (int i = 0; i < n; i++)
        {
            var v = views[i];
            bool selected = i == hand.SelectedIndex;
            v.targetPosition = new Vector2(SlotX(i), BaseY + (selected ? SelectedLift : 0f));
            v.targetRotation = 0f;
            v.targetScale = 1f; // escala inteira = pixel art nítida (a selecionada só sobe e ganha contorno)
            v.Rect.SetSiblingIndex(i); // ordem fixa: a da direita cobre só a metade direita da anterior
        }

        // A mão fica abaixo dos pés do player, então não precisa mais ficar transparente.
        float targetAlpha = 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 4f);
    }
}
