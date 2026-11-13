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
    [SerializeField] private Sprite[] jokers = new Sprite[4];   // variações do coringa (ataque dos chefes)
    [SerializeField] private Sprite[] chips = new Sprite[4];    // fichas de poker (chuva dos chefes): vermelha, preta, azul, branca
    [SerializeField] private Sprite[] props = new Sprite[2];    // decoração em cima das plataformas (peão, torre)
    [SerializeField] private Sprite felt;                       // feltro em 9-slice (cinza, tingido com a cor do chefe)
    [Tooltip("Fundos da fase (cinza, tingidos com a cor do chefe). Menu Draw the Odds/Import Backgrounds preenche.")]
    [SerializeField] private Sprite[] backgrounds = new Sprite[0];
    [Tooltip("Qual fundo da lista usar (0 = parede de tijolos, 1 = castelo).")]
    [SerializeField] private int backgroundIndex = 1;
    [Tooltip("Quanto da cor das plataformas entra no fundo (0 = cinza puro, 1 = cor cheia).")]
    [SerializeField, Range(0f, 1f)] private float backgroundTint = 0.55f;
    [Tooltip("Brilho do fundo (menor = mais escuro, para o jogo se destacar).")]
    [SerializeField, Range(0f, 2f)] private float backgroundBrightness = 1f;

    [Tooltip("Fonte do título do jogo e dos nomes dos chefes (menu Draw the Odds/Import Alagard Font cria e preenche).")]
    [SerializeField] private TMPro.TMP_FontAsset titleFont;

    [Header("Mão")]
    [SerializeField] private int maxHandSize = 10;
    [SerializeField] private int startingHandSize = 7;
    [Tooltip("Redraws (R) para a RUN inteira: não voltam ao passar de chefe.")]
    [SerializeField] private int redrawsPerBoss = 3;
    [SerializeField] private float throwCooldown = 0.22f;
    [SerializeField] private float cardSpeed = 22f;
    [SerializeField] private float cardHomingDegreesPerSecond = 55f;
    [SerializeField] private float cardHomingConeDegrees = 30f; // só corrige se a carta já vai mais ou menos na direção do chefe

    [Header("Pickups de carta")]
    [SerializeField] private int lowHandThreshold = 7;     // abaixo/igual disto surgem pacotes de carta
    [SerializeField] private int minPackSize = 2;
    [SerializeField] private int maxPackSize = 4;
    [SerializeField, Range(0f, 1f)] private float specificSuitChance = 0.35f;

    [Header("Chance de cada naipe (peso relativo; maior = mais comum)")]
    [SerializeField, Min(0f)] private float clubsWeight = 1f;
    [SerializeField, Min(0f)] private float spadesWeight = 1f;
    [SerializeField, Min(0f)] private float heartsWeight = 0.3f;    // cura: só de vez em quando
    [SerializeField, Min(0f)] private float diamondsWeight = 0.3f;

    [Header("Poderes (fusão)")]
    [SerializeField] private Vector2 powerSpawnInterval = new Vector2(2.8f, 4.5f);
    [SerializeField] private int maxPowersOnMap = 3;
    [Tooltip("Quanto tempo (s) um orbe fica no mapa antes de sumir: sorteado entre x e y. Pisca no fim.")]
    [SerializeField] private Vector2 powerLifetime = new Vector2(10f, 20f);
    [SerializeField] private float fusionWindow = 10f;     // segundos reais para escolher (tempo parado)
    [SerializeField] private bool allowMixedSuits = true;  // juntar naipes diferentes → naipe sorteado

    [Header("Player")]
    [SerializeField] private int playerMaxHp = 8;
    [SerializeField] private float playerMoveSpeed = 10.5f;
    [SerializeField] private float playerJumpVelocity = 23f;
    [SerializeField] private float playerGravityScale = 4.2f;  // mais gravidade = pulo menos "lunar"
    [SerializeField] private float playerMaxFallSpeed = 30f;
    [Tooltip("Segundos entre um dash e outro (longo para o dash não virar spam).")]
    [SerializeField] private float playerDashCooldown = 4.2f;

    [Header("Dificuldade (ordem: EASY, NORMAL, HARDCORE)")]
    [SerializeField] private DifficultySettings[] difficulties =
    {
        // nome, pausa, antecipação, velocidade, combos, janela da fusão, preview, intervalo dos orbes, orbes extras, investidas máx.
        new DifficultySettings("EASY", 1.35f, 1.15f, 0.9f, 0.5f, 0f, true, 0.55f, 2, 0, 0, 0),
        new DifficultySettings("NORMAL", 1f, 1f, 1f, 1f, 10f, false, 1f, 0, 0, 0, 0),
        new DifficultySettings("HARDCORE", 0.75f, 0.85f, 1.2f, 1.4f, 6f, false, 1f, 0, 2, 3, 5),
    };

    // Atalho para escrever as faces do dado abaixo.
    private static BossAttack A(BossAttackType t, int count, float speed) => new BossAttack(t, count, speed);
    private const BossAttackType Volley = BossAttackType.Volley, Rain = BossAttackType.Rain,
        Dash = BossAttackType.Dash, Jokers = BossAttackType.Jokers;

    // Cada chefe: 6 faces do dado = 6 ataques (tipo, quantidade, velocidade). Editáveis no Inspector.
    [Header("Chefes (em ordem)")]
    [SerializeField] private List<BossData> bosses = new List<BossData>
    {
        new BossData("JACK", 12, 13, 2.2f, 1.05f, false, 0f, 0,
            new Color32(0x1B, 0x10, 0x29, 0xFF), new Color32(0x2A, 0x24, 0x7A, 0xFF), 0.06f, 0f,
            A(Volley, 2, 11f), A(Volley, 3, 11f), A(Rain, 3, 18f), A(Jokers, 1, 18f), A(Volley, 4, 12f), A(Rain, 5, 19f)),
        new BossData("QUEEN", 16, 20, 1.7f, 0.9f, true, 0f, 1,
            new Color32(0x24, 0x0E, 0x22, 0xFF), new Color32(0x6B, 0x1F, 0x4E, 0xFF), 0.1f, 0.35f,
            A(Volley, 3, 13.5f), A(Dash, 1, 33f), A(Rain, 4, 21f), A(Jokers, 2, 19f), A(Volley, 5, 14.5f), A(Dash, 2, 35f)).Combo(0.3f).Dice(1.08f, new Color32(0xFF, 0x6E, 0xC7, 0xFF), false),
        new BossData("KING", 20, 26, 1.3f, 0.8f, false, 25f, 2,
            new Color32(0x1E, 0x16, 0x08, 0xFF), new Color32(0x7A, 0x5A, 0x16, 0xFF), 0.25f, 0.5f,
            A(Volley, 4, 16f), A(Rain, 5, 24f), A(Dash, 3, 37f), A(Jokers, 2, 21f), A(Volley, 6, 17f), A(Rain, 7, 25f)).Combo(0.5f).Dice(1.15f, new Color32(0xFF, 0x30, 0x30, 0xFF), true),
    };

    [Header("Tutorial (miniboss Coringa)")]
    [SerializeField] private BossData tutorialBoss = new BossData("JOKER", 16, 8, 3.2f, 1.5f, false, 0f, 0,
        new Color32(0x12, 0x1A, 0x22, 0xFF), new Color32(0x1F, 0x4A, 0x5C, 0xFF), 0f, 0f,
        A(Volley, 1, 8f), A(Volley, 1, 8f), A(Volley, 2, 8f), A(Volley, 2, 8f), A(Volley, 2, 9f), A(Volley, 3, 9f));

    [Header("Áudio (arraste os clipes; vazio = sem som)")]
    [SerializeField] private AudioClip menuMusicIntro;   // toca 1x, depois emenda no loop
    [SerializeField] private AudioClip menuMusic;        // loop (menu e tutorial)
    [SerializeField] private AudioClip gameMusic;        // loop reserva (chefe sem música própria)
    [SerializeField] private AudioClip sfxThrow;
    [SerializeField] private AudioClip sfxHit;          // acerto com dano
    [SerializeField] private AudioClip sfxMiss;         // acerto sem dano (longe do número)
    [SerializeField] private AudioClip sfxCritical;
    [SerializeField] private AudioClip sfxHeal;         // carta de Copas
    [SerializeField] private AudioClip sfxPlayerHurt;
    [SerializeField] private AudioClip sfxBossDefeated;
    [SerializeField] private AudioClip sfxPowerUp;      // pegou um orbe (tempo para)
    [SerializeField] private AudioClip sfxFusion;       // fusão deu certo
    [SerializeField] private AudioClip sfxUiClick;
    [SerializeField] private AudioClip sfxUiHover;
    [SerializeField] private AudioClip sfxCardSelect;   // trocar a carta selecionada
    [SerializeField] private AudioClip sfxCardDraw;     // ganhar cartas (pacote, ouros)
    [SerializeField] private AudioClip sfxNewHand;      // mão nova (início do chefe, redraw)
    [SerializeField] private AudioClip sfxDiceShake;    // antecipação do chefe / troca de número
    [SerializeField] private AudioClip sfxVictory;
    [Header("Áudio — sons novos (13/11)")]
    [SerializeField] private AudioClip sfxJump;          // pulo (e descer da plataforma, mais grave)
    [SerializeField] private AudioClip sfxDash;          // dash (whoosh)
    [SerializeField] private AudioClip sfxPlayerDeath;   // player morreu
    [SerializeField] private AudioClip sfxDiceLand;      // dado do chefe parou no ataque escolhido
    [SerializeField] private AudioClip sfxNewNumber;     // chefe troca o número ("NEW NUMBER!")
    [SerializeField] private AudioClip sfxChipBreak;     // ficha quebrando
    [SerializeField] private AudioClip sfxJoker;         // coringa atravessando a tela
    // "Match" pela proximidade: [0] = lower (3–4), [1] = mid (2), [2] = max (1). Ver GameManager.HandleBossHit.
    [UnityEngine.Serialization.FormerlySerializedAs("sfxFireDice")]
    [SerializeField] private AudioClip[] sfxCloseness;
    [SerializeField] private AudioClip sfxCurtainCard;   // cartas da transição pousando
    [SerializeField] private AudioClip sfxLand;          // player pousou no chão/plataforma
    [SerializeField] private AudioClip sfxMixSelect;     // pôr carta no slot Q/E da fusão
    [SerializeField] private AudioClip sfxMixResult;     // cartas se juntando na fusão
    [SerializeField] private AudioClip sfxBossDashImpact; // fim da investida do chefe (batida forte)
    [SerializeField] private AudioClip sfxChipFall;      // ficha caindo (para quando ela quebra)
    [SerializeField] private AudioClip sfxBossEntry;     // chefe entrando na arena
    [SerializeField] private float[] bossEntryPitch = { 0.95f, 1.05f, 1.15f }; // por chefe: cada vez mais tenso

    public AudioClip SfxBossDashImpact => sfxBossDashImpact;
    public AudioClip SfxChipFall => sfxChipFall;
    public AudioClip SfxBossEntry => sfxBossEntry;
    public float BossEntryPitch(int index) =>
        bossEntryPitch != null && index >= 0 && index < bossEntryPitch.Length ? bossEntryPitch[index] : 0.9f;

    [Header("Áudio — equalização (menu Draw the Odds/Balance Audio Levels)")]
    [SerializeField] private List<ClipGain> sfxGains = new List<ClipGain>();
    [SerializeField] private float sfxMakeupDb;          // reforço no grupo SFX/UI do mixer (os altos são abaixados pelo gain)

    public List<ClipGain> SfxGains => sfxGains;
    public float SfxMakeupDb => sfxMakeupDb;
    public AudioClip SfxLand => sfxLand;
    public AudioClip SfxMixSelect => sfxMixSelect;
    public AudioClip SfxMixResult => sfxMixResult;

    public AudioClip SfxJump => sfxJump;
    public AudioClip SfxDash => sfxDash;
    public AudioClip SfxPlayerDeath => sfxPlayerDeath;
    public AudioClip SfxDiceLand => sfxDiceLand;
    public AudioClip SfxNewNumber => sfxNewNumber;
    public AudioClip SfxChipBreak => sfxChipBreak;
    public AudioClip SfxJoker => sfxJoker;
    public AudioClip[] SfxCloseness => sfxCloseness;
    public AudioClip SfxCurtainCard => sfxCurtainCard;

    public AudioClip MenuMusicIntro => menuMusicIntro;
    public AudioClip MenuMusic => menuMusic;
    public AudioClip SfxUiHover => sfxUiHover;
    public AudioClip SfxCardSelect => sfxCardSelect;
    public AudioClip SfxCardDraw => sfxCardDraw;
    public AudioClip SfxNewHand => sfxNewHand;
    public AudioClip SfxDiceShake => sfxDiceShake;
    public AudioClip SfxVictory => sfxVictory;
    public AudioClip GameMusic => gameMusic;
    public AudioClip SfxThrow => sfxThrow;
    public AudioClip SfxHit => sfxHit;
    public AudioClip SfxMiss => sfxMiss;
    public AudioClip SfxCritical => sfxCritical;
    public AudioClip SfxHeal => sfxHeal;
    public AudioClip SfxPlayerHurt => sfxPlayerHurt;
    public AudioClip SfxBossDefeated => sfxBossDefeated;
    public AudioClip SfxPowerUp => sfxPowerUp;
    public AudioClip SfxFusion => sfxFusion;
    public AudioClip SfxUiClick => sfxUiClick;

    public Sprite CardBack => cardBack;
    public TMPro.TMP_FontAsset TitleFont => titleFont;
    public Sprite Square => square;
    public Sprite Felt => felt;
    public Sprite Background => backgrounds != null && backgrounds.Length > 0
        ? backgrounds[Mathf.Clamp(backgroundIndex, 0, backgrounds.Length - 1)] : null;
    public int BackgroundCount => backgrounds != null ? backgrounds.Length : 0;
    public int BackgroundIndex { get => backgroundIndex; set => backgroundIndex = value; }
    public float BackgroundTint => backgroundTint;
    public float BackgroundBrightness => backgroundBrightness;
    public IReadOnlyList<Sprite> Props => props;
    public Sprite RandomChip() => chips != null && chips.Length > 0 ? chips[Random.Range(0, chips.Length)] : null;
    public Sprite RandomJoker() => jokers != null && jokers.Length > 0 ? jokers[Random.Range(0, jokers.Length)] : null;
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
    public float CardHomingConeDegrees => cardHomingConeDegrees;
    public int LowHandThreshold => lowHandThreshold;
    public int MinPackSize => minPackSize;
    public int MaxPackSize => maxPackSize;
    public float SpecificSuitChance => specificSuitChance;
    public Vector2 PowerSpawnInterval => powerSpawnInterval;
    public int MaxPowersOnMap => maxPowersOnMap;
    public float FusionWindow => fusionWindow;
    public bool AllowMixedSuits => allowMixedSuits;
    public int PlayerMaxHp => playerMaxHp;
    public float PlayerMoveSpeed => playerMoveSpeed;
    public float PlayerJumpVelocity => playerJumpVelocity;
    public float PlayerGravityScale => playerGravityScale;
    public float PlayerMaxFallSpeed => playerMaxFallSpeed;
    public Vector2 PowerLifetime => powerLifetime;
    public float PlayerDashCooldown => playerDashCooldown;
    public DifficultySettings GetDifficulty(Difficulty d) =>
        difficulties != null && difficulties.Length > (int)d ? difficulties[(int)d] : new DifficultySettings();

    // Naipe sorteado com os pesos acima (cartas da mão, pacotes, ouros).
    public Suit RandomCardSuit()
    {
        float c = clubsWeight, s = spadesWeight, h = heartsWeight, d = diamondsWeight;
        float total = c + s + h + d;
        if (total <= 0f) return SuitInfo.Random();
        float r = Random.value * total;
        if ((r -= c) < 0f) return Suit.Clubs;
        if ((r -= s) < 0f) return Suit.Spades;
        if ((r -= h) < 0f) return Suit.Hearts;
        return Suit.Diamonds;
    }

    // Músicas de gameplay disponíveis (sem repetir), na ordem dos chefes: usadas na opção de trilha.
    public List<(AudioClip intro, AudioClip loop)> GameplayTracks()
    {
        var list = new List<(AudioClip, AudioClip)>();
        foreach (var b in bosses)
        {
            if (b.MusicLoop == null) continue;
            bool seen = false;
            foreach (var t in list) if (t.Item2 == b.MusicLoop) seen = true;
            if (!seen) list.Add((b.MusicIntro, b.MusicLoop));
        }
        return list;
    }
    public IReadOnlyList<BossData> Bosses => bosses;
    public BossData TutorialBoss => tutorialBoss;
}
