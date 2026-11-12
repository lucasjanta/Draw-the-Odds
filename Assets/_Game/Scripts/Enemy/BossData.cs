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
        int layout, Color bg, Color platform, float multiplyChance, params BossAttack[] faces)
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
        dieFaces = faces;
    }
}
