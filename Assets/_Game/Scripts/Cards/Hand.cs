using System;
using System.Collections.Generic;

// Mão de cartas: C# puro (não é MonoBehaviour). A UI (HandView) só OUVE os eventos.
public class Hand
{
    public int MaxSize { get; }
    public int Count => cards.Count;
    public bool IsFull => cards.Count >= MaxSize;
    public bool IsEmpty => cards.Count == 0;
    public IReadOnlyList<Card> Cards => cards;

    public int SelectedIndex { get; private set; }
    public Card Selected => cards[SelectedIndex];

    // Eventos (≈ signals do Godot)
    public event Action<int> OnCardAdded;          // índice novo
    public event Action<int, Card> OnCardRemoved;  // índice antigo, carta
    public event Action<int, Card> OnCardReplaced; // índice, carta nova (troca de naipe)
    public event Action OnSelectionChanged;

    private readonly List<Card> cards = new List<Card>();

    public Hand(int maxSize) { MaxSize = maxSize; }

    public Card this[int index] => cards[index];

    public bool Add(Card card) => Insert(cards.Count, card);

    public bool Insert(int index, Card card)
    {
        if (IsFull) return false;
        index = Math.Clamp(index, 0, cards.Count);
        cards.Insert(index, card);
        OnCardAdded?.Invoke(index);
        if (cards.Count == 1) Select(0);
        else if (index <= SelectedIndex && cards.Count > 1) { SelectedIndex++; OnSelectionChanged?.Invoke(); }
        return true;
    }

    public Card RemoveAt(int index)
    {
        Card card = cards[index];
        cards.RemoveAt(index);
        OnCardRemoved?.Invoke(index, card);

        // Mantém a seleção numa carta válida (a próxima à direita, ou a última).
        if (index < SelectedIndex) SelectedIndex--;
        SelectedIndex = cards.Count == 0 ? 0 : Math.Clamp(SelectedIndex, 0, cards.Count - 1);
        OnSelectionChanged?.Invoke();
        return card;
    }

    public void Replace(int index, Card card)
    {
        cards[index] = card;
        OnCardReplaced?.Invoke(index, card);
    }

    public List<Card> RemoveAll()
    {
        var removed = new List<Card>(cards);
        for (int i = cards.Count - 1; i >= 0; i--) RemoveAt(i);
        return removed;
    }

    public void Select(int index)
    {
        if (cards.Count == 0) return;
        SelectedIndex = Math.Clamp(index, 0, cards.Count - 1);
        OnSelectionChanged?.Invoke();
    }

    // Scroll: anda para o lado e dá a volta nas pontas.
    public void MoveSelection(int delta)
    {
        if (cards.Count == 0) return;
        int i = (SelectedIndex + delta) % cards.Count;
        if (i < 0) i += cards.Count;
        Select(i);
    }
}
