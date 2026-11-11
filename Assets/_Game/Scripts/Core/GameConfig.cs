using System.Collections.Generic;
using UnityEngine;

// Todas as referências e números de balanceamento do jogo num asset só (Assets/_Game/Data/GameConfig).
[CreateAssetMenu(menuName = "Draw the Odds/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Sprites")]
    [SerializeField] private Sprite clubsBlank;
    [SerializeField] private Sprite spadesBlank;
    [SerializeField] private Sprite heartsBlank;
    [SerializeField] private Sprite diamondsBlank;
    [SerializeField] private Sprite cardBack;
    [SerializeField] private Sprite[] dieFaces = new Sprite[6];
    [SerializeField] private Sprite square;

    [Header("Mão")]
    [SerializeField] private int maxHandSize = 10;
    [SerializeField] private int startingHandSize = 5;
    [SerializeField] private int redrawsPerBoss = 3;
    [SerializeField] private float throwCooldown = 0.22f;
    [SerializeField] private float cardSpeed = 22f;
    [SerializeField] private float cardHomingDegreesPerSecond = 160f;

    [Header("Pickups de carta")]
    [SerializeField] private int lowHandThreshold = 3;     // abaixo/igual disto surgem pacotes de carta
    [SerializeField] private int minPackSize = 2;
    [SerializeField] private int maxPackSize = 4;
    [SerializeField, Range(0f, 1f)] private float specificSuitChance = 0.35f;

    [Header("Poderes (fusão)")]
    [SerializeField] private Vector2 powerSpawnInterval = new Vector2(8f, 13f);
    [SerializeField] private int maxPowersOnMap = 2;
    [SerializeField] private float fusionTimeScale = 0.2f;
    [SerializeField] private float fusionWindow = 6f;      // segundos reais para escolher
    [SerializeField] private bool allowMixedSuits = true;  // juntar naipes diferentes → naipe sorteado

    [Header("Player")]
    [SerializeField] private int playerMaxHp = 8;

    [Header("Chefes (em ordem)")]
    [SerializeField] private List<BossData> bosses = new List<BossData>
    {
        new BossData("JACK", 12, 13, 2.6f, 1.1f, 1, 3, 8f, false, false, 0f, 0,
            new Color32(0x1B, 0x10, 0x29, 0xFF), new Color32(0x2A, 0x24, 0x7A, 0xFF)),
        new BossData("QUEEN", 16, 20, 2.1f, 0.95f, 2, 4, 10f, true, true, 0f, 1,
            new Color32(0x24, 0x0E, 0x22, 0xFF), new Color32(0x6B, 0x1F, 0x4E, 0xFF)),
        new BossData("KING", 20, 26, 1.7f, 0.85f, 2, 6, 11.5f, true, false, 25f, 2,
            new Color32(0x1E, 0x16, 0x08, 0xFF), new Color32(0x7A, 0x5A, 0x16, 0xFF)),
    };

    public Sprite CardBack => cardBack;
    public Sprite Square => square;
    public Sprite DieFace(int face) => dieFaces[Mathf.Clamp(face, 1, 6) - 1];

    public Sprite Blank(Suit suit) => suit switch
    {
        Suit.Clubs => clubsBlank,
        Suit.Spades => spadesBlank,
        Suit.Hearts => heartsBlank,
        _ => diamondsBlank,
    };

    public int MaxHandSize => maxHandSize;
    public int StartingHandSize => startingHandSize;
    public int RedrawsPerBoss => redrawsPerBoss;
    public float ThrowCooldown => throwCooldown;
    public float CardSpeed => cardSpeed;
    public float CardHomingDegreesPerSecond => cardHomingDegreesPerSecond;
    public int LowHandThreshold => lowHandThreshold;
    public int MinPackSize => minPackSize;
    public int MaxPackSize => maxPackSize;
    public float SpecificSuitChance => specificSuitChance;
    public Vector2 PowerSpawnInterval => powerSpawnInterval;
    public int MaxPowersOnMap => maxPowersOnMap;
    public float FusionTimeScale => fusionTimeScale;
    public float FusionWindow => fusionWindow;
    public bool AllowMixedSuits => allowMixedSuits;
    public int PlayerMaxHp => playerMaxHp;
    public IReadOnlyList<BossData> Bosses => bosses;
}
