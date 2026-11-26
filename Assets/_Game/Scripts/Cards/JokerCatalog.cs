using System.Collections.Generic;
using UnityEngine;

// Coringas do JOGADOR (relíquias da run). Não confundir com o ataque "Jokers" dos chefes.
// A ordem do enum importa: cada um vira um bit no save (ProgressSave). Só adicione no FIM.
public enum JokerId { SharpSword, GoldenHeart, LoadedDie, RabbitFoot, LuckyChip, BigHand, StoppedClock, WildCard, NearMiss, MarkedCard }

// Ficha de um coringa: nome, descrição curta (cabe na tela do celular), cor, símbolo e preço na taverna.
// (Símbolos só com caracteres que a fonte tem: ★ e ícones emoji NÃO existem nela.)
public class JokerDef
{
    public JokerId id;
    public int cost;          // 0 = já vem liberado
    public Color color;
    public string symbol;     // desenhado no ícone (texto TMP, por isso só caracteres que a fonte tem)
    public string[] names;    // 6 idiomas (ordem do Loc.Pick)
    public string[] descs;

    public string Name => Loc.Pick(names[0], names[1], names[2], names[3], names[4], names[5]);
    public string Desc => Loc.Pick(descs[0], descs[1], descs[2], descs[3], descs[4], descs[5]);
}

public static class JokerCatalog
{
    private static JokerDef D(JokerId id, int cost, Color32 color, string symbol, string[] names, string[] descs) =>
        new JokerDef { id = id, cost = cost, color = color, symbol = symbol, names = names, descs = descs };

