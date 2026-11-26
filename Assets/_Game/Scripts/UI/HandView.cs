using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Mão de cartas na parte de baixo da tela. Ouve os eventos da Hand e da fusão.
// PC: canto inferior ESQUERDO. Celular (controles de toque): no MEIO, para o joystick ficar à
// esquerda e os botões à direita.
// Cada carta pode ser tocada (seleciona) ou arrastada:
//  - soltar acima da mão = arremessa na direção do dedo/mouse;
//  - durante a fusão, soltar em cima de um slot = coloca a carta nele.
public class HandView : MonoBehaviour
{
    private const float BaseY = 12f;          // centro das cartas (px acima da borda de baixo): topo em ~52 px
    private const float SelectedLift = 10f;   // a selecionada só sobe um pouco (não vai para a frente das outras)
    private const float LeftMargin = 8f;      // distância da borda esquerda da tela (layout do PC)
    private const float Spacing = 34f;        // passo entre cartas: deixa o número e o naipe do canto sempre à mostra
    private const float ThrowLine = 74f;      // soltar a carta acima desta altura (px do Canvas) = arremessar

    private static readonly Color SlotAColor = new Color32(0xFF, 0xA5, 0x2E, 0xFF);
    private static readonly Color SlotBColor = new Color32(0xFF, 0x6E, 0xC7, 0xFF);

