using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Recebe toque/clique e arrasto numa carta da mão e repassa para a HandView (que sabe o que fazer).
// Funciona com mouse também: no PC dá para arrastar a carta até o chefe ou até o slot da fusão.
public class CardDragHandler : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IInteractiveUi
{
    public CardView View { get; private set; }
    public Action<CardDragHandler> Tapped;
    public Action<CardDragHandler, PointerEventData> DragStarted, Dragging, DragEnded;
    public bool IsDragging { get; private set; }

    public static CardDragHandler Attach(CardView view)
    {
        var h = view.gameObject.AddComponent<CardDragHandler>();
        h.View = view;
        view.SetTouchable(true);
        return h;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!e.dragging) Tapped?.Invoke(this);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        IsDragging = true;
        DragStarted?.Invoke(this, e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (IsDragging) Dragging?.Invoke(this, e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!IsDragging) return;
        IsDragging = false;
        DragEnded?.Invoke(this, e);
    }

    // Carta removida/escondida no meio do arrasto: termina sem fazer nada.
    public void CancelDrag() => IsDragging = false;
}
