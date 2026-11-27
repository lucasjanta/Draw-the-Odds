using System.Collections.Generic;
using UnityEngine;

// Coringas do JOGADOR (relíquias da run). Não confundir com o ataque "Jokers" dos chefes.
// A ordem do enum importa: cada um vira um bit no save (ProgressSave) e um número no ranking. Só adicione no FIM.
public enum JokerId
{
    SharpSword, GoldenHeart, LoadedDie, RabbitFoot, LuckyChip, BigHand, StoppedClock, WildCard, NearMiss, MarkedCard,
    // 27/11: bônus de naipe viraram coringas + evoluções
    Clover, GoldPurse, PiggyBank, AllIn, PocketAce,
    RoyalSword, LionHeart, GoldMine, FourLeaf, HawkEye,
}

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
    public JokerId? evolvesFrom; // evolução: só aparece com o coringa de origem no cinto e toma o lugar dele

    public string Name => Loc.Pick(names[0], names[1], names[2], names[3], names[4], names[5]);
    public string Desc => Loc.Pick(descs[0], descs[1], descs[2], descs[3], descs[4], descs[5]);
    public bool IsEvolution => evolvesFrom.HasValue;
}

public static class JokerCatalog
{
    private static JokerDef D(JokerId id, int cost, Color32 color, string symbol, string[] names, string[] descs, JokerId? from = null) =>
        new JokerDef { id = id, cost = cost, color = color, symbol = symbol, names = names, descs = descs, evolvesFrom = from };

    // Na mesma ordem do enum (Get usa o índice).
    public static readonly JokerDef[] All =
    {
        D(JokerId.SharpSword, 0, new Color32(0x6E, 0x8C, 0xFF, 0xFF), "♠",
            new[] { "SHARP SWORD", "ESPADA AFIADA", "ESPADA AFILADA", "ÉPÉE AFFÛTÉE", "SCHARFES SCHWERT", "SPADA AFFILATA" },
            new[] { "♠ cards deal x1.5 damage", "Cartas ♠ dão x1,5 de dano", "Las cartas ♠ hacen x1,5 de daño",
                    "Les cartes ♠ font x1,5 dégâts", "♠-Karten machen x1,5 Schaden", "Le carte ♠ fanno x1,5 danni" }),
        D(JokerId.GoldenHeart, 0, new Color32(0xE8, 0x4A, 0x5F, 0xFF), "♥",
            new[] { "GOLDEN HEART", "CORAÇÃO DE OURO", "CORAZÓN DE ORO", "CŒUR D'OR", "GOLDENES HERZ", "CUORE D'ORO" },
            new[] { "♥ hits also heal you 1", "Acertos ♥ também curam 1", "Los golpes ♥ también curan 1",
                    "Les coups ♥ soignent aussi 1", "♥-Treffer heilen auch 1", "I colpi ♥ curano anche 1" }),
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

        // ===================== 27/11 =====================
        D(JokerId.Clover, 0, new Color32(0x3C, 0xC8, 0x6E, 0xFF), "♣",
            new[] { "CLOVER", "TREVO", "TRÉBOL", "TRÈFLE", "KLEEBLATT", "TRIFOGLIO" },
            new[] { "♣ hits deal +1 damage", "Acertos ♣ dão +1 de dano", "Los golpes ♣ hacen +1 de daño",
                    "Les coups ♣ font +1 dégât", "♣-Treffer machen +1 Schaden", "I colpi ♣ fanno +1 danno" }),
        D(JokerId.GoldPurse, 0, new Color32(0xF0, 0x9A, 0x2E, 0xFF), "♦",
            new[] { "GOLD PURSE", "BOLSA DE OUROS", "BOLSA DE OROS", "BOURSE D'OR", "GOLDBEUTEL", "BORSA D'ORO" },
            new[] { "♦ hits draw 1 card (2 on critical)", "Acertos ♦ compram 1 carta (2 no crítico)", "Los golpes ♦ roban 1 carta (2 en crítico)",
                    "Les coups ♦ piochent 1 carte (2 en critique)", "♦-Treffer ziehen 1 Karte (2 bei kritisch)", "I colpi ♦ pescano 1 carta (2 col critico)" }),
        D(JokerId.PiggyBank, 45, new Color32(0xFF, 0x9E, 0xC4, 0xFF), "$",
            new[] { "PIGGY BANK", "COFRINHO", "HUCHA", "TIRELIRE", "SPARSCHWEIN", "SALVADANAIO" },
            new[] { "Earn 50% more chips this run", "Ganha 50% mais fichas na run", "Ganas 50% más fichas en la partida",
                    "Gagne 50% de jetons en plus", "50% mehr Chips in diesem Lauf", "Guadagni il 50% di fiche in più" }),
        D(JokerId.AllIn, 70, new Color32(0xFF, 0x4A, 0x3D, 0xFF), "!",
            new[] { "ALL IN", "TUDO OU NADA", "TODO O NADA", "TAPIS", "ALL IN", "ALL IN" },
            new[] { "+1 damage on every hit, -3 max HP", "+1 de dano em todo acerto, -3 de vida máx.", "+1 de daño en cada golpe, -3 de vida máx.",
                    "+1 dégât à chaque coup, -3 PV max", "+1 Schaden pro Treffer, -3 max. LP", "+1 danno a ogni colpo, -3 vita max" }),
        D(JokerId.PocketAce, 90, new Color32(0x4C, 0xFF, 0x9A, 0xFF), "A+",
            new[] { "POCKET ACE", "ÁS NO BOLSO", "AS EN EL BOLSILLO", "AS DE POCHE", "ASS IN DER TASCHE", "ASSO IN TASCA" },
            new[] { "Criticals deal +3 damage", "Críticos dão +3 de dano", "Los críticos hacen +3 de daño",
                    "Les critiques font +3 dégâts", "Kritische Treffer +3 Schaden", "I critici fanno +3 danni" }),

        // Evoluções (aparecem na escolha só com o coringa de origem no cinto)
        D(JokerId.RoyalSword, 75, new Color32(0x9C, 0xB4, 0xFF, 0xFF), "♠♠",
            new[] { "ROYAL SWORD", "ESPADA REAL", "ESPADA REAL", "ÉPÉE ROYALE", "KÖNIGSSCHWERT", "SPADA REALE" },
            new[] { "EVOLUTION: ♠ cards deal x2", "EVOLUÇÃO: cartas ♠ dão x2", "EVOLUCIÓN: las cartas ♠ hacen x2",
                    "ÉVOLUTION : les cartes ♠ font x2", "EVOLUTION: ♠-Karten machen x2", "EVOLUZIONE: le carte ♠ fanno x2" }, JokerId.SharpSword),
        D(JokerId.LionHeart, 75, new Color32(0xFF, 0x6E, 0x80, 0xFF), "♥♥",
            new[] { "LION HEART", "CORAÇÃO DE LEÃO", "CORAZÓN DE LEÓN", "CŒUR DE LION", "LÖWENHERZ", "CUOR DI LEONE" },
            new[] { "EVOLUTION: ♥ hits heal 2", "EVOLUÇÃO: acertos ♥ curam 2", "EVOLUCIÓN: los golpes ♥ curan 2",
                    "ÉVOLUTION : les coups ♥ soignent 2", "EVOLUTION: ♥-Treffer heilen 2", "EVOLUZIONE: i colpi ♥ curano 2" }, JokerId.GoldenHeart),
        D(JokerId.GoldMine, 75, new Color32(0xFF, 0xC2, 0x4A, 0xFF), "♦♦",
            new[] { "GOLD MINE", "MINA DE OURO", "MINA DE ORO", "MINE D'OR", "GOLDMINE", "MINIERA D'ORO" },
            new[] { "EVOLUTION: ♦ draws 2 cards and gives 1 chip", "EVOLUÇÃO: ♦ compra 2 cartas e dá 1 ficha", "EVOLUCIÓN: ♦ roba 2 cartas y da 1 ficha",
                    "ÉVOLUTION : ♦ pioche 2 cartes et donne 1 jeton", "EVOLUTION: ♦ zieht 2 Karten, gibt 1 Chip", "EVOLUZIONE: ♦ pesca 2 carte e dà 1 fiche" }, JokerId.GoldPurse),
        D(JokerId.FourLeaf, 75, new Color32(0x7C, 0xFF, 0x8E, 0xFF), "♣♣",
            new[] { "FOUR-LEAF CLOVER", "TREVO DE 4 FOLHAS", "TRÉBOL DE 4 HOJAS", "TRÈFLE À 4 FEUILLES", "VIERBLÄTTRIGES KLEE", "QUADRIFOGLIO" },
            new[] { "EVOLUTION: ♣ hits deal +2 damage", "EVOLUÇÃO: acertos ♣ dão +2 de dano", "EVOLUCIÓN: los golpes ♣ hacen +2 de daño",
                    "ÉVOLUTION : les coups ♣ font +2 dégâts", "EVOLUTION: ♣-Treffer +2 Schaden", "EVOLUZIONE: i colpi ♣ fanno +2 danni" }, JokerId.Clover),
        D(JokerId.HawkEye, 90, new Color32(0xFF, 0xD0, 0x80, 0xFF), "??",
            new[] { "HAWK EYE", "OLHO DE LINCE", "OJO DE LINCE", "ŒIL DE LYNX", "ADLERAUGE", "OCCHIO DI LINCE" },
            new[] { "EVOLUTION: EVEN/ODD and LOW/HIGH half", "EVOLUÇÃO: PAR/ÍMPAR e metade BAIXA/ALTA", "EVOLUCIÓN: PAR/IMPAR y mitad BAJA/ALTA",
                    "ÉVOLUTION : PAIR/IMPAIR et moitié BASSE/HAUTE", "EVOLUTION: GERADE/UNGERADE und untere/obere Hälfte", "EVOLUZIONE: PARI/DISPARI e metà BASSA/ALTA" }, JokerId.LoadedDie),
    };