    private GameManager gm;
    private Hand hand;
    private RectTransform root;
    private CanvasGroup group;
    private TextMeshProUGUI emptyLabel;
    private readonly List<CardView> views = new List<CardView>();
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private CardView dragged;
    private Vector2 dragTarget;

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
        view.Rect.anchoredPosition = new Vector2(SlotX(index, hand.Count), -60f); // sobe de baixo da tela até o lugar dela
        view.Rect.localRotation = Quaternion.Euler(0f, 0f, -12f);
        var drag = CardDragHandler.Attach(view);
        drag.Tapped = HandleTap;
        drag.DragStarted = HandleDragStart;
        drag.Dragging = HandleDrag;
        drag.DragEnded = HandleDragEnd;
        views.Insert(index, view);
        Refresh();
    }

    private void HandleRemoved(int index, Card card)
    {
        var view = views[index];
        views.RemoveAt(index);
        if (view == dragged) { dragged = null; view.GetComponent<CardDragHandler>().CancelDrag(); }
        view.FlyAwayAndDestroy(new Vector2(0f, 260f));
        Refresh();
    }

    private void Refresh()
    {
        var fusion = gm.Fusion;
        bool touch = VirtualInput.Active;
        for (int i = 0; i < views.Count; i++)
        {
            views[i].SetSelected(i == hand.SelectedIndex);
            // Rótulo do slot: Q/E no teclado, 1/2 no toque (não há teclas)
            if (fusion.Active && i == fusion.SlotA) views[i].SetTag(touch ? "1" : KeyBindings.Name(GameKey.SlotA), SlotAColor);
            else if (fusion.Active && i == fusion.SlotB) views[i].SetTag(touch ? "2" : KeyBindings.Name(GameKey.SlotB), SlotBColor);
            else views[i].SetTag(null, Color.clear);
        }
    }

    // ===================== Toque / arrasto =====================
    private bool CanUseCards => gm.CanAct && !gm.Fusion.Resolving && (!gm.Fusion.Active || gm.Fusion.AcceptsInput);

    private void HandleTap(CardDragHandler h)
    {
        int i = views.IndexOf(h.View);
        if (i < 0 || !CanUseCards) return;
        if (hand.SelectedIndex != i)
        {
            hand.Select(i);
            AudioManager.Ui(gm.Config.SfxCardSelect, 0.7f);
        }
    }

    private void HandleDragStart(CardDragHandler h, PointerEventData e)
    {
        int i = views.IndexOf(h.View);
        if (i < 0 || !CanUseCards) { h.CancelDrag(); return; }
        if (hand.SelectedIndex != i) hand.Select(i);
        dragged = h.View;
        dragged.transform.SetAsLastSibling();
        HandleDrag(h, e);
    }

    private void HandleDrag(CardDragHandler h, PointerEventData e)
    {
        if (h.View != dragged) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, e.position, e.pressEventCamera, out var local);
        dragTarget = local - root.rect.center; // as cartas usam a âncora do centro da "Hand"
    }

    private void HandleDragEnd(CardDragHandler h, PointerEventData e)
    {
        if (h.View != dragged) return;
        dragged = null;
        int i = views.IndexOf(h.View);
        if (i < 0 || !CanUseCards) return;

        if (gm.Fusion.Active)
        {
            int slot = SlotUnder(e.position);
            if (slot < 0) return; // soltou fora: a carta volta para a mão
            hand.Select(i);
            gm.Fusion.PutInSlot(slot == 0);
            return;
        }

        // Soltou acima da mão: arremessa na direção do dedo/mouse.
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)root.parent, e.position, e.pressEventCamera, out var canvasLocal);
        float heightFromBottom = canvasLocal.y - ((RectTransform)root.parent).rect.yMin;
        if (heightFromBottom < ThrowLine) return;
        hand.Select(i);
        gm.Player.GetComponent<PlayerCards>().ThrowAtScreen(e.position);
    }

    // Slot da fusão debaixo do ponto (0 = primeiro, 1 = segundo, -1 = nenhum).
    private int SlotUnder(Vector2 screenPos)
    {
        var es = EventSystem.current;
        if (es == null) return -1;
        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = screenPos }, hits);
        foreach (var hit in hits)
        {
            var target = hit.gameObject.GetComponentInParent<FusionSlotTarget>();
            if (target != null) return target.IsFirst ? 0 : 1;
        }
        return -1;
    }

    // ===================== Layout =====================
    // Centro (x) da carta i: a partir da borda esquerda (PC) ou centralizado (toque).
    private float SlotX(int i, int count)
    {
        if (!VirtualInput.Active) return LeftMargin + CardView.Width / 2f + i * Spacing;
        float canvasWidth = ((RectTransform)root.parent).rect.width;
        float total = CardView.Width + Mathf.Max(0, count - 1) * Spacing;
        return (canvasWidth - total) / 2f + CardView.Width / 2f + i * Spacing;
    }

    private void LateUpdate()
    {
        int n = views.Count;
        emptyLabel.gameObject.SetActive(n == 0 && gm.State == GameManager.GameState.Playing);
        if (VirtualInput.Active)
        {
            emptyLabel.alignment = TextAlignmentOptions.Center;
            emptyLabel.rectTransform.anchoredPosition = new Vector2(((RectTransform)root.parent).rect.width / 2f - 150f, 26f);
        }
        else
        {
            emptyLabel.alignment = TextAlignmentOptions.Left;
            emptyLabel.rectTransform.anchoredPosition = new Vector2(LeftMargin, 26f);
        }

        for (int i = 0; i < n; i++)
        {
            var v = views[i];
            if (v == dragged)
            {
                v.targetPosition = dragTarget;
                v.targetScale = 1f;
                continue; // a carta arrastada segue o dedo e fica por cima das outras
            }
            bool selected = i == hand.SelectedIndex;
            v.targetPosition = new Vector2(SlotX(i, n), BaseY + (selected ? SelectedLift : 0f));
            v.targetRotation = 0f;
            v.targetScale = 1f; // escala inteira = pixel art nítida (a selecionada só sobe e ganha contorno)
            v.Rect.SetSiblingIndex(i); // ordem fixa: a da direita cobre só a metade direita da anterior
        }
        if (dragged != null) dragged.Rect.SetAsLastSibling();

        // A mão fica abaixo dos pés do player (sem transparência), mas some nas telas de fim (derrota/vitória),
        // que ficam por baixo dela na ordem da UI.
        var st = gm.State;
        float targetAlpha = st == GameManager.GameState.GameOver || st == GameManager.GameState.Victory ? 0f : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 4f);
        group.blocksRaycasts = targetAlpha > 0f;
    }
}
