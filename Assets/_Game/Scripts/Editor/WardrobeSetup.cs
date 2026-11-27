using System.Linq;
using UnityEditor;
using UnityEngine;

// Menu "Draw the Odds/Setup Wardrobe" (27/11): liga os chapéus do aldeão no GameConfig.
// Chapéu 0 = Art/Player/adventurer.aseprite (palha); os outros = Art/Player/Hats/villager_<nome>.aseprite,
// gerados pelo Tools/villager_hats.lua. Guarda os quadros de cada um: o Animator do personagem continua o
// mesmo e o VillagerLook troca o sprite pelo quadro de mesmo número do chapéu escolhido.
public static class WardrobeSetup
{
    private const string ConfigPath = "Assets/_Game/Data/GameConfig.asset";
    // Mesma ordem de Wardrobe.Hats
    private static readonly string[] Paths =
    {
        "Assets/_Game/Art/Player/adventurer.aseprite",
        "Assets/_Game/Art/Player/Hats/villager_none.aseprite",
        "Assets/_Game/Art/Player/Hats/villager_cap.aseprite",
        "Assets/_Game/Art/Player/Hats/villager_feather.aseprite",
        "Assets/_Game/Art/Player/Hats/villager_tophat.aseprite",
        "Assets/_Game/Art/Player/Hats/villager_jester.aseprite",
        "Assets/_Game/Art/Player/Hats/villager_crown.aseprite",
    };

    [MenuItem("Draw the Odds/Setup Wardrobe")]
    public static void Setup()
    {
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        if (config == null) { Debug.LogError("[Wardrobe] GameConfig não encontrado"); return; }
        var frames = new GameConfig.HatFrames[Paths.Length];
        for (int i = 0; i < Paths.Length; i++)
        {
            // Quadros "Frame_0", "Frame_1"... (mesmos números em todos os arquivos): índice = número
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Paths[i]).OfType<Sprite>().ToList();
            int max = sprites.Select(s => FrameNumber(s.name)).Where(n => n < 9999).DefaultIfEmpty(-1).Max();
            var list = new Sprite[Mathf.Max(0, max + 1)];
            foreach (var s in sprites) { int n = FrameNumber(s.name); if (n >= 0 && n < list.Length) list[n] = s; }
            frames[i] = new GameConfig.HatFrames { frames = list };
            if (list.Length == 0) Debug.LogWarning($"[Wardrobe] sem quadros em {Paths[i]}");
        }
        config.EditorSetHats(frames);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("[Wardrobe] hats: " + string.Join(", ", frames.Select(f => f.frames.Length.ToString())) + " frames");
    }

    private static int FrameNumber(string name)
    {
        int i = name.LastIndexOf('_');
        return i >= 0 && int.TryParse(name.Substring(i + 1), out int n) ? n : 9999;
    }
}
