using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Menu "Draw the Odds/Import Alagard Font": cria o font asset do TMP para a Alagard (fonte medieval
// pixel art do título e dos nomes dos chefes) e coloca no GameConfig (campo titleFont).
public static class AlagardSetup
{
    private const string Fonts = "Assets/_Game/Fonts/";
    private const string AssetPath = Fonts + "Alagard SDF.asset";

    [MenuItem("Draw the Odds/Import Alagard Font")]
    public static void Run()
    {
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        if (fa == null)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "alagard.ttf");
            if (font == null) { Debug.LogError("alagard.ttf não encontrada em " + Fonts); return; }
            // Mesmo esquema da Silkscreen: SDF dinâmico (adiciona letras sozinho, nítido em qualquer tamanho).
            fa = TMP_FontAsset.CreateFontAsset(font, 32, 4, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, true);
            fa.name = "Alagard SDF";
            AssetDatabase.CreateAsset(fa, AssetPath);
            fa.material.name = "Alagard SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.atlasTextures[0].name = "Alagard SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");
        }

        // Símbolos que a Alagard não tem (naipes, setas) caem na Silkscreen e depois na LiberationSans.
        var silk = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts + "Silkscreen-Regular SDF.asset");
        var list = new List<TMP_FontAsset>();
        if (silk != null)
        {
            list.Add(silk);
            if (silk.fallbackFontAssetTable != null) list.AddRange(silk.fallbackFontAssetTable);
        }
        fa.fallbackFontAssetTable = list;
        EditorUtility.SetDirty(fa);

        var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Game/Data/GameConfig.asset");
        if (config != null)
        {
            var so = new SerializedObject(config);
            so.FindProperty("titleFont").objectReferenceValue = fa;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Alagard] font asset pronto e colocado no GameConfig");
    }
}
