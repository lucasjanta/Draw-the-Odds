using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Menu "Draw the Odds/Balance Audio Levels": mede o volume de cada efeito sonoro ligado no GameConfig
// e calcula um ganho para todos soarem parecidos (equalização).
//
// Como mede: divide o som em janelas de 100 ms, calcula o RMS (a "energia média") de cada uma e pega
// a MAIOR. Isso aproxima o quão alto o ouvido acha um som curto (um pico isolado de 1 ms quase não conta).
// Resultado em dBFS: 0 = volume máximo digital; −20 = bem mais baixo.
//
// Os ganhos só podem ABAIXAR (0..1). Para os sons baixos subirem, o grupo SFX/UI do mixer ganha um
// reforço fixo ("makeup", em dB) e os sons altos são abaixados para compensar.
public static class AudioLevels
{
    private const string ConfigPath = "Assets/_Game/Data/GameConfig.asset";
    private const float TargetDb = -15f;     // volume "padrão" que todo efeito deve ter
    private const float MaxMakeupDb = 9f;    // reforço máximo no mixer (evita distorcer)

    // Ênfase de propósito (dB acima/abaixo do alvo): momentos importantes mais altos, sons
    // repetitivos mais discretos. Só dá para SUBIR quem tem folga (arquivo original alto).
    private static readonly Dictionary<string, float> Emphasis = new Dictionary<string, float>
    {
        { "boss_investida_impacto", 5f }, // batida forte da investida
        { "right_number", 3f },           // crítico
        { "win_game", 3f },               // chefe derrotado / vitória
        { "player_defeated", 2f },
        { "boss_entry", 2f },
        { "card_hover", -4f },            // hover de botão: bem discreto
        { "player_land", -3f },           // pouso acontece toda hora
    };

    [MenuItem("Draw the Odds/Balance Audio Levels")]
    public static void Balance()
    {
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        var so = new SerializedObject(config);

        // Todos os clipes de efeito do GameConfig (campos que começam com "sfx", inclusive listas)
        var clips = new List<AudioClip>();
        var it = so.GetIterator();
        while (it.NextVisible(true))
        {
            if (!it.name.StartsWith("sfx") || it.name == "sfxGains") continue;
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is AudioClip c && !clips.Contains(c))
                clips.Add(c);
            // Listas (ex.: sfxCloseness = match_lower/mid/max): os itens se chamam "data", então lê um por um
            if (it.isArray && it.propertyType == SerializedPropertyType.Generic)
                for (int k = 0; k < it.arraySize; k++)
                    if (it.GetArrayElementAtIndex(k).objectReferenceValue is AudioClip e && !clips.Contains(e)) clips.Add(e);
        }

        // 1) Mede cada clipe
        var measured = new List<(AudioClip clip, float db, float peakDb)>();
        foreach (var clip in clips)
        {
            if (!Measure(clip, out float db, out float peakDb)) { Debug.LogWarning("[Audio Levels] não deu para ler " + clip.name); continue; }
            measured.Add((clip, db, peakDb));
        }

        // 2) Ganho ideal de cada um = o quanto falta/sobra para chegar no alvo.
        //    O mais baixo precisa do maior ganho; esse vira 1 e o resto é proporcional.
        float maxBoost = measured.Max(m => TargetDb + (Emphasis.TryGetValue(m.clip.name, out float x) ? x : 0f) - m.db);
        float makeup = Mathf.Clamp(maxBoost, 0f, MaxMakeupDb);

        var gains = so.FindProperty("sfxGains");
        gains.arraySize = measured.Count;
        var report = new StringBuilder($"alvo {TargetDb} dB, makeup {makeup:0.0} dB\n");
        int i = 0;
        foreach (var m in measured.OrderBy(m => m.db))
        {
            Emphasis.TryGetValue(m.clip.name, out float extra);
            float gainDb = TargetDb + extra - m.db - makeup;       // dB que este clipe precisa mudar
            float gain = Mathf.Clamp01(Mathf.Pow(10f, gainDb / 20f));
            var e = gains.GetArrayElementAtIndex(i++);
            e.FindPropertyRelative("clip").objectReferenceValue = m.clip;
            e.FindPropertyRelative("gain").floatValue = gain;
            e.FindPropertyRelative("measuredDb").floatValue = m.db;
            report.AppendLine($"{m.clip.name,-24} rms {m.db,6:0.0} dB  pico {m.peakDb,6:0.0} dB  ganho {gain:0.00}");
        }
        so.FindProperty("sfxMakeupDb").floatValue = makeup;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        Directory.CreateDirectory("Temp/ClaudeBridge");
        File.WriteAllText("Temp/ClaudeBridge/levels.txt", report.ToString());
        Debug.Log("[Audio Levels]\n" + report);
    }

    private static bool Measure(AudioClip clip, out float rmsDb, out float peakDb)
    {
        rmsDb = peakDb = -80f;
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        var data = new float[clip.samples * clip.channels];
        if (data.Length == 0 || !clip.GetData(data, 0)) return false;

        int window = Mathf.Max(1, clip.frequency / 10) * clip.channels; // 100 ms
        float bestRms = 0f, peak = 0f;
        for (int start = 0; start < data.Length; start += window / 2)    // janelas sobrepostas pela metade
        {
            int end = Mathf.Min(data.Length, start + window);
            double sum = 0;
            for (int s = start; s < end; s++)
            {
                sum += data[s] * data[s];
                peak = Mathf.Max(peak, Mathf.Abs(data[s]));
            }
            bestRms = Mathf.Max(bestRms, Mathf.Sqrt((float)(sum / Mathf.Max(1, end - start))));
        }
        rmsDb = 20f * Mathf.Log10(Mathf.Max(bestRms, 1e-4f));
        peakDb = 20f * Mathf.Log10(Mathf.Max(peak, 1e-4f));
        return true;
    }
}
