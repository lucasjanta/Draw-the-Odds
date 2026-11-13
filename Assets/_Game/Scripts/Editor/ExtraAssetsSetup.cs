using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Menu "Draw the Odds/Import Extra Assets": configura e liga no GameConfig os assets da pasta
// "choosen assets" já copiados para o projeto (fichas, props, feltro, fonte Silkscreen, sons e músicas).
// Pode rodar várias vezes.
public static class ExtraAssetsSetup
{
    private const string ConfigPath = "Assets/_Game/Data/GameConfig.asset";
    private const string Art = "Assets/_Game/Art/";
    private const string Sfx = "Assets/_Game/Audio/Sfx/";
    private const string Music = "Assets/_Game/Audio/Music/";
    private const string Fonts = "Assets/_Game/Fonts/";
    private const string LiberationPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Draw the Odds/Import Extra Assets")]
    public static void Import()
    {
        var log = new StringBuilder();

        // ---------- Sprites (pixel art: PPU 16, Point, sem compressão) ----------
        foreach (var path in Directory.GetFiles(Art + "Chips", "*.png")) ConfigureSprite(path, SpriteAlignment.Center, Vector4.zero, false);
        foreach (var path in Directory.GetFiles(Art + "Props", "*.png")) ConfigureSprite(path, SpriteAlignment.BottomCenter, Vector4.zero, false);
        // Feltro: bordas de 8 px nos 4 lados e Mesh Type Full Rect (obrigatório para o modo Tiled)
        ConfigureSprite(Art + "Tiles/felt_9slice.png", SpriteAlignment.Center, new Vector4(8, 8, 8, 8), true);

        // ---------- Áudio ----------
        foreach (var path in Directory.GetFiles(Sfx, "*.ogg")) ConfigureAudio(path, false);
        foreach (var path in Directory.GetFiles(Music, "*.ogg")) ConfigureAudio(path, true);

        // ---------- Fonte ----------
        SetupFonts(log);

        // ---------- GameConfig ----------
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        var so = new SerializedObject(config);
        SetArray(so.FindProperty("chips"), Sprite(Art + "Chips/chip_red.png"), Sprite(Art + "Chips/chip_black.png"),
            Sprite(Art + "Chips/chip_blue.png"), Sprite(Art + "Chips/chip_white.png"));
        SetArray(so.FindProperty("props"), Sprite(Art + "Props/pawn.png"), Sprite(Art + "Props/rook.png"));
        so.FindProperty("felt").objectReferenceValue = Sprite(Art + "Tiles/felt_9slice.png");
        so.FindProperty("startingHandSize").intValue = 7;

        so.FindProperty("menuMusicIntro").objectReferenceValue = Clip(Music + "menu_and_tutorial_intro.ogg");
        so.FindProperty("menuMusic").objectReferenceValue = Clip(Music + "menu_and_tutorial_loop.ogg");
        so.FindProperty("gameMusic").objectReferenceValue = Clip(Music + "gameplay_1_loop.ogg");
        so.FindProperty("sfxThrow").objectReferenceValue = Clip(Sfx + "card_throw.ogg");
        so.FindProperty("sfxHit").objectReferenceValue = Clip(Sfx + "enemy_hit.ogg");
        so.FindProperty("sfxMiss").objectReferenceValue = Clip(Sfx + "enemy_hit.ogg");
        so.FindProperty("sfxCritical").objectReferenceValue = Clip(Sfx + "right_number.ogg");
        so.FindProperty("sfxHeal").objectReferenceValue = Clip(Sfx + "heal.ogg");
        so.FindProperty("sfxPlayerHurt").objectReferenceValue = Clip(Sfx + "card_damage.ogg");
        so.FindProperty("sfxBossDefeated").objectReferenceValue = Clip(Sfx + "win_game.ogg");
        so.FindProperty("sfxVictory").objectReferenceValue = Clip(Sfx + "win_game.ogg");
        so.FindProperty("sfxPowerUp").objectReferenceValue = Clip(Sfx + "card_select.ogg");
        so.FindProperty("sfxFusion").objectReferenceValue = Clip(Sfx + "card_result.ogg");
        so.FindProperty("sfxUiClick").objectReferenceValue = Clip(Sfx + "card_select.ogg");
        so.FindProperty("sfxUiHover").objectReferenceValue = Clip(Sfx + "card_hover.ogg");
        so.FindProperty("sfxCardSelect").objectReferenceValue = Clip(Sfx + "card_hover.ogg");
        so.FindProperty("sfxCardDraw").objectReferenceValue = Clip(Sfx + "card_draw.ogg");
        so.FindProperty("sfxNewHand").objectReferenceValue = Clip(Sfx + "new_hand.ogg");
        so.FindProperty("sfxDiceShake").objectReferenceValue = Clip(Sfx + "dice_shake.ogg");

        // Música de cada chefe: Valete e Dama = gameplay 1, Rei (final) = gameplay 2
        var bosses = so.FindProperty("bosses");
        string[] tracks = { "gameplay_1", "gameplay_1", "gameplay_2" };
        for (int i = 0; i < bosses.arraySize && i < tracks.Length; i++)
        {
            var b = bosses.GetArrayElementAtIndex(i);
            b.FindPropertyRelative("musicIntro").objectReferenceValue = Clip($"{Music}{tracks[i]}_intro.ogg");
            b.FindPropertyRelative("musicLoop").objectReferenceValue = Clip($"{Music}{tracks[i]}_loop.ogg");
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("[Import Extra Assets] " + log + "done");
    }

    // ===================== Fonte Silkscreen (TMP) =====================
    private static void SetupFonts(StringBuilder log)
    {
        var regular = MakeFontAsset("Silkscreen-Regular", log);
        var bold = MakeFontAsset("Silkscreen-Bold", log);
        var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath);

        // Símbolos que a Silkscreen não tem (▲▼♣♠♥♦×−±►◄) caem na LiberationSans.
        foreach (var fa in new[] { regular, bold })
        {
            if (liberation != null) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { liberation };
            EditorUtility.SetDirty(fa);
        }

        // Negrito (FontStyles.Bold, peso 700) usa a Silkscreen Bold de verdade, não o negrito "falso".
        var table = regular.fontWeightTable;
        table[7].regularTypeface = bold; // o array é a própria tabela do asset: mudar o item já basta
        EditorUtility.SetDirty(regular);

        // Fonte padrão de todo TextMeshPro do jogo (UI e textos no mundo).
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings != null)
        {
            var sso = new SerializedObject(settings);
            sso.FindProperty("m_defaultFontAsset").objectReferenceValue = regular;
            sso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            log.AppendLine("TMP default font = Silkscreen");
        }
    }

    private static TMP_FontAsset MakeFontAsset(string name, StringBuilder log)
    {
        string assetPath = Fonts + name + " SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>(Fonts + name + ".ttf");
        // SDF (amostra 32 px): bordas nítidas em qualquer tamanho de texto. Dinâmico = acrescenta letras novas sozinho.
        var fa = TMP_FontAsset.CreateFontAsset(font, 32, 4, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, true);
        fa.name = name + " SDF";
        AssetDatabase.CreateAsset(fa, assetPath);
        fa.material.name = name + " SDF Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.atlasTextures[0].name = name + " SDF Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        // Já deixa o ASCII pronto no atlas
        fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·");
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        log.AppendLine("created " + assetPath);
        return fa;
    }

    // ===================== Ajudantes =====================
    private static void ConfigureSprite(string path, SpriteAlignment pivot, Vector4 border, bool fullRect)
    {
        path = path.Replace('\\', '/');
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)pivot;
        settings.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        settings.spriteBorder = border;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    // SFX: descomprime ao carregar (toca na hora), mono. Música: comprimida na memória, carrega em segundo plano.
    private static void ConfigureAudio(string path, bool music)
    {
        path = path.Replace('\\', '/');
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) return;
        importer.forceToMono = !music;
        importer.loadInBackground = music;
        var s = importer.defaultSampleSettings;
        s.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = music ? 0.5f : 0.7f;
        s.preloadAudioData = true;
        importer.defaultSampleSettings = s;
        importer.SaveAndReimport();
    }

    private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    private static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

    private static void SetArray(SerializedProperty array, params Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
