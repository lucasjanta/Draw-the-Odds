using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Menu "Draw the Odds/Setup Boss Roster" (27/11): monta a sequência nova de chefes no GameConfig.
//  1. Valete de Paus (naipe fixo ♣)
//  2. Valetes Gêmeos (♥ e ♦ juntos, cada um com o seu número)
//  3. Valete de Espadas, o irmão mais velho (♠, 1–16, rápido)
//  4. Dama (4 fases: a cada vez que a vida zera troca de naipe; cada naipe muda uma regra)
//  5. Rei (o naipe gira junto com o dado e muda o ataque)
//  + Ás secreto (depois do Rei, como antes)
// Usa os chefes que já existiam (sprites, músicas, ataques extras) como base: pode rodar de novo
// sem perder nada (os gêmeos e o Valete de Espadas são sempre recriados a partir do Valete).
// Também deixa os 4 naipes com a mesma chance (todos dão o mesmo dano agora).
public static class BossRosterSetup
{
    private const string ConfigPath = "Assets/_Game/Data/GameConfig.asset";

    private static BossAttack A(BossAttackType t, int count, float speed) => new BossAttack(t, count, speed);
    private const BossAttackType Volley = BossAttackType.Volley, Rain = BossAttackType.Rain, Dash = BossAttackType.Dash,
        Jokers = BossAttackType.Jokers, Roulette = BossAttackType.Roulette, Marked = BossAttackType.MarkedCards;

    [MenuItem("Draw the Odds/Setup Boss Roster")]
    public static void Setup()
    {
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        if (config == null) { Debug.LogError("[Boss Roster] GameConfig não encontrado"); return; }
        var list = config.EditorBosses;

        BossData Find(string name)
        {
            foreach (var b in list) if (b.DisplayName == name) return b;
            return null;
        }
        var jack = Find("JACK");
        var queen = Find("QUEEN");
        var king = Find("KING");
        if (jack == null || queen == null || king == null) { Debug.LogError("[Boss Roster] faltam JACK/QUEEN/KING no GameConfig"); return; }

        // 1. Valete de Paus: o mesmo de antes, mas sempre ♣
        jack.Identity("JACK", "JACK", "boss0", 0, 0, 15, false).SuitRule(BossSuitMode.Fixed, Suit.Clubs);

        // 2. Gêmeos: dois Valetes (♥ e ♦), menos vida cada, ataques mais espaçados
        var twins = jack.Clone()
            .Identity("JACK", "TWIN JACKS", "twins", -1, 0, 20, false)
            .SuitRule(BossSuitMode.Fixed, Suit.Hearts, true, Suit.Diamonds)
            .Stats(9, 1, 13, 2.9f, 1.05f, 5f, 0f, false, 0f)
            .Faces(A(Volley, 2, 11f), A(Volley, 3, 11.5f), A(Rain, 3, 18f), A(Jokers, 1, 18f), A(Volley, 3, 12f), A(Rain, 4, 19f))
            .Stage(2, new Color32(0x26, 0x0C, 0x18, 0xFF), new Color32(0x8A, 0x23, 0x40, 0xFF))
            .Dice(1.02f, new Color32(0xFF, 0x7A, 0x8A, 0xFF), false)
            .Combo(0f)
            .Bonus(0f)
            .ReRaise(0.25f)
            .Multiply(0.07f);

        // 3. Valete de Espadas: o irmão mais velho, mais rápido, faixa 1–16, com investidas
        var elder = jack.Clone()
            .Identity("ELDER JACK", "ELDER JACK", "elder", -1, 1, 25, false)
            .SuitRule(BossSuitMode.Fixed, Suit.Spades)
            .Stats(18, 1, 16, 1.9f, 0.92f, 6.2f, 0.3f, false, 0f)
            .Faces(A(Volley, 3, 13.5f), A(Dash, 1, 33f), A(Rain, 4, 21f), A(Jokers, 2, 19f), A(Volley, 4, 14f), A(Dash, 2, 35f))
            .Stage(1, new Color32(0x0C, 0x12, 0x22, 0xFF), new Color32(0x2E, 0x3A, 0x8C, 0xFF))
            .Dice(1.06f, new Color32(0x8C, 0xA8, 0xFF, 0xFF), false)
            .Combo(0.2f)
            .Bonus(0.15f, A(Roulette, 6, 8f))
            .ReRaise(0.3f)
            .Multiply(0.09f)
            .Music(queen.MusicIntro, queen.MusicLoop);

        // 4. Dama: 4 fases de 8 de vida (uma por naipe)
        queen.Identity("QUEEN", "QUEEN", "boss1", 1, 1, 35, true).SuitRule(BossSuitMode.Phases)
             .Stats(8, 1, 20, 1.8f, 0.9f, queen.MoveSpeed, 0.3f, false, 0f);

        // 5. Rei: naipe por ataque, bem resistente
        king.Identity("KING", "KING", "boss2", 2, 2, 50, false).SuitRule(BossSuitMode.PerAttack)
            .Stats(28, 1, 26, 1.35f, 0.9f, king.MoveSpeed, 0.5f, false, 25f);

        // Ás secreto
        config.SecretBoss.Identity("ACE", "ACE", "", -1, 2, 70, false);
        // Coringa do tutorial
        config.TutorialBoss.Identity("JOKER", "JOKER", "", -1, 0, 0, false);

        list.Clear();
        list.AddRange(new List<BossData> { jack, twins, elder, queen, king });

        // Todos os naipes com a mesma chance (o bônus de cada um vem dos coringas)
        var so = new SerializedObject(config);
        foreach (var w in new[] { "clubsWeight", "spadesWeight", "heartsWeight", "diamondsWeight" })
            so.FindProperty(w).floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("[Boss Roster] " + string.Join(" → ", list.ConvertAll(b => b.BannerName)) + " (+ ACE)");
    }

    public static void Batch()
    {
        AssetDatabase.Refresh();
        Setup();
        EditorApplication.Exit(0);
    }
}
