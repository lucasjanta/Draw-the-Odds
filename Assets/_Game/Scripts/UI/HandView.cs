using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Mão em leque na parte de baixo da tela. Ouve os eventos da Hand e da fusão.
public class HandView : MonoBehaviour
{
    private const float BaseY = 30f;          // centro das cartas (px acima da borda de baixo)
    private const float SelectedLift = 16f;
    private const float MaxSpacing = 32f;
    private const float MaxWidth = 300f;

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
        var rt = UIFactory.Rect("Hand", parent).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(10f, 10f));
        var v = rt.gameObject.AddComponent<HandView>();
        v.root = rt;
        v.gm = gm;
        v.hand = gm.Hand;
        v.group = rt.gameObject.AddComponent<CanvasGroup>();

        v.emptyLabel = UIFactory.Text("Empty", rt, "NO CARDS - GRAB A PACK!", 10f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
        v.emptyLabel.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(300f, 20f));

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
        view.Rect.anchoredPosition = new Vector2(240f, -40f); // entra pela direita, de baixo
        view.Rect.localRotation = Quaternion.Euler(0f, 0f, -30f);
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

    private void LateUpdate()
    {
        int n = views.Count;
        emptyLabel.gameObject.SetActive(n == 0 && gm.State == GameManager.GameState.Playing);

        float spacing = n > 1 ? Mathf.Min(MaxSpacing, MaxWidth / (n - 1)) : 0f;
        float half = (n - 1) / 2f;
        for (int i = 0; i < n; i++)
        {
            var v = views[i];
            float u = half > 0f ? (i - half) / half : 0f;   // -1 (esquerda) .. 1 (direita)
            bool selected = i == hand.SelectedIndex;
            v.targetPosition = new Vector2((i - half) * spacing, BaseY - u * u * 6f + (selected ? SelectedLift : 0f));
            v.targetRotation = selected ? 0f : -u * 7f;
            v.targetScale = selected ? 1.12f : 1f;
            v.Rect.SetSiblingIndex(i);
        }
        if (n > 0 && hand.SelectedIndex < n) views[hand.SelectedIndex].Rect.SetAsLastSibling();

        // Se o player está atrás das cartas, deixa a mão transparente.
        float targetAlpha = 1f;
        var cam = Camera.main;
        if (cam != null && gm.Player != null)
        {
            var canvas = GetComponentInParent<Canvas>();
            Vector3 sp = cam.WorldToScreenPoint(gm.Player.transform.position + Vector3.up * 0.8f) / canvas.scaleFactor;
            float canvasWidth = ((RectTransform)canvas.transform).rect.width;
            float handHalf = (n - 1) * spacing / 2f + CardView.Width * 0.6f;
            if (sp.y < BaseY + 50f && Mathf.Abs(sp.x - canvasWidth / 2f) < handHalf) targetAlpha = 0.45f;
        }
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 4f);
    }
}
