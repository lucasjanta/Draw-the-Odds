using System;
using UnityEngine;

// Janela de fusão: o TEMPO PARA, o jogador põe 2 cartas nos slots (Q e E) e confirma com o botão direito.
// Depois de confirmar, o tempo continua parado enquanto o painel mostra a animação do merge
// (fase "Resolving"); o painel chama FinishResolve() no fim e o jogo volta.
public class FusionController
{
    private const float DefaultFixedDelta = 0.02f;

    public bool Active { get; private set; }        // janela aberta (inclui a animação)
    public bool Resolving { get; private set; }     // animação do merge tocando
    public PowerType Type { get; private set; }
    public int SlotA { get; private set; } = -1;    // índice na mão (Q)
    public int SlotB { get; private set; } = -1;    // índice na mão (E)
    public float TimeLeft { get; private set; }
    public float Duration { get; private set; }
    public bool BothSlotsFilled => SlotA >= 0 && SlotB >= 0;
    public bool AcceptsInput => Active && !Resolving;

    public event Action OnStarted;
    public event Action OnChanged;
    public event Action<Card, Card, Card> OnResolved; // carta A, carta B, resultado
    public event Action<bool> OnEnded;                // true = fundiu
    public event Action<string> OnRejected;           // erro ao confirmar

    private readonly Hand hand;
    private readonly GameConfig config;
    private bool editingHand;
    private Card pendingResult;   // resultado guardado até o fim da animação (surpresa!)

    public FusionController(Hand hand, GameConfig config)
    {
        this.hand = hand;
        this.config = config;
        // Segurança: se a mão mudar por fora durante a escolha, cancela.
        hand.OnCardRemoved += (_, __) => { if (AcceptsInput && !editingHand) Cancel(); };
    }

    public bool Begin(PowerType type)
    {
        if (Active || hand.Count < 2) return false;
        Active = true;
        Resolving = false;
        Type = type;
        SlotA = SlotB = -1;
        Duration = TimeLeft = config.FusionWindow;
        SetTimeScale(0f); // tempo totalmente parado
        OnStarted?.Invoke();
        OnChanged?.Invoke();
        return true;
    }

    public void Tick()
    {
        if (!AcceptsInput) return;
        TimeLeft -= Time.unscaledDeltaTime; // tempo REAL
        if (TimeLeft <= 0f) End(false);
    }

    public void PutInSlot(bool slotA)
    {
        if (!AcceptsInput || hand.Count == 0) return;
        int index = hand.SelectedIndex;
        if (slotA)
        {
            if (SlotB == index) SlotB = -1;
            SlotA = index;
            if (SlotB < 0) hand.MoveSelection(1); // facilita: já aponta para a próxima carta
        }
        else
        {
            if (SlotA == index) SlotA = -1;
            SlotB = index;
        }
        OnChanged?.Invoke();
    }

    public bool TryConfirm()
    {
        if (!AcceptsInput) return false;
        if (!BothSlotsFilled) { OnRejected?.Invoke("PUT CARDS IN Q AND E"); return false; }

        Card a = hand[SlotA], b = hand[SlotB];
        if (!FusionRules.Validate(Type, a, b, config.AllowMixedSuits, out string error))
        {
            OnRejected?.Invoke(error);
            return false;
        }

        // A mão só muda no fim da animação, para o resultado não aparecer antes da hora.
        pendingResult = FusionRules.Resolve(Type, a, b);
        Resolving = true;
        OnResolved?.Invoke(a, b, pendingResult);
        return true;
    }

    // Chamado pelo painel quando a animação do merge acaba: aplica o resultado na mão.
    public void FinishResolve()
    {
        if (!Resolving) return;
        ApplyPending();
        End(true);
    }

    private void ApplyPending()
    {
        int first = Mathf.Min(SlotA, SlotB);
        editingHand = true;
        hand.RemoveAt(Mathf.Max(SlotA, SlotB));
        hand.RemoveAt(first);
        hand.Insert(first, pendingResult);
        hand.Select(first);
        editingHand = false;
    }

    public void Cancel()
    {
        if (!Active) return;
        if (Resolving) ApplyPending(); // já tinha confirmado: não perde a fusão
        End(Resolving);
    }

    private void End(bool success)
    {
        Active = false;
        Resolving = false;
        SlotA = SlotB = -1;
        SetTimeScale(1f);
        OnEnded?.Invoke(success);
    }

    public static void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        // fixedDeltaTime acompanha a câmera lenta (física suave); com o tempo parado não precisa mudar.
        if (scale > 0f) Time.fixedDeltaTime = DefaultFixedDelta * scale;
    }
}