    public static JokerDef Get(JokerId id) => All[(int)id];

    // Evolução de um coringa (null = não tem).
    public static JokerId? EvolutionOf(JokerId baseId)
    {
        foreach (var d in All) if (d.evolvesFrom == baseId) return d.id;
        return null;
    }

    // Sorteia até "count" coringas diferentes, liberados (taverna) e que o jogador ainda não tem na run.
    // Evoluções só entram com o coringa de origem no cinto (e valem em dobro no sorteio).
    // Com o cinto cheio, só evoluções (elas não ocupam espaço novo).
    public static List<JokerId> RollChoices(int count)
    {
        var pool = new List<JokerId>();
        foreach (var d in All)
        {
            if (!ProgressSave.JokerOwned(d.id) || Jokers.Has(d.id)) continue;
            if (d.IsEvolution)
            {
                if (!Jokers.Has(d.evolvesFrom.Value)) continue;
                pool.Add(d.id);
                pool.Add(d.id); // peso 2
            }
            else
            {
                if (Jokers.BeltFull) continue;
                var evo = EvolutionOf(d.id);
                if (evo.HasValue && Jokers.Has(evo.Value)) continue; // já tem a versão evoluída
                pool.Add(d.id);
            }
        }
        var result = new List<JokerId>();
        while (result.Count < count && pool.Count > 0)
        {
            var pick = pool[Random.Range(0, pool.Count)];
            result.Add(pick);
            pool.RemoveAll(j => j == pick);
        }
        return result;
    }
}
