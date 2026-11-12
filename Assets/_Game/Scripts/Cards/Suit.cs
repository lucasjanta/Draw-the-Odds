using UnityEngine;

public enum Suit { Clubs, Spades, Hearts, Diamonds }

// Informações fixas de cada naipe (cor, símbolo, nome). "static class" = só funções, sem instância.
public static class SuitInfo
{
    public const int Count = 4;

    public static Color GetColor(Suit suit) => suit switch
    {
        Suit.Clubs => new Color32(0x2F, 0x8F, 0x4E, 0xFF),
        Suit.Spades => new Color32(0x3B, 0x4C, 0xC0, 0xFF),
        Suit.Hearts => new Color32(0xD1, 0x37, 0x3F, 0xFF),
        _ => new Color32(0xE0, 0x86, 0x1E, 0xFF),
    };

    public static string Symbol(Suit suit) => suit switch
    {
        Suit.Clubs => "♣",
        Suit.Spades => "♠",
        Suit.Hearts => "♥",
        _ => "♦",
    };

    // O que cada naipe faz ao acertar o chefe (mostrado no tutorial da tela inicial).
    public static string Effect(Suit suit) => suit switch
    {
        Suit.Clubs => "normal damage",
        Suit.Spades => "x1.5 damage",
        Suit.Hearts => "heals you instead",
        _ => "damage + draw a card",
    };

    // "Tinta" impressa nas cartas (mesma paleta da arte): preto para ♠♣, vermelho para ♥♦.
    // Usada nos números desenhados por cima das cartas; GetColor continua para efeitos e HUD.
    public static Color Ink(Suit suit) => suit == Suit.Clubs || suit == Suit.Spades
        ? new Color32(0x1E, 0x1E, 0x28, 0xFF)
        : new Color32(0xDE, 0x28, 0x00, 0xFF);

    public static Suit Random() => (Suit)UnityEngine.Random.Range(0, Count);

    // Texto com a cor do naipe (rich text do TextMeshPro).
    public static string Colored(string text, Suit suit) =>
        $"<color=#{ColorUtility.ToHtmlStringRGB(GetColor(suit))}>{text}</color>";
}
