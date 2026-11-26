#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;

// Builds para portais de jogos web: GamePix e CrazyGames.
//
// Por que existe uma "chave" de portal?
// O plugin da GamePix tem uma biblioteca nativa (libunity-gpx.a) que entra em TODA build WebGL e
// um script que liga sozinho ao abrir o jogo. Na build do itch (ou Poki / CrazyGames no futuro) isso
// não pode existir. Então o plugin só compila com o símbolo DTO_GAMEPIX (asmdefs do plugin têm
// "defineConstraints") e a .a só vai para a build WebGL com a chave ligada.
//
// Passo a passo (menus):
//   1. Draw the Odds/Portal/GamePix     → liga a chave (o Unity recompila; espere terminar)
//   2. Draw the Odds/Build GamePix      → gera Builds/GamePix/<...>.gpx (é o único arquivo que se envia)
//   3. Draw the Odds/Portal/itch (no SDK) → desliga a chave (volta ao normal para as builds do itch)
//
// CrazyGames: mesma ideia com DTO_CRAZY (o SDK em Assets/CrazySDK só compila com ela; o .jslib tem
// "defineConstraints: DTO_CRAZY" no .meta).
//   1. Draw the Odds/Portal/CrazyGames  2. Draw the Odds/Build CrazyGames  → Builds/CrazyGamesRelease
//      (enviar TODOS os arquivos dessa pasta)  3. Draw the Odds/Portal/itch (no SDK)
public static class PortalBuild
{
    private const string GamePixDefine = "DTO_GAMEPIX";
    private const string CrazyDefine = "DTO_CRAZY";
    public const string CrazyOutput = "Builds/CrazyGamesRelease"; // pasta fixa do builder da CrazyGames
    private const string GamePixNativeLib = "Assets/Plugins/GamePix/impl/libunity-gpx.a";
    public const string GamePixOutput = "Builds/GamePix";

    private static bool HasDefine(string d) =>
        PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL).Split(';').Contains(d);

    public static bool IsGamePixActive => HasDefine(GamePixDefine);
    public static bool IsCrazyActive => HasDefine(CrazyDefine);
    public static bool AnyPortalActive => IsGamePixActive || IsCrazyActive;

    [MenuItem("Draw the Odds/Portal/GamePix")]
    public static string UseGamePix() => SetPortal(GamePixDefine);

    [MenuItem("Draw the Odds/Portal/CrazyGames")]
    public static string UseCrazy() => SetPortal(CrazyDefine);

    [MenuItem("Draw the Odds/Portal/itch (no SDK)")]
    public static string UseItch() => SetPortal(null);

    // Build Profiles (Unity 6) têm configurações próprias do Player. Se um estiver ativo, os defines e
    // ajustes vão para ELE, mas o plugin da GamePix desativa o profile antes de compilar (e a build sairia
    // sem o SDK). Por isso as builds de portal sempre usam as configurações globais: sem profile ativo.
    private static void DeactivateBuildProfile()
    {
        var active = BuildProfile.GetActiveBuildProfile();
        if (active == null) return;
        Debug.Log("[Portal] Build Profile '" + active.name + "' deactivated (portal builds use the global Player Settings)");
        BuildProfile.SetActiveBuildProfile(null);
    }

    // Mostra no Console as configurações que importam (para conferir se tudo voltou ao normal).
    [MenuItem("Draw the Odds/Portal/Log Settings")]
    public static string LogSettings()
    {
        var p = BuildProfile.GetActiveBuildProfile();
        string s = $"profile={(p != null ? p.name : "none")} defines={PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL)} " +
                   $"template={PlayerSettings.WebGL.template} web={PlayerSettings.defaultWebScreenWidth}x{PlayerSettings.defaultWebScreenHeight} " +
                   $"runInBg={PlayerSettings.runInBackground} il2cpp={PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL)} " +
                   $"strip={PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL)} wasm2023={PlayerSettings.WebGL.wasm2023} " +
                   $"compression={PlayerSettings.WebGL.compressionFormat} company={PlayerSettings.companyName}";
        Debug.Log("[Portal] " + s);
        return s;
    }

    // Liga UM portal (define) ou nenhum (null). Os portais nunca ficam ligados juntos.
    private static string SetPortal(string define)
    {
        bool on = define == GamePixDefine;
        DeactivateBuildProfile();
        // A chave vale para o alvo WebGL; o Editor precisa estar nele para compilar com ela.
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL)
            .Split(';').Where(d => d.Length > 0 && d != GamePixDefine && d != CrazyDefine).ToList();
        if (define != null) defines.Add(define);
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, string.Join(";", defines));

        // Biblioteca nativa do plugin: só entra na build WebGL com a chave ligada.
        if (AssetImporter.GetAtPath(GamePixNativeLib) is PluginImporter lib)
        {
            lib.SetCompatibleWithAnyPlatform(false);
            lib.SetCompatibleWithEditor(false);
            lib.SetCompatibleWithPlatform(BuildTarget.WebGL, on);
            lib.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        string msg = "[Portal] " + (define ?? "itch (no SDK)") + " - recompiling";
        Debug.Log(msg);
        return msg;
    }

