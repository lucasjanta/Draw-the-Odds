using UnityEngine;

public enum HintDirection { Exact, Higher, Lower } // Higher = o número secreto é MAIOR que a carta

// Resultado de uma carta acertando o chefe.
public struct HitResult
{
    public Card card;
    public int distance;
    public int amount;       // dano (ou cura, se isHeal)
    public bool isHeal;
    public bool isCritical;
    public int bonusCards;   // ♦ compra cartas
    public HintDirection hint;
    public bool defeated;
    // Coringas e RAISE (preenchidos pelo BossController)
    public bool nearMiss;     // Quase Lá: errou por 1 e bateu como crítico
    public bool marked;       // Carta Marcada: a carta virou o número exato
    public int raise;         // 0 = normal · 1 = RAISE · 2 = CALL (aceitou o RE-RAISE)
    public bool raiseWon;     // RAISE perto do número (dano multiplicado)
    public int raiseBust;     // RAISE longe do número: dano que o PLAYER leva
    public bool firstLocked;  // Ás: acertou o 1º número (falta o 2º)
    public bool royalFlush;   // Ás: acertou os dois números em sequência
}

// Matemática da "sorte": distância entre carta e número secreto → dano.
public static class LuckyMath
{
    public const int CriticalDamage = 5;
    public const int ColdDistance = 5; // a partir daqui não dá dano

    public static int Distance(int cardValue, int secret) => Mathf.Abs(cardValue - secret);

    // 0 = crítico (5) · 1 = 3 · 2 = 2 · 3–4 = 1 · 5+ = 0
    public static int BaseDamage(int distance)
    {
        if (distance == 0) return CriticalDamage;
        if (distance == 1) return 3;
        if (distance == 2) return 2;
        if (distance <= 4) return 1;
        return 0;
    }

    // ♠ x1,5 (x2 com o coringa Espada Afiada)
    public static float SuitMultiplier(Suit suit) => suit == Suit.Spades ? Jokers.SpadeMultiplier : 1f;

    public static int FinalDamage(int baseDamage, Suit suit) =>
        Mathf.CeilToInt(baseDamage * SuitMultiplier(suit));

    public static HintDirection Hint(int cardValue, int secret)
    {
        if (cardValue == secret) return HintDirection.Exact;
        return secret > cardValue ? HintDirection.Higher : HintDirection.Lower;
    }

    // 1 = quente (perto), 0 = frio (longe)
    public static float Warmth(int distance) => 1f - Mathf.Clamp01(distance / (float)ColdDistance);

    public static Color WarmthColor(int distance) =>
        Color.Lerp(new Color32(0x5A, 0x9B, 0xFF, 0xFF), new Color32(0xFF, 0x6A, 0x2E, 0xFF), Warmth(distance));

    public static HitResult Evaluate(Card card, int secret)
    {
        int dist = Distance(card.value, secret);
        int baseDmg = BaseDamage(dist);
        // Coringa Quase Lá: errar por 1 bate como crítico (mas o número não muda)
        bool nearMiss = dist == 1 && Jokers.NearMissCrit;
        if (nearMiss) baseDmg = CriticalDamage;
        var r = new HitResult
        {
            card = card,
            distance = dist,
            isCritical = dist == 0,
            hint = Hint(card.value, secret),
            isHeal = card.suit == Suit.Hearts,
            nearMiss = nearMiss,
        };
        r.amount = r.isHeal ? baseDmg : FinalDamage(baseDmg, card.suit);
        if (r.isHeal && r.amount > 0) r.amount += Jokers.ExtraHeal; // Coração de Ouro
        if (card.suit == Suit.Diamonds && baseDmg > 0) r.bonusCards = (r.isCritical ? 2 : 1) + Jokers.ExtraDiamondCards;
        return r;
    }

    // ===================== RAISE / TRUCO =====================
    public const int RaiseNear = 2;       // até 2 do número: o RAISE paga
    public const int RaiseBustFrom = 6;   // 6 ou mais longe: o RAISE estoura (o player leva dano)
    public static int RaiseMultiplier(int level) => level >= 2 ? 3 : 2; // RAISE x2 · CALL x3

    // Aplica o RAISE num resultado já calculado.
    public static void ApplyRaise(ref HitResult r, int level)
    {
        r.raise = level;
        if (level <= 0) return;
        if (r.distance <= RaiseNear && r.amount > 0)
        {
            r.raiseWon = true;
            r.amount *= RaiseMultiplier(level);
        }
        else if (r.distance >= RaiseBustFrom) r.raiseBust = level; // RAISE: 1 de dano · CALL: 2
    }
}
