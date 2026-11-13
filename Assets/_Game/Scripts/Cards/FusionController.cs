using System;
using UnityEngine;

// Janela de fusão: o TEMPO PARA, o jogador põe 2 cartas nos slots (Q e E) e confirma com o botão ESQUERDO.
// O botão direito pula o poder (o orbe é gasto sem usar).
// Depois de confirmar, o tempo continua parado enquanto o painel mostra a animação do merge
// (fase "Resolving"); o painel chama FinishResolve() no fim e o jogo volta.
public class FusionController
{

    public bool Active { get; private set; }        // janela aberta (inclui a animação)
    public bool Resolving { get; private set; }     // animação do merge tocando
    public PowerType Type { get; private set; }
    public int SlotA { get; private set; } = -1;    // índice na mão (Q)
    public int SlotB { get; private set; } = -1;    // índice na mão (E)
    public float TimeLeft { get; private set; }
    public float Duration { get; private set; }
    public bool BothSlotsFilled => SlotA >= 0 && SlotB >= 0;
    public bool AcceptsInput => Active && !Resolving;
    // Tutorial: sem limite de tempo (o relógio não anda).
    public bool Unlimited { get; set; }
    // Dificuldade (o GameManager preenche): segundos para escolher (0 = sem limite) e se mostra o resultado antes.
    public Func<float> WindowProvider { get; set; }
    public Func<bool> PreviewProvider { get; set; }
    public bool NoTimer { get; private set; }          // EASY: o relógio não anda
    public bool ShowsPreview => PreviewProvider != null && PreviewProvider();

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
        float window = WindowProvider != null ? WindowProvider() : config.FusionWindow;
        NoTimer = window <= 0f;
        Duration = TimeLeft = NoTimer ? 1f : window;
        previewA = previewB = -1;
        SetTimeScale(0f); // tempo totalmente parado
        OnStarted?.Invoke();
        OnChanged?.Invoke();
        return true;
    }

    public void Tick()
    {
        if (!AcceptsInput || Unlimited || NoTimer) return;
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
        AudioManager.Sfx(config.SfxMixSelect, 0.8f);
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
        // (EASY: usa o mesmo resultado que o preview mostrou, inclusive o naipe sorteado.)
        pendingResult = TryPreview(out Card preview, out _) ? preview : FusionRules.Resolve(Type, a, b);
        Resolving = true;
        OnResolved?.Invoke(a, b, pendingResult);
        return true;
    }

    // Preview (EASY): o resultado das cartas nos slots. O naipe de naipes diferentes é sorteado UMA vez
    // por par de cartas e guardado, para o preview e a fusão darem o mesmo resultado.
    private int previewA = -1, previewB = -1;
    private Card previewCard;

    public bool TryPreview(out Card result, out string error)
    {
        result = default;
        error = null;
        if (!BothSlotsFilled) return false;
        Card a = hand[SlotA], b = hand[SlotB];
        if (!FusionRules.Validate(Type, a, b, config.AllowMixedSuits, out error)) return false;
        if (previewA != SlotA || previewB != SlotB || previewCard.value != FusionRules.Compute(Type, a.value, b.value))
        {
            previewCard = FusionRules.Resolve(Type, a, b);
            previewA = SlotA;
            previewB = SlotB;
        }
        result = previewCard;
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

    // Botão direito: desiste deste poder (o orbe já foi gasto).
    public void Skip()
    {
        if (!AcceptsInput) return;
        End(false);
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

    // Mantido por compatibilidade: quem controla o tempo agora é o GameTime (que respeita a pausa).
    public static void SetTimeScale(float scale) => GameTime.SetScale(scale);
}
