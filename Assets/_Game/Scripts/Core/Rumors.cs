using System.Collections.Generic;

// Mural de Boatos (taverna): 3 boatos por chefe da história (Valete, Dama, Rei).
// Cada boato é um DESAFIO; cumprido, ele revela um "podre" do nobre e dá fichas.
// Os 3 boatos do Rei juntos revelam o Ás secreto (ProgressSave.AceUnlocked).
// A luta atual é acompanhada por Track (o GameManager avisa os eventos).
public static class Rumors
{
    public const int PerBoss = 3;
    public const int Bosses = 3;
    public const int ChipReward = 25;

    public static int Index(int boss, int slot) => boss * PerBoss + slot;

    // ===================== Acompanhamento da luta atual =====================
    public class FightTrack
    {
        public int hits, crits, damageTaken;
        public bool firstHitCritical;
        public bool raiseWon;
    }
    public static FightTrack Track { get; private set; } = new FightTrack();
    public static void BeginFight() => Track = new FightTrack();

    // Chefe derrotado: quais boatos dele foram cumpridos nesta luta (só os novos).
    public static List<int> Evaluate(int boss)
    {
        var found = new List<int>();
        if (boss < 0 || boss >= Bosses) return found;
        var t = Track;
        bool[] done =
        {
            // 0 = vencer · 1 e 2 dependem do chefe
            true,
            boss == 0 ? t.firstHitCritical : boss == 1 ? t.raiseWon : t.crits >= 3,
            boss == 2 ? t.raiseWon : t.damageTaken == 0,
        };
        for (int slot = 0; slot < PerBoss; slot++)
        {
            int i = Index(boss, slot);
            if (done[slot] && !ProgressSave.RumorFound(i))
            {
                ProgressSave.FindRumor(i);
                ProgressSave.AddChips(ChipReward);
                found.Add(i);
            }
        }
        return found;
    }

    // ===================== Textos =====================
    private static string BossName(int boss) => Loc.T(boss == 0 ? "JACK" : boss == 1 ? "QUEEN" : "KING");

    // Nome com artigo, para "vença X" e "contra X" (o artigo muda com o gênero em PT/ES/FR/IT/DE).
    private static string The(int boss) => Loc.Pick(
        "the " + BossName(boss),
        (boss == 1 ? "a " : "o ") + BossName(boss),
        (boss == 2 ? "el " : "la ") + BossName(boss),
        (boss == 1 ? "la " : "le ") + BossName(boss),
        (boss == 1 ? "die " : "den ") + BossName(boss),
        (boss == 1 ? "la " : "il ") + BossName(boss));

    // "Vence a la J" / "al REY" (espanhol junta a + el)
    private static string EsBeat(int boss) => boss == 2 ? "al " + BossName(boss) : "a la " + BossName(boss);

    // O desafio (o que fazer para descobrir o boato).
    public static string Challenge(int index)
    {
        int boss = index / PerBoss, slot = index % PerBoss;
        string b = The(boss), es = EsBeat(boss);
        if (slot == 0) return Loc.Pick($"Beat {b}", $"Vença {b}", $"Vence {es}", $"Bats {b}", $"Besiege {b}", $"Batti {b}");
        if (boss == 0 && slot == 1)
            return Loc.Pick("Critical with your first hit", "Crítico no primeiro acerto", "Crítico en tu primer golpe",
                            "Critique au premier coup", "Kritisch mit dem ersten Treffer", "Critico al primo colpo");
        if ((boss == 1 && slot == 1) || (boss == 2 && slot == 2))
            return Loc.Pick($"Win a RAISE against {b}", $"Ganhe um TRUCO contra {b}", $"Gana un TRUCO contra {b}",
                            $"Gagne une RELANCE contre {b}", $"Gewinne ein RAISE gegen {b}", $"Vinci un RILANCIO contro {b}");
        if (boss == 2 && slot == 1)
            return Loc.Pick($"3 criticals in one fight with {b}", $"3 críticos numa luta contra {b}", $"3 críticos en una pelea con {b}",
                            $"3 critiques dans un combat contre {b}", $"3 kritische Treffer gegen {b}", $"3 critici in uno scontro con {b}");
        return Loc.Pick($"Beat {b} without taking damage", $"Vença {b} sem levar dano", $"Vence {es} sin recibir daño",
                        $"Bats {b} sans prendre de dégâts", $"Besiege {b} ohne Schaden", $"Batti {b} senza subire danni");
    }

