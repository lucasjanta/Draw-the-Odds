using System;
using UnityEngine;

// Dados de um chefe (editáveis no Inspector do GameConfig).
// [Serializable] = o Unity consegue salvar/mostrar esta classe dentro de outro asset.
[Serializable]
public class BossData
{
    [SerializeField] private string displayName = "JACK";
    [SerializeField] private Sprite sprite;                       // usado quando não há sprites por naipe (ex.: coringa do tutorial)
    [Tooltip("Um sprite por naipe, na ordem do enum Suit: Clubs, Spades, Hearts, Diamonds. Cada vez que o chefe surge, sorteia um.")]
    [SerializeField] private Sprite[] suitSprites = new Sprite[4];
    [SerializeField] private float spriteScale = 1f;              // pixel art: sempre 1 (sprites já no tamanho final)

    [Header("Vida e número")]
    [SerializeField] private int maxHp = 12;
    [SerializeField] private int minNumber = 1;
    [SerializeField] private int maxNumber = 13;
    [SerializeField] private bool rerollAtHalfHp;          // troca o número ao chegar na metade da vida
    [SerializeField] private float rerollEverySeconds;     // 0 = desligado

    [Header("Ataques")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float idleTime = 2.4f;        // pausa entre ataques
    [SerializeField] private float telegraphTime = 1.0f;   // antecipação (dado rolando + face à mostra)
    [Tooltip("Quanto o chefe acelera conforme perde vida. 0 = nunca; 0,5 = com a vida quase no fim, pausas/passeio 50% mais rápidos e projéteis 25% mais rápidos.")]
    [SerializeField, Range(0f, 1f)] private float enrage;
    [Tooltip("Chance de MISTURAR dois ataques de uma vez (o dado mostra os dois). 0 = nunca. Também dá chance de emendar um ataque no outro sem pausa.")]
    [SerializeField, Range(0f, 1f)] private float comboChance;
    [Tooltip("Chance de emendar o próximo ataque SEM a pausa entre ataques (0 = nunca).")]
    [SerializeField, Range(0f, 1f)] private float chainChance;
    [Tooltip("Multiplica a velocidade dos dados (Volley) deste chefe. 1 = a velocidade das faces.")]
    [SerializeField, Min(0.1f)] private float diceSpeedMultiplier = 1f;
    [Tooltip("Cor do rastro de meteoro dos dados.")]
    [SerializeField] private Color diceTrailColor = new Color32(0xFF, 0xB0, 0x40, 0xFF);
    [Tooltip("Dados \"perigosos\": brilho em volta e rastro maior que termina em vermelho escuro.")]
    [SerializeField] private bool dangerousDice;
    [Tooltip("O dado da antecipação escolhe o ataque: elemento 0 = face 1 ... elemento 5 = face 6.")]
    [SerializeField] private BossAttack[] dieFaces = new BossAttack[0];
    [SerializeField, Range(0f, 1f)] private float multiplyChance = 0.08f; // chance de um poder ser ×

    [Header("Música (intro toca 1x e emenda no loop)")]
    [SerializeField] private AudioClip musicIntro;
    [SerializeField] private AudioClip musicLoop;

    [Header("Fase")]
    [SerializeField] private int layoutIndex;
    [SerializeField] private Color backgroundColor = new Color32(0x1B, 0x10, 0x29, 0xFF);
    [SerializeField] private Color platformColor = new Color32(0x10, 0x0E, 0x54, 0xFF);

    public string DisplayName => displayName;
    public Sprite Sprite => sprite;
    public bool HasSuits => suitSprites != null && suitSprites.Length == 4 && suitSprites[0] != null;

    // Sprite do chefe para um naipe (cai no sprite único se não houver sprites por naipe).
    public Sprite SpriteFor(Suit suit) => HasSuits && suitSprites[(int)suit] != null ? suitSprites[(int)suit] : sprite;
    public float SpriteScale => spriteScale;
    public int MaxHp => maxHp;
    public int MinNumber => minNumber;
    public int MaxNumber => maxNumber;
    public bool RerollAtHalfHp => rerollAtHalfHp;
    public float RerollEverySeconds => rerollEverySeconds;
    public float MoveSpeed => moveSpeed;
    public float IdleTime => idleTime;
    public float TelegraphTime => telegraphTime;
    public float Enrage => enrage;
    public float ComboChance => comboChance;
    public float ChainChance => chainChance;
    public float DiceSpeedMultiplier => diceSpeedMultiplier;
    public Color DiceTrailColor => diceTrailColor;
    public bool DangerousDice => dangerousDice;
    public bool HasAttack(BossAttackType type)
    {
        if (dieFaces != null) foreach (var a in dieFaces) if (a != null && a.Type == type) return true;
        return false;
    }

    // Usado só nos valores padrão do GameConfig: BossData("QUEEN", ...).Combo(0.3f)
    public BossData Combo(float chance) { comboChance = chance; chainChance = chance * 0.4f; return this; }

    // Valores padrão dos dados (velocidade extra, cor do rastro, versão perigosa)
    public BossData Dice(float speedMultiplier, Color trail, bool dangerous)
    {
        diceSpeedMultiplier = speedMultiplier;
        diceTrailColor = trail;
        dangerousDice = dangerous;
        return this;
    }
    public int FaceCount => dieFaces != null ? dieFaces.Length : 0;

    // Ataque de uma face (1..6). Se a lista tiver menos de 6, as faces "dão a volta" na lista.
    public BossAttack AttackForFace(int face)
    {
        if (FaceCount == 0) return new BossAttack(BossAttackType.Volley, 2, 9f);
        return dieFaces[(Mathf.Clamp(face, 1, 6) - 1) % FaceCount] ?? new BossAttack();
    }
    public float MultiplyChance => multiplyChance;
    public int LayoutIndex => layoutIndex;
    public AudioClip MusicIntro => musicIntro;
    public AudioClip MusicLoop => musicLoop;
    public Color BackgroundColor => backgroundColor;
    public Color PlatformColor => platformColor;

    public BossData() { }

    // Construtor usado só para criar os valores padrão dos chefes.
    public BossData(string name, int hp, int maxNumber, float idle, float telegraph, bool rerollHalf, float rerollEvery,
        int layout, Color bg, Color platform, float multiplyChance, float enrage, params BossAttack[] faces)
    {
        displayName = name;
        maxHp = hp;
        this.maxNumber = maxNumber;
        idleTime = idle;
        telegraphTime = telegraph;
        rerollAtHalfHp = rerollHalf;
        rerollEverySeconds = rerollEvery;
        layoutIndex = layout;
        backgroundColor = bg;
        platformColor = platform;
        this.multiplyChance = multiplyChance;
        this.enrage = enrage;
        dieFaces = faces;
    }
}
