#if UNITY_EDITOR
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu "Draw the Odds/Setup Scene": prepara assets e a cena Game para o jogo rodar.
// Pode ser executado várias vezes (só cria o que falta).
// Menu "Draw the Odds/Import Card Art": reexporta o .ase das cartas e liga os sprites no GameConfig.
public static class DrawTheOddsSetup
{
    private const string CardsDir = "Assets/_Game/Art/Cards/";
    private const string ConfigPath = "Assets/_Game/Data/GameConfig.asset";
    private const string ScenePath = "Assets/_Game/Scenes/Game.unity";

    // Arte das cartas (pixel art 58×80): o .ase fica fora do repositório; o menu "Import Card Art"
    // exporta cada quadro por tag (Tools/export_cards.lua) para DeckDir e liga tudo no GameConfig.
    private const string DeckDir = "Assets/_Game/Art/Cards/Deck/";
    private const string AsepriteExe = @"C:\Program Files (x86)\Steam\steamapps\common\Aseprite\Aseprite.exe";
    private const string CardsAse = @"C:\Users\lucas\OneDrive\Documentos\Game Dev\Unity Assets\drawtheodds\choosen assets\playing_cards_x1.ase";
    private static readonly string[] SuitTags = { "clubs", "spades", "hearts", "diamonds" }; // mesma ordem do enum Suit

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


        // 2) GameConfig
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            log.AppendLine("created GameConfig");
        }
        var so = new SerializedObject(config);
        so.FindProperty("square").objectReferenceValue = LoadSprite("Assets/_Game/Art/Square.png");
        var dice = so.FindProperty("dieFaces");
        dice.arraySize = 6;
        for (int i = 0; i < 6; i++)
            dice.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite($"{CardsDir}DIE_0{i + 1}.png");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        AssignCardArt(config, log);

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

    // Reexporta o .ase das cartas e liga os sprites no GameConfig (rodar sempre que editar o .ase).
    [MenuItem("Draw the Odds/Import Card Art")]
    public static string ImportCardArt()
    {
        var log = new StringBuilder();
        string outDir = System.IO.Path.GetFullPath(DeckDir);
        System.IO.Directory.CreateDirectory(outDir);
        string script = System.IO.Path.GetFullPath("Tools/export_cards.lua");

        var psi = new System.Diagnostics.ProcessStartInfo(AsepriteExe,
            $"--batch --script-param file=\"{CardsAse}\" --script-param out=\"{outDir.TrimEnd('\\', '/')}\" --script \"{script}\"")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
        };
        using (var proc = System.Diagnostics.Process.Start(psi))
        {
            log.AppendLine(proc.StandardOutput.ReadToEnd().Trim());
            string err = proc.StandardError.ReadToEnd().Trim();
            if (err.Length > 0) log.AppendLine("aseprite: " + err);
            proc.WaitForExit();
        }

        AssetDatabase.Refresh();
        foreach (var path in System.IO.Directory.GetFiles(DeckDir, "*.png"))
            ConfigureSprite(path.Replace('\\', '/'), log);

        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        AssignCardArt(config, log);
        AssetDatabase.SaveAssets();
        log.AppendLine("card art imported");
        Debug.Log("[Import Card Art] " + log);
        return log.ToString();
    }

    private static void AssignCardArt(GameConfig config, StringBuilder log)
    {
        var so = new SerializedObject(config);
        // Cartas da mão / carta arremessada / pacotes = os quadros ui_<naipe>
        so.FindProperty("clubsBlank").objectReferenceValue = LoadSprite(DeckDir + "ui_clubs_0.png");
        so.FindProperty("spadesBlank").objectReferenceValue = LoadSprite(DeckDir + "ui_spades_0.png");
        so.FindProperty("heartsBlank").objectReferenceValue = LoadSprite(DeckDir + "ui_hearts_0.png");
        so.FindProperty("diamondsBlank").objectReferenceValue = LoadSprite(DeckDir + "ui_diamonds_0.png");
        so.FindProperty("cardBack").objectReferenceValue = LoadSprite(DeckDir + "card_back_0.png");

        var jokers = so.FindProperty("jokers");
        jokers.arraySize = 4;
        for (int i = 0; i < 4; i++)
            jokers.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite($"{DeckDir}tutorial_jokers_{i}.png");

        // Chefes 0,1,2 = J, Q, K; cada um com um sprite por naipe (sorteado ao surgir)
        var bosses = so.FindProperty("bosses");
        for (int b = 0; b < bosses.arraySize && b < 3; b++)
        {
            var boss = bosses.GetArrayElementAtIndex(b);
            var suits = boss.FindPropertyRelative("suitSprites");
            suits.arraySize = 4;
            for (int s = 0; s < 4; s++)
                suits.GetArrayElementAtIndex(s).objectReferenceValue = LoadSprite($"{DeckDir}bosses_{SuitTags[s]}_{b}.png");
            boss.FindPropertyRelative("sprite").objectReferenceValue = LoadSprite($"{DeckDir}bosses_spades_{b}.png");
            boss.FindPropertyRelative("spriteScale").floatValue = 1f;
            boss.FindPropertyRelative("canJokers").boolValue = true;
        }

        // Tutorial: o coringa branco (variação 0), sem naipes e sem o ataque dos coringas
        var tutorial = so.FindProperty("tutorialBoss");
        tutorial.FindPropertyRelative("displayName").stringValue = "JOKER";
        tutorial.FindPropertyRelative("sprite").objectReferenceValue = LoadSprite(DeckDir + "tutorial_jokers_0.png");
        tutorial.FindPropertyRelative("suitSprites").arraySize = 0;
        tutorial.FindPropertyRelative("spriteScale").floatValue = 1f;
        tutorial.FindPropertyRelative("canJokers").boolValue = false;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        log.AppendLine("assigned card art to GameConfig");
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

    // Build Windows (executável) em Builds/Windows/DrawTheOdds.exe. Abre em tela cheia (Alt+Enter alterna).
    [MenuItem("Draw the Odds/Build Windows")]
    public static string BuildWindows()
    {
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.resizableWindow = true;
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/Windows/DrawTheOdds.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        string result = $"{report.summary.result} errors={report.summary.totalErrors} size={report.summary.totalSize / 1024 / 1024}MB time={report.summary.totalTime}";
        Debug.Log("[Build Windows] " + result);
        return result;
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