#if DTO_CRAZY
    // Build da CrazyGames pelo builder oficial do SDK (release comprimida, template CrazyGames, versões
    // extras para celular: texturas ASTC e memória limitada). Ele mesmo guarda e restaura as configurações.
    [MenuItem("Draw the Odds/Build CrazyGames")]
    public static string BuildCrazyGames()
    {
        if (!IsCrazyActive) return "CrazyGames is OFF: use Draw the Odds/Portal/CrazyGames first";
        DeactivateBuildProfile();
        string company = PlayerSettings.companyName;
        PlayerSettings.companyName = "Janta Games";
        EditorPrefs.SetBool("CGBuildSupportsMobile", true); // o jogo tem controles de toque
        // A CrazyGames recusa ".unityweb" (= "Decompression fallback" ligado, que a build do itch usa).
        // O servidor deles manda Content-Encoding: br, então o fallback fica desligado nesta build.
        bool fallback = PlayerSettings.WebGL.decompressionFallback;
        PlayerSettings.WebGL.decompressionFallback = false;
        if (Directory.Exists(CrazyOutput)) Directory.Delete(CrazyOutput, true); // sem sobras da build anterior
        try
        {
            new CrazyGames.Builder().DoReleaseBuild(new CrazyGames.AdditionalBuildOptions { supportsMobile = true });
        }
        finally
        {
            PlayerSettings.companyName = company;
            PlayerSettings.WebGL.decompressionFallback = fallback;
            AssetDatabase.SaveAssets();
        }
        bool ok = File.Exists(Path.Combine(CrazyOutput, "index.html"))
                  && Directory.GetFiles(CrazyOutput, "*.unityweb", SearchOption.AllDirectories).Length == 0;
        long bytes = ok ? Directory.GetFiles(CrazyOutput, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) : 0;
        int files = ok ? Directory.GetFiles(CrazyOutput, "*", SearchOption.AllDirectories).Length : 0;
        string result = ok ? $"Succeeded {CrazyOutput} files={files} size={bytes / 1024 / 1024}MB" : "Failed (see Console)";
        Debug.Log("[Build CrazyGames] " + result);
        return result;
    }
#endif

