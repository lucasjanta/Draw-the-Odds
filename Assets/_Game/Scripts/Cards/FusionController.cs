using System;
using UnityEngine;

// Janela de fusão: câmera lenta + 2 slots (Q e E) + confirmar com botão direito.
// C# puro; o GameManager chama Tick() todo frame e o PlayerCards chama PutInSlot/TryConfirm.
public class FusionController
{
    public bool Active { get; private set; }
    public PowerType Type { get; private set; }
    public int SlotA { get; private set; } = -1;   // índice na mão (Q)
    public int SlotB { get; private set; } = -1;   // índice na mão (E)
    public float TimeLeft { get; private set; }
    public float Duration { get; private set; }
    public bool BothSlotsFilled => SlotA >= 0 && SlotB >= 0;

    public event Action OnStarted;
    public event Action OnChanged;
    public event Action<bool> OnEnded;      // true = fundiu
    public event Action<string> OnRejected; // mensagem de erro (ex.: "ZERO")

    private readonly Hand hand;
    private readonly GameConfig config;
    private const float DefaultFixedDelta = 0.02f;

    public FusionController(Hand hand, GameConfig config)
    {
        this.hand = hand;
        this.config = config;
        hand.OnCardRemoved += (_, __) => { if (Active) Cancel(); }; // segurança: a mão mudou no meio
    }

    public bool Begin(PowerType type)
    {
        if (Active || hand.Count < 2) return false;
        Active = true;
        Type = type;
        SlotA = SlotB = -1;
        Duration = TimeLeft = config.FusionWindow;
        SetTimeScale(config.FusionTimeScale);
        OnStarted?.Invoke();
        OnChanged?.Invoke();
        return true;
    }

    public void Tick()
    {
        if (!Active) return;
        TimeLeft -= Time.unscaledDeltaTime;   // tempo REAL (não sofre a câmera lenta)
        if (TimeLeft <= 0f) End(false);
    }

    public void PutInSlot(bool slotA)
    {
        if (!Active || hand.Count == 0) return;
        int index = hand.SelectedIndex;
        if (slotA)
        {
            if (SlotB == index) SlotB = -1;
            SlotA = index;
        }
        else
        {
            if (SlotA == index) SlotA = -1;
            SlotB = index;
        }
        // Facilita: depois de pôr no Q, a seleção pula para a próxima carta.
        if (slotA && SlotB < 0) hand.MoveSelection(1);
        OnChanged?.Invoke();
    }

    public bool TryGetPreview(out FusionRules.Outcome outcome)
    {
        outcome = default;
        if (!Active || !BothSlotsFilled) return false;
        outcome = FusionRules.Preview(Type, hand[SlotA], hand[SlotB], config.AllowMixedSuits);
        return true;
    }

    public bool TryConfirm()
    {
        if (!TryGetPreview(out var outcome)) { OnRejected?.Invoke("FILL Q AND E"); return false; }
        if (!outcome.valid) { OnRejected?.Invoke(outcome.error); return false; }

        var results = FusionRules.Resolve(outcome);
        int a = SlotA, b = SlotB;
        int first = Mathf.Min(a, b);

        Active = false; // desliga antes de mexer na mão (senão o evento de remoção cancela)
        if (outcome.isSwap)
        {
            hand.Replace(a, results[0]);
            hand.Replace(b, results[1]);
            hand.Select(a);
        }
        else
        {
            hand.RemoveAt(Mathf.Max(a, b));
            hand.RemoveAt(first);
            hand.Insert(first, results[0]);
            hand.Select(first);
        }
        Active = true;
        End(true);
        return true;
    }

    public void Cancel()
    {
        if (Active) End(false);
    }

    private void End(bool success)
    {
        Active = false;
        SlotA = SlotB = -1;
        SetTimeScale(1f);
        OnEnded?.Invoke(success);
    }

    public static void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = DefaultFixedDelta * scale; // física suave em câmera lenta
    }
}
