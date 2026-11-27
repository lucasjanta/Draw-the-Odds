using System.Collections.Generic;

// Conquistas (taverna → aba CONQUISTAS). Cada uma libera um item do guarda-roupa (chapéu, roupa ou verso de carta).
// A ordem importa: cada conquista vira um bit no save (ProgressSave). Só adicione no FIM.
public static class Achievements
{
    public const int Crowned = 0;      // vencer o Rei → Coroa
    public const int AceSleeve = 1;    // vencer o Ás → roupa Real
    public const int HighRoller = 2;   // vencer o Rei no HARDCORE → roupa Sombra
    public const int EndlessNight = 3; // chegar à rodada 10 do infinito → chapéu de Bobo
    public const int Flawless = 4;     // vencer um chefe sem levar dano → verso Obsidiana
    public const int CardShark = 5;    // 5 críticos numa luta → verso Tubarão
    public const int Count = 6;

    public static bool Has(int a) => ProgressSave.AchievementDone(a);

    // Chefe vencido: devolve as conquistas NOVAS (para o aviso na tela).
    public static List<int> OnBossBeaten(GameManager gm)
    {
        var news = new List<int>();
        void Try(int a, bool ok)
        {
            if (!ok || Has(a)) return;
            ProgressSave.MarkAchievement(a);
            news.Add(a);
        }
        if (gm.IsTutorial) return news;
        var data = gm.CurrentBossData;
        var t = Rumors.Track;
        bool story = !GameManager.EndlessMode;
        bool easy = GameManager.SelectedDifficulty == Difficulty.Easy;
        Try(Crowned, story && data.StoryId == "boss2");
        Try(AceSleeve, story && gm.IsSecretBoss);
        Try(HighRoller, story && data.StoryId == "boss2" && GameManager.SelectedDifficulty == Difficulty.Hardcore);
        Try(EndlessNight, !story && gm.EndlessRound + 1 >= 10);
        Try(Flawless, !easy && t.damageTaken == 0);
        Try(CardShark, t.crits >= 5);
        return news;
    }

    public static string Name(int a) => a switch
    {
        Crowned => Loc.Pick("CROWNED", "COROADO", "CORONADO", "COURONNÉ", "GEKRÖNT", "INCORONATO"),
        AceSleeve => Loc.Pick("ACE UP YOUR SLEEVE", "ÁS NA SUA MANGA", "AS EN TU MANGA", "AS DANS TA MANCHE", "ASS IM ÄRMEL", "ASSO NELLA MANICA"),
        HighRoller => Loc.Pick("HIGH ROLLER", "APOSTA ALTA", "APOSTADOR", "GROS JOUEUR", "HIGHROLLER", "GIOCATORE D'AZZARDO"),
        EndlessNight => Loc.Pick("ENDLESS NIGHT", "NOITE SEM FIM", "NOCHE SIN FIN", "NUIT SANS FIN", "ENDLOSE NACHT", "NOTTE SENZA FINE"),
        Flawless => Loc.Pick("FLAWLESS", "IMPECÁVEL", "IMPECABLE", "SANS FAUTE", "MAKELLOS", "IMPECCABILE"),
        _ => Loc.Pick("CARD SHARK", "TUBARÃO DAS CARTAS", "TAHÚR", "REQUIN DES CARTES", "KARTENHAI", "SQUALO DELLE CARTE"),
    };

    public static string Desc(int a) => a switch
    {
        Crowned => Loc.Pick("Beat the King", "Vença o Rei", "Vence al Rey", "Bats le Roi", "Besiege den König", "Batti il Re"),
        AceSleeve => Loc.Pick("Beat the secret Ace", "Vença o Ás secreto", "Vence al As secreto", "Bats l'As secret", "Besiege das geheime Ass", "Batti l'Asso segreto"),
        HighRoller => Loc.Pick("Beat the King on HARDCORE", "Vença o Rei no HARDCORE", "Vence al Rey en HARDCORE", "Bats le Roi en HARDCORE", "Besiege den König auf HARDCORE", "Batti il Re in HARDCORE"),
        EndlessNight => Loc.Pick("Reach round 10 in Endless", "Chegue à rodada 10 do Infinito", "Llega a la ronda 10 del Infinito", "Atteins la manche 10 en Infini", "Erreiche Runde 10 im Endlosmodus", "Arriva al round 10 dell'Infinito"),
        Flawless => Loc.Pick("Beat a boss without taking damage (not EASY)", "Vença um chefe sem levar dano (fora do FÁCIL)", "Vence a un jefe sin recibir daño (no FÁCIL)",
                             "Bats un boss sans dégâts (hors FACILE)", "Besiege einen Boss ohne Schaden (nicht LEICHT)", "Batti un boss senza danni (non FACILE)"),
        _ => Loc.Pick("5 criticals in one fight", "5 críticos numa luta", "5 críticos en una pelea", "5 critiques dans un combat", "5 kritische Treffer in einem Kampf", "5 critici in uno scontro"),
    };

    // "Libera: COROA (chapéu)"
    public static string RewardText(int a)
    {
        string unlocks = Loc.Pick("Unlocks", "Libera", "Desbloquea", "Débloque", "Schaltet frei", "Sblocca");
        foreach (var h in Wardrobe.Hats) if (h.achievement == a) return $"{unlocks}: {h.Name} ({Loc.Pick("hat", "chapéu", "sombrero", "chapeau", "Hut", "cappello")})";
        foreach (var o in Wardrobe.Outfits) if (o.achievement == a) return $"{unlocks}: {o.Name} ({Loc.Pick("outfit", "roupa", "ropa", "tenue", "Outfit", "abito")})";
        for (int i = 0; i < CardBacks.All.Length; i++)
            if (CardBacks.All[i].achievement == a) return $"{unlocks}: {Loc.T(CardBacks.All[i].name)} ({Loc.Pick("card back", "verso", "reverso", "dos", "Kartenrücken", "dorso")})";
        return "";
    }

    // Debug (ponte): call Achievements.DebugUnlock 3 · DebugUnlock all
    public static string DebugUnlock(string arg)
    {
        if (arg == "all") for (int i = 0; i < Count; i++) ProgressSave.MarkAchievement(i);
        else ProgressSave.MarkAchievement(int.Parse(arg));
        return "achievements ok";
    }
}
