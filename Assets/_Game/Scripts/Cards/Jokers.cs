using System;
using System.Collections.Generic;

// Coringas que o jogador tem NESTA run (o "cinto", até 5). static: sobrevive ao "continuar"
// (a cena recarrega) e é zerado quando uma run nova começa (GameManager chama Clear).
// Os efeitos são lidos pelos sistemas que já existiam (dano, fusão, dash, mão...) com Jokers.Has(...).
public static class Jokers
{
    public const int MaxBelt = 5;

    private static readonly List<JokerId> belt = new List<JokerId>();
    public static IReadOnlyList<JokerId> Belt => belt;
    public static bool BeltFull => belt.Count >= MaxBelt;

    public static event Action OnChanged;

    // Carta Marcada: 1 uso por chefe.
    private static bool markedUsedThisBoss;

    public static bool Has(JokerId id) => belt.Contains(id);

    public static void Add(JokerId id)
    {
        if (Has(id) || BeltFull) return;
        belt.Add(id);
        OnChanged?.Invoke();
    }

    public static void Clear()
    {
        belt.Clear();
        markedUsedThisBoss = false;
        OnChanged?.Invoke();
    }

    // Novo chefe (ou nova rodada): recarrega os efeitos "por chefe".
    public static void NewBoss() => markedUsedThisBoss = false;

    // Carta Marcada: a próxima carta que acertar vira o número exato (1x por chefe).
    public static bool TryUseMarked()
    {
        if (!Has(JokerId.MarkedCard) || markedUsedThisBoss) return false;
        markedUsedThisBoss = true;
        return true;
    }

    // ===================== Números dos efeitos (um lugar só para balancear) =====================
    public static float SpadeMultiplier => Has(JokerId.SharpSword) ? 2f : 1.5f;
    public static int ExtraHeal => Has(JokerId.GoldenHeart) ? 1 : 0;
    public static int ExtraDiamondCards => Has(JokerId.GoldenHeart) ? 1 : 0;
    public static float DashCooldownMultiplier => Has(JokerId.RabbitFoot) ? 0.6f : 1f;
    public static int ExtraMaxHand => Has(JokerId.BigHand) ? 2 : 0;
    public static int ExtraStartingHand => Has(JokerId.BigHand) ? 1 : 0;
    public static float ExtraFusionSeconds => Has(JokerId.StoppedClock) ? 5f : 0f;
    public static bool FusionPreview => Has(JokerId.StoppedClock);
    public static bool WildMerges => Has(JokerId.WildCard);
    public static bool NearMissCrit => Has(JokerId.NearMiss);
    public static bool ShowParity => Has(JokerId.LoadedDie);
}
