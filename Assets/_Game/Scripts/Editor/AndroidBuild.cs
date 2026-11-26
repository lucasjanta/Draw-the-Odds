#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;

// Menu "Draw the Odds/Build Android": gera um APK leve para testar no celular (Builds/Android/DrawTheOdds.apk).
// Configura tudo sozinho antes de compilar, então dá para rodar de novo sem mexer no Player Settings:
//   - IL2CPP + só ARM64 (todo celular dos últimos ~8 anos; ARMv7 dobraria o tamanho à toa)
//   - só paisagem, sem desenhar embaixo do recorte da câmera (os botões de toque ficam na borda)
//   - código "enxugado" (stripping + minify) para o APK ficar pequeno
//   - sem SDK de portal (GamePix/CrazyGames são só da web)
// A chave de assinatura é a de DEBUG da Unity: serve para instalar e testar, não para a Play Store.
public static class AndroidBuild
{
    private const string ScenePath = "Assets/_Game/Scenes/Game.unity";
    private const string OutputPath = "Builds/Android/DrawTheOdds.apk";
    private const string IconPath = "Assets/_Game/Art/Icon/app_icon.png";
    private const string PackageId = "com.jantagames.drawtheodds";

    [MenuItem("Draw the Odds/Build Android")]
    public static string Build()
    {
        Configure();
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        string result = $"{report.summary.result} errors={report.summary.totalErrors} size={report.summary.totalSize / 1024 / 1024}MB time={report.summary.totalTime}";
        Debug.Log("[Build Android] " + result);
        return result;
    }

    // Linha de comando (Unity sem janela, mais estável que pela ponte):
    // Unity.exe -batchmode -projectPath <projeto> -buildTarget Android -executeMethod AndroidBuild.BuildBatch -logFile <log>
    public static void BuildBatch()
    {
        string result = Build();
        EditorApplication.Exit(result.StartsWith("Succeeded") ? 0 : 1);
    }

    // Só aplica as configurações (útil para conferir no Player Settings sem compilar).
    [MenuItem("Draw the Odds/Android/Apply Settings")]
    public static string Configure()
    {
        // Um Build Profile ativo teria configurações próprias e ignoraria as daqui.
        if (BuildProfile.GetActiveBuildProfile() != null) BuildProfile.SetActiveBuildProfile(null);

        var android = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, PackageId);
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCodeGeneration(android, Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minifyRelease = true;
        PlayerSettings.Android.forceInternetPermission = true; // ranking global (Supabase)
        PlayerSettings.Android.renderOutsideSafeArea = false;  // nada fica embaixo do furo da câmera
        PlayerSettings.Android.startInFullscreen = true;
        PlayerSettings.Android.optimizedFramePacing = true;
        // Android 13+ (obrigatório no 16): sem isto o sistema "come" o botão Voltar e manda o jogo para o fundo.
        // Ligado, o Voltar chega no jogo como a tecla Esc (abre a pausa / volta no menu).
        PlayerSettings.Android.predictiveBackSupport = true;
        // Só OpenGL ES 3: jogo 2D não ganha nada com Vulkan; o APK fica menor e escapa de drivers Vulkan
        // bugados de celular barato (e do emulador do PC, que desenhava tudo errado com Vulkan).
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        EditorUserBuildSettings.buildAppBundle = false; // APK para instalar direto (a Play Store usa .aab)
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        EditorUserBuildSettings.development = false;

        // Só paisagem (as duas direções). Valem só para celular: na web/Windows não mudam nada.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // Sem defines de portal no Android.
        var defines = PlayerSettings.GetScriptingDefineSymbols(android).Split(';')
            .Where(d => d.Length > 0 && d != "DTO_GAMEPIX" && d != "DTO_CRAZY");
        PlayerSettings.SetScriptingDefineSymbols(android, string.Join(";", defines));

        SetIcon();
        return "android settings applied";
    }

    // Ícone do app = o mesmo ícone da loja (Docs/store/icon_256.png, copiado para o projeto).
    private static void SetIcon()
    {
        if (AssetImporter.GetAtPath(IconPath) is TextureImporter importer)
        {
            bool changed = importer.textureType != TextureImporterType.Default || importer.filterMode != FilterMode.Point
                || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled;
            if (changed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;   // pixel art nítido ao reduzir
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null) { Debug.LogWarning("[Build Android] icon not found: " + IconPath); return; }
        int count = PlayerSettings.GetIconSizes(NamedBuildTarget.Android, IconKind.Application).Length;
        PlayerSettings.SetIcons(NamedBuildTarget.Android, Enumerable.Repeat(icon, count).ToArray(), IconKind.Application);
    }
}
#endif
