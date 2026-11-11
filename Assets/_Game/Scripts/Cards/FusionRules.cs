using System.Collections.Generic;
using UnityEngine;

public enum PowerType { Add, Subtract, SwapSuit }

// Regras dos poderes de fusão (puras, sem UI). A UI usa Preview() e o FusionController usa Apply().
public static class FusionRules
{
    public static string Symbol(PowerType type) => type switch
    {
        PowerType.Add => "+",
        PowerType.Subtract => "−",
        _ => "↔",
    };

    public static string Title(PowerType type) => type switch
    {
        PowerType.Add => "ADD",
        PowerType.Subtract => "SUBTRACT",
        _ => "SWAP SUITS",
    };

    public static Color GetColor(PowerType type) => type switch
    {
        PowerType.Add => new Color32(0x3C, 0xC8, 0x6E, 0xFF),
        PowerType.Subtract => new Color32(0xE8, 0x4A, 0x5F, 0xFF),
        _ => new Color32(0xA8, 0x5C, 0xF0, 0xFF),
    };

    // Resultado de uma fusão: 1 carta (soma/subtração) ou 2 (troca de naipe).
    // suitIsRandom = naipes diferentes na soma/subtração → naipe sorteado entre os dois.
    public struct Outcome
    {
        public bool valid;
        public string error;
        public int value;
        public Suit suitA, suitB;
        public bool suitIsRandom;
        public bool isSwap;
        public Card swapA, swapB;
    }

    public static Outcome Preview(PowerType type, Card a, Card b, bool allowMixedSuits)
    {
        var o = new Outcome { suitA = a.suit, suitB = b.suit };

        if (type == PowerType.SwapSuit)
        {
            o.isSwap = true;
            if (a.suit == b.suit) { o.error = "SAME SUIT"; return o; }
            o.swapA = new Card(b.suit, a.value);
            o.swapB = new Card(a.suit, b.value);
            o.valid = true;
            return o;
        }

        if (a.suit != b.suit && !allowMixedSuits) { o.error = "SAME SUIT ONLY"; return o; }

        o.suitIsRandom = a.suit != b.suit;
        o.value = type == PowerType.Add ? a.value + b.value : Mathf.Abs(a.value - b.value);

        if (o.value <= 0) { o.error = "ZERO"; return o; }
        if (o.value > Card.MaxValue) { o.error = "MAX " + Card.MaxValue; return o; }

        o.valid = true;
        return o;
    }

    // Transforma o Outcome em cartas de verdade (sorteia o naipe se precisar).
    public static List<Card> Resolve(Outcome o)
    {
        if (o.isSwap) return new List<Card> { o.swapA, o.swapB };
        Suit suit = o.suitIsRandom ? (Random.value < 0.5f ? o.suitA : o.suitB) : o.suitA;
        return new List<Card> { new Card(suit, o.value) };
    }
}