    // O boato em si (lore), revelado depois de cumprir o desafio.
    public static string Lore(int index)
    {
        switch (index)
        {
            case 0: return Loc.Pick("The Jack's dice are loaded... toward the Crown.", "Os dados do Valete são viciados... a favor da Coroa.",
                "Los dados de la J están trucados... a favor de la Corona.", "Les dés du Valet sont pipés... en faveur de la Couronne.",
                "Die Würfel des Buben sind gezinkt... zugunsten der Krone.", "I dadi del Fante sono truccati... a favore della Corona.");
            case 1: return Loc.Pick("He never lies. He just never says who he works for.", "Ele nunca mente. Só nunca diz pra quem trabalha.",
                "Nunca miente. Solo nunca dice para quién trabaja.", "Il ne ment jamais. Il ne dit juste pas pour qui il travaille.",
                "Er lügt nie. Er sagt nur nie, für wen er arbeitet.", "Non mente mai. Solo non dice mai per chi lavora.");
            case 2: return Loc.Pick("He collects taxes from the jester too. Nobody escapes.", "Ele cobra imposto até do bobo. Ninguém escapa.",
                "Le cobra impuestos hasta al bufón. Nadie se libra.", "Il taxe même le bouffon. Personne n'y échappe.",
                "Er kassiert sogar beim Narren. Niemand entkommt.", "Tassa perfino il giullare. Nessuno sfugge.");
            case 3: return Loc.Pick("The Queen was a villager once. She forgot on purpose.", "A Dama já foi aldeã. Esqueceu de propósito.",
                "La Reina fue aldeana. Lo olvidó a propósito.", "La Dame était villageoise. Elle l'a oublié exprès.",
                "Die Dame war einst Dorfbewohnerin. Sie vergaß es mit Absicht.", "La Regina era una popolana. L'ha dimenticato apposta.");
            case 4: return Loc.Pick("She rewrites her number when nobody's looking.", "Ela reescreve o número quando ninguém está olhando.",
                "Reescribe su número cuando nadie mira.", "Elle réécrit son nombre quand personne ne regarde.",
                "Sie schreibt ihre Zahl um, wenn keiner hinsieht.", "Riscrive il suo numero quando nessuno guarda.");
            case 5: return Loc.Pick("Her favorite game is truco. She always calls.", "O jogo favorito dela é truco. Ela sempre pede seis.",
                "Su juego favorito es el truco. Siempre canta retruco.", "Son jeu préféré, c'est la relance. Elle suit toujours.",
                "Ihr Lieblingsspiel ist Poker. Sie geht immer mit.", "Il suo gioco preferito è il truco. Rilancia sempre.");
            case 6: return Loc.Pick("The King never learned to play. Someone plays FOR him.", "O Rei nunca aprendeu a jogar. Alguém joga POR ele.",
                "El Rey nunca aprendió a jugar. Alguien juega POR él.", "Le Roi n'a jamais su jouer. Quelqu'un joue POUR lui.",
                "Der König lernte nie zu spielen. Jemand spielt FÜR ihn.", "Il Re non ha mai imparato a giocare. Qualcuno gioca PER lui.");
            case 7: return Loc.Pick("His 'official truth' changes every 25 seconds.", "A 'verdade oficial' dele muda a cada 25 segundos.",
                "Su 'verdad oficial' cambia cada 25 segundos.", "Sa 'vérité officielle' change toutes les 25 secondes.",
                "Seine 'offizielle Wahrheit' ändert sich alle 25 Sekunden.", "La sua 'verità ufficiale' cambia ogni 25 secondi.");
            default: return Loc.Pick("There is a card higher than the King... up a sleeve.", "Existe uma carta mais alta que o Rei... numa manga.",
                "Hay una carta más alta que el Rey... en una manga.", "Il existe une carte plus haute que le Roi... dans une manche.",
                "Es gibt eine Karte höher als der König... in einem Ärmel.", "C'è una carta più alta del Re... in una manica.");
        }
    }
}