#if DTO_GAMEPIX
    // Build da GamePix pelo plugin oficial. O plugin muda várias configurações do projeto (template,
    // compressão, memória...). Guardamos tudo antes e devolvemos depois, para a build do itch continuar igual.
    [MenuItem("Draw the Odds/Build GamePix")]
    public static string BuildGamePix()
    {
        if (!IsGamePixActive) return "GamePix is OFF: use Draw the Odds/Portal/GamePix first";

        DeactivateBuildProfile(); // a foto tem que ser das configurações globais
        var saved = new SettingsSnapshot();
        string result;
        try
        {
            // NoOverride: as texturas já estão sem compressão (pixel art) e os sons já estão ajustados;
            // o modo padrão do plugin reimportaria TODOS os assets à toa.
            GamePix.Editor.UnityConfigurator.SetWebGLSettings(true, GamePix.Editor.AssetsCompression.NoOverride);
            // A GamePix calcula a proporção do jogo pela tela padrão: 16:9, igual à câmera do jogo.
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 540;
            PlayerSettings.companyName = "Janta Games"; // aparece no nome do .gpx

            bool ok = GamePix.Editor.Builder.BuildArchive(GamePixOutput,
                GamePix.Editor.AssetsCompression.NoOverride, GamePix.Editor.DataFileCompression.Uncompressed, false);
            string gpx = ok ? Directory.GetFiles(GamePixOutput, "*.gpx").FirstOrDefault() : null;
            result = gpx != null
                ? $"Succeeded gpx={gpx} size={new FileInfo(gpx).Length / 1024 / 1024}MB"
                : "Failed (see Console)";
        }
        finally
        {
            saved.Restore();
        }
        Debug.Log("[Build GamePix] " + result);
        return result;
    }

    // Foto das configurações que o plugin da GamePix altera.
    private class SettingsSnapshot
    {
        private readonly string template = PlayerSettings.WebGL.template;
        private readonly WebGLCompressionFormat compression = PlayerSettings.WebGL.compressionFormat;
        private readonly bool fallback = PlayerSettings.WebGL.decompressionFallback;
        private readonly bool dataCaching = PlayerSettings.WebGL.dataCaching;
        private readonly WebGLExceptionSupport exceptions = PlayerSettings.WebGL.exceptionSupport;
        private readonly string emscriptenArgs = PlayerSettings.WebGL.emscriptenArgs;
        private readonly int memorySize = PlayerSettings.WebGL.memorySize;
        private readonly int initialMemory = PlayerSettings.WebGL.initialMemorySize;
        private readonly WebGLMemoryGrowthMode growthMode = PlayerSettings.WebGL.memoryGrowthMode;
        private readonly float growthStep = PlayerSettings.WebGL.geometricMemoryGrowthStep;
        private readonly int growthCap = PlayerSettings.WebGL.memoryGeometricGrowthCap;
        private readonly int maxMemory = PlayerSettings.WebGL.maximumMemorySize;
        private readonly WebGLPowerPreference power = PlayerSettings.WebGL.powerPreference;
        private readonly WebGLDebugSymbolMode symbols = PlayerSettings.WebGL.debugSymbolMode;
        private readonly bool hashNames = PlayerSettings.WebGL.nameFilesAsHashes;
        private readonly bool diagnostics = PlayerSettings.WebGL.showDiagnostics;
        private readonly bool wasm2023 = PlayerSettings.WebGL.wasm2023;
        private readonly bool wasmTable = PlayerSettings.WebGL.webAssemblyTable;
        private readonly bool bigInt = PlayerSettings.WebGL.webAssemblyBigInt;
        private readonly bool runInBackground = PlayerSettings.runInBackground;
        private readonly bool splash = PlayerSettings.SplashScreen.show;
        private readonly bool stripEngine = PlayerSettings.stripEngineCode;
        private readonly bool stripMesh = PlayerSettings.stripUnusedMeshComponents;
        private readonly bool bakeMeshes = PlayerSettings.bakeCollisionMeshes;
        private readonly bool mipStripping = PlayerSettings.mipStripping;
        private readonly int screenW = PlayerSettings.defaultWebScreenWidth;
        private readonly int screenH = PlayerSettings.defaultWebScreenHeight;
        private readonly string company = PlayerSettings.companyName;
        private readonly bool development = EditorUserBuildSettings.development;
        private readonly Il2CppCompilerConfiguration il2cpp = PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL);
        private readonly ManagedStrippingLevel stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL);
        private readonly Il2CppCodeGeneration codeGen = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL);
        private readonly Il2CppStacktraceInformation stacktrace = PlayerSettings.GetIl2CppStacktraceInformation(NamedBuildTarget.WebGL);

        public void Restore()
        {
            PlayerSettings.WebGL.template = template;
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.decompressionFallback = fallback;
            PlayerSettings.WebGL.dataCaching = dataCaching;
            PlayerSettings.WebGL.exceptionSupport = exceptions;
            PlayerSettings.WebGL.emscriptenArgs = emscriptenArgs;
            PlayerSettings.WebGL.memorySize = memorySize;
            PlayerSettings.WebGL.initialMemorySize = initialMemory;
            PlayerSettings.WebGL.memoryGrowthMode = growthMode;
            PlayerSettings.WebGL.geometricMemoryGrowthStep = growthStep;
            PlayerSettings.WebGL.memoryGeometricGrowthCap = growthCap;
            PlayerSettings.WebGL.maximumMemorySize = maxMemory;
            PlayerSettings.WebGL.powerPreference = power;
            PlayerSettings.WebGL.debugSymbolMode = symbols;
            PlayerSettings.WebGL.nameFilesAsHashes = hashNames;
            PlayerSettings.WebGL.showDiagnostics = diagnostics;
            PlayerSettings.WebGL.wasm2023 = wasm2023;
            PlayerSettings.WebGL.webAssemblyTable = wasmTable;
            PlayerSettings.WebGL.webAssemblyBigInt = bigInt;
            PlayerSettings.runInBackground = runInBackground;
            PlayerSettings.SplashScreen.show = splash;
            PlayerSettings.stripEngineCode = stripEngine;
            PlayerSettings.stripUnusedMeshComponents = stripMesh;
            PlayerSettings.bakeCollisionMeshes = bakeMeshes;
            PlayerSettings.mipStripping = mipStripping;
            PlayerSettings.defaultWebScreenWidth = screenW;
            PlayerSettings.defaultWebScreenHeight = screenH;
            PlayerSettings.companyName = company;
            EditorUserBuildSettings.development = development;
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, il2cpp);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, stripping);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, codeGen);
            PlayerSettings.SetIl2CppStacktraceInformation(NamedBuildTarget.WebGL, stacktrace);
            AssetDatabase.SaveAssets();
        }
    }
#endif
}
#endif
