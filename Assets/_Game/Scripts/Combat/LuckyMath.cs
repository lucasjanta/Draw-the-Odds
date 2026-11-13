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

    public static float SuitMultiplier(Suit suit) => suit == Suit.Spades ? 1.5f : 1f;

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
        var r = new HitResult
        {
            card = card,
            distance = dist,
            isCritical = dist == 0,
            hint = Hint(card.value, secret),
            isHeal = card.suit == Suit.Hearts,
        };
        r.amount = r.isHeal ? baseDmg : FinalDamage(baseDmg, card.suit);
        if (card.suit == Suit.Diamonds && baseDmg > 0) r.bonusCards = r.isCritical ? 2 : 1;
        return r;
    }
}
