using UnityEngine;

public enum PowerType { Add, Subtract, Multiply }

// Regras dos poderes de fusão (puras, sem UI).
// Não existe preview: o jogador só descobre o resultado na animação do merge.
public static class FusionRules
{
    public static string Symbol(PowerType type) => type switch
    {
        PowerType.Add => "+",
        PowerType.Subtract => "−",
        _ => "×",
    };

    public static string Title(PowerType type) => type switch
    {
        PowerType.Add => "ADD",
        PowerType.Subtract => "SUBTRACT",
        _ => "MULTIPLY",
    };

    public static Color GetColor(PowerType type) => type switch
    {
        PowerType.Add => new Color32(0x3C, 0xC8, 0x6E, 0xFF),
        PowerType.Subtract => new Color32(0xE8, 0x4A, 0x5F, 0xFF),
        _ => new Color32(0xFF, 0xD4, 0x47, 0xFF),
    };

    public static int Compute(PowerType type, int a, int b) => type switch
    {
        PowerType.Add => a + b,
        PowerType.Subtract => Mathf.Abs(a - b),
        _ => a * b,
    };

    // Valida a fusão. Retorna false com a mensagem de erro (mostrada ao confirmar).
    public static bool Validate(PowerType type, Card a, Card b, bool allowMixedSuits, out string error)
    {
        error = null;
        if (a.suit != b.suit && !allowMixedSuits) { error = "SAME SUIT ONLY"; return false; }
        int value = Compute(type, a.value, b.value);
        if (value <= 0) { error = "THAT MAKES ZERO!"; return false; }
        if (value > Card.MaxValue) { error = $"OVER {Card.MaxValue}!"; return false; }
        return true;
    }

    // Cria a carta resultante. Naipes diferentes → naipe sorteado entre os dois.
    public static Card Resolve(PowerType type, Card a, Card b)
    {
        Suit suit = a.suit == b.suit ? a.suit : (Random.value < 0.5f ? a.suit : b.suit);
        return new Card(suit, Compute(type, a.value, b.value));
    }
}
