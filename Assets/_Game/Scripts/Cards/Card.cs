using System;

// struct = tipo "por valor" (copiado ao passar adiante), ótimo para dados pequenos como uma carta.
[Serializable]
public struct Card
{
    public const int MinBaseValue = 1;
    public const int MaxBaseValue = 13;   // A=1 ... K=13
    public const int MaxValue = 26;       // combos podem chegar até 26

    public Suit suit;
    public int value;

    public Card(Suit suit, int value)
    {
        this.suit = suit;
        this.value = value;
    }

    public static Card Random() =>
        new Card(SuitInfo.Random(), UnityEngine.Random.Range(MinBaseValue, MaxBaseValue + 1));

    public static Card RandomOfSuit(Suit suit) =>
        new Card(suit, UnityEngine.Random.Range(MinBaseValue, MaxBaseValue + 1));

    public string Label => value.ToString();

    public override string ToString() => value + SuitInfo.Symbol(suit);
    public string ColoredLabel => SuitInfo.Colored(ToString(), suit);
}
