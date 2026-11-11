using System;
using UnityEngine;

// Dados de um chefe (editáveis no Inspector do GameConfig).
// [Serializable] = o Unity consegue salvar/mostrar esta classe dentro de outro asset.
[Serializable]
public class BossData
{
    [SerializeField] private string displayName = "JACK";
    [SerializeField] private Sprite sprite;
    [SerializeField] private float spriteScale = 1.7f;

    [Header("Vida e número")]
    [SerializeField] private int maxHp = 12;
    [SerializeField] private int minNumber = 1;
    [SerializeField] private int maxNumber = 13;
    [SerializeField] private bool rerollAtHalfHp;          // troca o número ao chegar na metade da vida
    [SerializeField] private float rerollEverySeconds;     // 0 = desligado

    [Header("Ataques")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float idleTime = 2.4f;        // pausa entre ataques
    [SerializeField] private float telegraphTime = 1.0f;   // antecipação (dado rolando)
    [SerializeField] private int minDice = 1;
    [SerializeField] private int maxDice = 3;
    [SerializeField] private float diceSpeed = 9f;
    [SerializeField] private bool canRain = true;
    [SerializeField] private bool canDash;

    [Header("Fase")]
    [SerializeField] private int layoutIndex;
    [SerializeField] private Color backgroundColor = new Color32(0x1B, 0x10, 0x29, 0xFF);
    [SerializeField] private Color platformColor = new Color32(0x10, 0x0E, 0x54, 0xFF);

    public string DisplayName => displayName;
    public Sprite Sprite => sprite;
    public float SpriteScale => spriteScale;
    public int MaxHp => maxHp;
    public int MinNumber => minNumber;
    public int MaxNumber => maxNumber;
    public bool RerollAtHalfHp => rerollAtHalfHp;
    public float RerollEverySeconds => rerollEverySeconds;
    public float MoveSpeed => moveSpeed;
    public float IdleTime => idleTime;
    public float TelegraphTime => telegraphTime;
    public int MinDice => minDice;
    public int MaxDice => maxDice;
    public float DiceSpeed => diceSpeed;
    public bool CanRain => canRain;
    public bool CanDash => canDash;
    public int LayoutIndex => layoutIndex;
    public Color BackgroundColor => backgroundColor;
    public Color PlatformColor => platformColor;

    public BossData() { }

    // Construtor usado só para criar os valores padrão dos 3 chefes.
    public BossData(string name, int hp, int maxNumber, float idle, float telegraph, int minDice, int maxDice,
        float diceSpeed, bool canDash, bool rerollHalf, float rerollEvery, int layout, Color bg, Color platform)
    {
        displayName = name;
        maxHp = hp;
        this.maxNumber = maxNumber;
        idleTime = idle;
        telegraphTime = telegraph;
        this.minDice = minDice;
        this.maxDice = maxDice;
        this.diceSpeed = diceSpeed;
        this.canDash = canDash;
        rerollAtHalfHp = rerollHalf;
        rerollEverySeconds = rerollEvery;
        layoutIndex = layout;
        backgroundColor = bg;
        platformColor = platform;
    }
}
