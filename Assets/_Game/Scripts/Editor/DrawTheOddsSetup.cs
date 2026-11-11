#if UNITY_EDITOR
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu "Draw the Odds/Setup Scene": prepara assets e a cena Game para o jogo rodar.
// Pode ser executado várias vezes (só cria o que falta).
public static class DrawTheOddsSetup
{
    private const string CardsDir = "Assets/_Game/Art/Cards/";
    private const string ConfigPath = "Assets/_Game/Data/GameConfig.asset";
    private const string ScenePath = "Assets/_Game/Scenes/Game.unity";

    [MenuItem("Draw the Odds/Import TMP Essentials")]
    public static string ImportTmp()
    {
        if (Resources.Load<TMP_Settings>("TMP Settings") != null) return "already";
        TMP_PackageResourceImporter.ImportResources(true, false, false);
        return "importing";
    }

    [MenuItem("Draw the Odds/Setup Scene")]
    public static string Setup()
    {
        var log = new StringBuilder();

        // 1) Sprites novos (chefes placeholder e verso da carta) com as configs de pixel art
        foreach (var name in new[] { "SPADES_JACK", "HEARTS_QUEEN", "DIAMONDS_KING", "BACK_HYPNO", "CLUBS_ACE" })
            ConfigureSprite(CardsDir + name + ".png", log);

        // 2) GameConfig
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            log.AppendLine("created GameConfig");
        }
        var so = new SerializedObject(config);
        so.FindProperty("clubsBlank").objectReferenceValue = LoadSprite(CardsDir + "CLUBS_BLANK.png");
        so.FindProperty("spadesBlank").objectReferenceValue = LoadSprite(CardsDir + "SPADES_BLANK.png");
        so.FindProperty("heartsBlank").objectReferenceValue = LoadSprite(CardsDir + "HEARTS_BLANK.png");
        so.FindProperty("diamondsBlank").objectReferenceValue = LoadSprite(CardsDir + "DIAMONDS_BLANK.png");
        so.FindProperty("cardBack").objectReferenceValue = LoadSprite(CardsDir + "BACK_HYPNO.png");
        so.FindProperty("square").objectReferenceValue = LoadSprite("Assets/_Game/Art/Square.png");
        var dice = so.FindProperty("dieFaces");
        dice.arraySize = 6;
        for (int i = 0; i < 6; i++)
            dice.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite($"{CardsDir}DIE_0{i + 1}.png");
        var bosses = so.FindProperty("bosses");
        string[] bossSprites = { "SPADES_JACK", "HEARTS_QUEEN", "DIAMONDS_KING" };
        for (int i = 0; i < bosses.arraySize && i < bossSprites.Length; i++)
            bosses.GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue = LoadSprite(CardsDir + bossSprites[i] + ".png");
        so.FindProperty("tutorialBoss").FindPropertyRelative("sprite").objectReferenceValue = LoadSprite(CardsDir + "CLUBS_ACE.png");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);

        // 3) Cena
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

        var floor = GameObject.Find("Floor");
        floor.transform.position = new Vector3(0f, -9.375f, 0f);
        floor.transform.localScale = new Vector3(42f, 3.75f, 1f);

        // Find() não acha objetos desativados: procura o template como filho do "Level".
        var templateTransform = GameObject.Find("Level").transform.Find("PlatformTemplate");
        var template = templateTransform != null ? templateTransform.gameObject : null;
        if (template == null)
        {
            template = GameObject.Find("Platform_C2");
            template.name = "PlatformTemplate";
        }
        foreach (var n in new[] { "Platform_L1", "Platform_R1", "Platform_L3", "Platform_R3" })
        {
            var p = GameObject.Find(n);
            if (p != null) Object.DestroyImmediate(p);
        }

        var player = Object.FindAnyObjectByType<PlayerController>();
        player.transform.position = new Vector3(0f, -7.45f, 0f);
        if (player.GetComponent<PlayerHealth>() == null) player.gameObject.AddComponent<PlayerHealth>();
        if (player.GetComponent<PlayerCards>() == null) player.gameObject.AddComponent<PlayerCards>();

        var gmObject = GameObject.Find("Game") ?? new GameObject("Game");
        var gm = gmObject.GetComponent<GameManager>() ?? gmObject.AddComponent<GameManager>();
        var gmSo = new SerializedObject(gm);
        gmSo.FindProperty("config").objectReferenceValue = config;
        gmSo.FindProperty("player").objectReferenceValue = player;
        gmSo.FindProperty("platformTemplate").objectReferenceValue = template.transform;
        gmSo.FindProperty("floor").objectReferenceValue = floor.GetComponent<SpriteRenderer>();
        gmSo.ApplyModifiedPropertiesWithoutUndo();
        template.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // 4) Build settings / player
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        AssetDatabase.SaveAssets();

        log.AppendLine("setup done");
        return log.ToString();
    }

    // Build WebGL em Builds/WebGL (Gzip + fallback de descompressão, funciona no itch.io).
    [MenuItem("Draw the Odds/Build WebGL")]
    public static string BuildWebGL()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        return $"{report.summary.result} errors={report.summary.totalErrors} size={report.summary.totalSize / 1024 / 1024}MB time={report.summary.totalTime}";
    }

    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    private static void ConfigureSprite(string path, StringBuilder log)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { log.AppendLine("missing " + path); return; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}
#endif