    public static readonly JokerDef[] All =
    {
        D(JokerId.SharpSword, 0, new Color32(0x6E, 0x8C, 0xFF, 0xFF), "♠",
            new[] { "SHARP SWORD", "ESPADA AFIADA", "ESPADA AFILADA", "ÉPÉE AFFÛTÉE", "SCHARFES SCHWERT", "SPADA AFFILATA" },
            new[] { "♠ cards deal x2 (not x1.5)", "Cartas ♠ dão x2 (não x1,5)", "Las cartas ♠ hacen x2 (no x1,5)",
                    "Les cartes ♠ font x2 (pas x1,5)", "♠-Karten machen x2 (statt x1,5)", "Le carte ♠ fanno x2 (non x1,5)" }),
        D(JokerId.GoldenHeart, 0, new Color32(0xE8, 0x4A, 0x5F, 0xFF), "♥",
            new[] { "GOLDEN HEART", "CORAÇÃO DE OURO", "CORAZÓN DE ORO", "CŒUR D'OR", "GOLDENES HERZ", "CUORE D'ORO" },
            new[] { "♥ heals +1 · ♦ draws +1 card", "♥ cura +1 · ♦ compra +1 carta", "♥ cura +1 · ♦ roba +1 carta",
                    "♥ soigne +1 · ♦ pioche +1 carte", "♥ heilt +1 · ♦ zieht +1 Karte", "♥ cura +1 · ♦ pesca +1 carta" }),
        D(JokerId.LoadedDie, 0, new Color32(0xFF, 0xB0, 0x40, 0xFF), "?",
            new[] { "LOADED DIE", "DADO VICIADO", "DADO TRUCADO", "DÉ PIPÉ", "GEZINKTER WÜRFEL", "DADO TRUCCATO" },
            new[] { "Shows if the number is EVEN or ODD", "Mostra se o número é PAR ou ÍMPAR", "Muestra si el número es PAR o IMPAR",
                    "Montre si le nombre est PAIR ou IMPAIR", "Zeigt, ob die Zahl GERADE oder UNGERADE ist", "Mostra se il numero è PARI o DISPARI" }),
        D(JokerId.RabbitFoot, 0, new Color32(0x73, 0xD8, 0xFF, 0xFF), "»",
            new[] { "RABBIT'S FOOT", "PÉ DE COELHO", "PATA DE CONEJO", "PATTE DE LAPIN", "HASENPFOTE", "ZAMPA DI CONIGLIO" },
            new[] { "Dash recharges 40% faster", "Dash recarrega 40% mais rápido", "El dash recarga 40% más rápido",
                    "Le dash recharge 40% plus vite", "Dash lädt 40% schneller", "Lo scatto si ricarica il 40% più in fretta" }),
        D(JokerId.LuckyChip, 0, new Color32(0x3C, 0xC8, 0x6E, 0xFF), "●",
            new[] { "LUCKY CHIP", "FICHA DA SORTE", "FICHA DE LA SUERTE", "JETON PORTE-BONHEUR", "GLÜCKSCHIP", "FICHE FORTUNATA" },
            new[] { "+1 redraw now and +1 per boss", "+1 nova mão agora e +1 por chefe", "+1 mano nueva ahora y +1 por jefe",
                    "+1 nouvelle main maintenant et +1 par boss", "+1 neue Hand jetzt und +1 pro Boss", "+1 nuova mano ora e +1 per boss" }),
        D(JokerId.BigHand, 40, new Color32(0xB5, 0x7C, 0xFF, 0xFF), "+2",
            new[] { "BIG HAND", "MÃO GRANDE", "MANO GRANDE", "GRANDE MAIN", "GROSSE HAND", "MANO GRANDE" },
            new[] { "Max hand +2, starting hand +1", "Mão máxima +2, mão inicial +1", "Mano máxima +2, mano inicial +1",
                    "Main max +2, main de départ +1", "Max. Hand +2, Starthand +1", "Mano massima +2, mano iniziale +1" }),
        D(JokerId.StoppedClock, 50, new Color32(0xC9, 0xC9, 0xD9, 0xFF), "II",
            new[] { "STOPPED CLOCK", "RELÓGIO PARADO", "RELOJ PARADO", "HORLOGE ARRÊTÉE", "STEHENDE UHR", "OROLOGIO FERMO" },
            new[] { "Merge: +5 s and shows the result", "Fusão: +5 s e mostra o resultado", "Fusión: +5 s y muestra el resultado",
                    "Fusion : +5 s et montre le résultat", "Fusion: +5 s und zeigt das Ergebnis", "Fusione: +5 s e mostra il risultato" }),
        D(JokerId.WildCard, 60, new Color32(0xFF, 0x6E, 0xC7, 0xFF), "J",
            new[] { "WILD CARD", "CARTA CURINGA", "CARTA COMODÍN", "CARTE JOKER", "WILDE KARTE", "CARTA JOLLY" },
            new[] { "Merges never fail", "Fusão nunca falha", "La fusión nunca falla",
                    "La fusion n'échoue jamais", "Fusion klappt immer", "La fusione non fallisce mai" }),
        D(JokerId.NearMiss, 80, new Color32(0xFF, 0x8A, 0x3D, 0xFF), "±",
            new[] { "NEAR MISS", "QUASE LÁ", "CASI", "PRESQUE", "KNAPP DANEBEN", "QUASI" },
            new[] { "Off by 1 hits like a critical", "Errar por 1 bate como crítico", "Fallar por 1 golpea como crítico",
                    "Rater de 1 frappe comme un critique", "Um 1 daneben trifft wie kritisch", "Sbagliare di 1 colpisce come un critico" }),
        D(JokerId.MarkedCard, 100, new Color32(0xFF, 0xD4, 0x47, 0xFF), "A",
            new[] { "MARKED CARD", "CARTA MARCADA", "CARTA MARCADA", "CARTE MARQUÉE", "GEZINKTE KARTE", "CARTA SEGNATA" },
            new[] { "1st card on each boss is EXACT", "1ª carta em cada chefe é EXATA", "1.ª carta contra cada jefe es EXACTA",
                    "1re carte contre chaque boss est EXACTE", "1. Karte pro Boss ist EXAKT", "1ª carta su ogni boss è ESATTA" }),
    };

    public static JokerDef Get(JokerId id) => All[(int)id];

    // Sorteia até "count" coringas diferentes, liberados (taverna) e que o jogador ainda não tem na run.
    public static List<JokerId> RollChoices(int count)
    {
        var pool = new List<JokerId>();
        foreach (var d in All)
            if (ProgressSave.JokerOwned(d.id) && !Jokers.Has(d.id)) pool.Add(d.id);
        var result = new List<JokerId>();
        while (result.Count < count && pool.Count > 0)
        {
            int i = Random.Range(0, pool.Count);
            result.Add(pool[i]);
            pool.RemoveAt(i);
        }
        return result;
    }
}
