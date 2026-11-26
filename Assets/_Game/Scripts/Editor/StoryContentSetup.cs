using UnityEditor;
using UnityEngine;

// Menu "Draw the Odds/Setup Story Content": liga o conteúdo novo (26/11) no GameConfig que já existia:
//  - sprite do Ás secreto (Art/Cards/Deck/boss_ace_0.png, gerado por Tools/make_ace_card.py);
//  - ataques extras (roleta / cartas marcadas) na Dama e no Rei e a chance de RE-RAISE de cada chefe.
// Pode rodar quantas vezes quiser: os ataques extras só entram se o chefe ainda não tiver nenhum
// (o que você ajustar no Inspector não é sobrescrito).
// Sem o Editor aberto: Unity.exe -batchmode -projectPath <proj> -executeMethod StoryContentSetup.Batch -quit
public static class StoryContentSetup
{
    private const string ConfigPath = "Assets/_Game/Data/GameConfig.asset";
    private const string AcePath = "Assets/_Game/Art/Cards/Deck/boss_ace_0.png";

    [MenuItem("Draw the Odds/Setup Story Content")]
    public static void Setup()
    {
        // Carta do Ás: mesmo import das outras cartas (PPU 16, Point, sem compressão)
        var importer = AssetImporter.GetAtPath(AcePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        else Debug.LogWarning("[Story Content] não achei " + AcePath);

        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        if (config == null) { Debug.LogError("[Story Content] GameConfig não encontrado"); return; }

        var ace = AssetDatabase.LoadAssetAtPath<Sprite>(AcePath);
        config.SecretBoss.EditorSetSprite(ace);

        var bosses = config.Bosses;
        if (bosses.Count > 0) bosses[0].EditorSetBonus(0.2f);
        if (bosses.Count > 1) bosses[1].EditorSetBonus(0.35f, new BossAttack(BossAttackType.Roulette, 7, 8.5f));
        if (bosses.Count > 2) bosses[2].EditorSetBonus(0.5f, new BossAttack(BossAttackType.MarkedCards, 3, 1f), new BossAttack(BossAttackType.Roulette, 9, 9.5f));

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Story Content] done (ace sprite: {(ace != null ? ace.name : "MISSING")})");
    }

    // Para rodar em batch (Editor fechado).
    public static void Batch()
    {
        AssetDatabase.Refresh();
        Setup();
        EditorApplication.Exit(0);
    }
}
