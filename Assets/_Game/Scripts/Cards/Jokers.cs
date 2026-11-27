using System;
using System.Collections.Generic;

// Coringas que o jogador tem NESTA run (o "cinto", até 5). static: sobrevive ao "continuar"
// (a cena recarrega) e é zerado quando uma run nova começa (GameManager chama Clear).
// Os efeitos são lidos pelos sistemas que já existiam (dano, fusão, dash, mão...) pelas propriedades abaixo.
//
// Desde 27/11 todos os naipes dão o MESMO dano: os bônus de cada naipe vêm dos coringas
// (♠ Espada Afiada, ♥ Coração de Ouro, ♦ Bolsa de Ouros, ♣ Trevo) e das EVOLUÇÕES deles.
// Uma evolução troca o coringa original no mesmo espaço do cinto (não ocupa espaço novo).
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

    // Tem o coringa OU a evolução dele (ex.: Espada Afiada ou Espada Real).
    public static bool HasLine(JokerId baseId)
    {
        if (Has(baseId)) return true;
        var evo = JokerCatalog.EvolutionOf(baseId);
        return evo.HasValue && Has(evo.Value);
    }

    public static void Add(JokerId id)
    {
        if (Has(id)) return;
        var def = JokerCatalog.Get(id);
        // Evolução: entra no lugar do coringa de origem
        if (def.evolvesFrom.HasValue)
        {
            int i = belt.IndexOf(def.evolvesFrom.Value);
            if (i >= 0) { belt[i] = id; OnChanged?.Invoke(); return; }
        }
        if (BeltFull) return;
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

    // Lista "0,5,13" (vai para o ranking).
    public static string Code => string.Join(",", belt.ConvertAll(j => ((int)j).ToString()));

    // ===================== Números dos efeitos (um lugar só para balancear) =====================
    // ♠ Espadas: x1,5 (Espada Afiada) · x2 (Espada Real)
    public static float SpadeMultiplier => Has(JokerId.RoyalSword) ? 2f : Has(JokerId.SharpSword) ? 1.5f : 1f;
    // ♣ Paus: +1 de dano (Trevo) · +2 (Trevo de 4 Folhas), só quando a carta já dá dano
    public static int ClubBonus => Has(JokerId.FourLeaf) ? 2 : Has(JokerId.Clover) ? 1 : 0;
    // ♥ Copas: cura quando dá dano (Coração de Ouro 1 · Coração de Leão 2)
    public static int HeartHeal => Has(JokerId.LionHeart) ? 2 : Has(JokerId.GoldenHeart) ? 1 : 0;
    // ♦ Ouros: compra cartas quando dá dano (Bolsa 1 · Mina 2; +1 no crítico) e a Mina dá 1 ficha por acerto
    public static int DiamondCards => Has(JokerId.GoldMine) ? 2 : Has(JokerId.GoldPurse) ? 1 : 0;
    public static int DiamondChips => Has(JokerId.GoldMine) ? 1 : 0;
    // Tudo ou Nada: +1 de dano em todo acerto, mas −3 de vida máxima
    public static int FlatBonus => Has(JokerId.AllIn) ? 1 : 0;
    public static int MaxHpDelta => Has(JokerId.AllIn) ? -3 : 0;
    // Ás no Bolso: crítico +3
    public static int CritBonus => Has(JokerId.PocketAce) ? 3 : 0;
    // Cofrinho: fichas ganhas na run x1,5
    public static float ChipMultiplier => Has(JokerId.PiggyBank) ? 1.5f : 1f;

    public static float DashCooldownMultiplier => Has(JokerId.RabbitFoot) ? 0.6f : 1f;
    public static int ExtraMaxHand => Has(JokerId.BigHand) ? 2 : 0;
    public static int ExtraStartingHand => Has(JokerId.BigHand) ? 1 : 0;
    public static float ExtraFusionSeconds => Has(JokerId.StoppedClock) ? 5f : 0f;
    public static bool FusionPreview => Has(JokerId.StoppedClock);
    public static bool WildMerges => Has(JokerId.WildCard);
    public static bool NearMissCrit => Has(JokerId.NearMiss);
    public static bool ShowParity => HasLine(JokerId.LoadedDie);
    public static bool ShowHalf => Has(JokerId.HawkEye);       // Olho de Lince: também diz se é a metade de baixo ou de cima
}
