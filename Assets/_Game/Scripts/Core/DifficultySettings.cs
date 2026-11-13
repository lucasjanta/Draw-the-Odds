using System;
using UnityEngine;

public enum Difficulty { Easy, Normal, Hardcore }

// Números de um modo de dificuldade (editáveis no Inspector do GameConfig).
// Escolhido depois do tutorial (ou no "Skip Tutorial"). NORMAL = o jogo como foi balanceado.
[Serializable]
public class DifficultySettings
{
    [SerializeField] private string displayName = "NORMAL";
    [Tooltip("Multiplica a pausa entre ataques. Maior = chefe ataca MENOS vezes.")]
    [SerializeField, Min(0.1f)] private float idleMultiplier = 1f;
    [Tooltip("Multiplica a antecipação (dado rolando). Maior = mais tempo para reagir.")]
    [SerializeField, Min(0.1f)] private float telegraphMultiplier = 1f;
    [Tooltip("Multiplica a velocidade dos projéteis e das investidas.")]
    [SerializeField, Min(0.1f)] private float attackSpeedMultiplier = 1f;
    [Tooltip("Multiplica a chance de ataques misturados (Dama e Rei).")]
    [SerializeField, Min(0f)] private float comboMultiplier = 1f;
    [Tooltip("Segundos reais para escolher as cartas da fusão. 0 = sem limite.")]
    [SerializeField, Min(0f)] private float fusionWindow = 10f;
    [Tooltip("Mostra o resultado da fusão antes de confirmar.")]
    [SerializeField] private bool fusionPreview;
    [Tooltip("Multiplica o intervalo entre orbes. Menor = MAIS orbes.")]
    [SerializeField, Min(0.1f)] private float powerIntervalMultiplier = 1f;
    [SerializeField, Min(0)] private int extraPowersOnMap;
    [Tooltip("Máximo de investidas seguidas por chefe (0 = usa o que o dado mandar). Se o chefe não tem investida no dado, ganha uma.")]
    [SerializeField] private int[] maxDashesPerBoss = new int[3];

    public string DisplayName => displayName;
    public float IdleMultiplier => idleMultiplier;
    public float TelegraphMultiplier => telegraphMultiplier;
    public float AttackSpeedMultiplier => attackSpeedMultiplier;
    public float ComboMultiplier => comboMultiplier;
    public float FusionWindow => fusionWindow;
    public bool FusionPreview => fusionPreview;
    public float PowerIntervalMultiplier => powerIntervalMultiplier;
    public int ExtraPowersOnMap => extraPowersOnMap;
    public int MaxDashes(int bossIndex) =>
        maxDashesPerBoss != null && bossIndex >= 0 && bossIndex < maxDashesPerBoss.Length ? maxDashesPerBoss[bossIndex] : 0;

    public DifficultySettings() { }

    public DifficultySettings(string name, float idle, float telegraph, float speed, float combo, float window, bool preview,
        float powerInterval, int extraPowers, params int[] dashes)
    {
        displayName = name;
        idleMultiplier = idle;
        telegraphMultiplier = telegraph;
        attackSpeedMultiplier = speed;
        comboMultiplier = combo;
        fusionWindow = window;
        fusionPreview = preview;
        powerIntervalMultiplier = powerInterval;
        extraPowersOnMap = extraPowers;
        maxDashesPerBoss = dashes;
    }
}
