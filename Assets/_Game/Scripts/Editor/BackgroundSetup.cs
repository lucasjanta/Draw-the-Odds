using UnityEditor;
using UnityEngine;

// Menu "Draw the Odds/Import Backgrounds": configura as imagens de fundo e liga no GameConfig.
// Cada imagem recebe um PPU que faz ela cobrir a tela inteira (40 × 22,5 unidades):
// PPU = largura em pixels / 40 (1920 px → 48; 320 px → 8). Filtro Point (pixel art).
public static class BackgroundSetup
{
    private const string ConfigPath = "Assets/_Game/Data/GameConfig.asset";
    private const string Dir = "Assets/_Game/Art/Backgrounds/";

    // Ordem = índice no GameConfig (backgroundIndex). 0 = parede de tijolos (o escolhido), 1 = castelo.
    private static readonly string[] Files = { "dungeon_brick_wall.png", "castle_bg.png" };

    [MenuItem("Draw the Odds/Import Backgrounds")]
    public static void Import()
    {
        var sprites = new Sprite[Files.Length];
        for (int i = 0; i < Files.Length; i++)
        {
            string path = Dir + Files[i];
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("[Backgrounds] não achei " + path); continue; }

            importer.GetSourceTextureWidthAndHeight(out int width, out _);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = width / 40f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        var so = new SerializedObject(config);
        var list = so.FindProperty("backgrounds");
        list.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("[Import Backgrounds] done");
    }
}
