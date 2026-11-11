using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Cérebro do jogo: fluxo (título → chefe 1..3 → vitória/derrota), mão, poderes e UI.
public class GameManager : MonoBehaviour
{
    public enum GameState { Title, Transition, Playing, GameOver, Victory }

    public class RunStats
    {
        public int cardsThrown, criticals, fusions, damageTaken;
        public float startTime;
    }

    public static GameManager Instance { get; private set; }

    // Sobrevive ao recarregar a cena: "tentar de novo" volta no mesmo chefe, sem tela de título.
    private static int retryBossIndex;
    private static bool skipTitle;

    [SerializeField] private GameConfig config;
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform platformTemplate;
    [SerializeField] private SpriteRenderer floor;

    public GameConfig Config => config;
    public PlayerController Player => player;
    public PlayerHealth PlayerHealth { get; private set; }
    public Hand Hand { get; private set; }
    public FusionController Fusion { get; private set; }
    public BossController Boss { get; private set; }
    public int BossIndex { get; private set; }
    public int RedrawsLeft { get; private set; }
    public GameState State { get; private set; }
    public RunStats Stats { get; } = new RunStats();
    public bool CanAct => State == GameState.Playing && !PlayerHealth.IsDead;
    public BossData CurrentBossData => config.Bosses[BossIndex];

    public event Action OnRedrawsChanged;
    public event Action<BossController> OnBossSpawned;
    public event Action<HitResult> OnBossHit;

    private LevelBuilder level;
    private PickupSpawner pickups;
    private GameUI ui;
    private float acceptClickTime;

    private void Awake()
    {
        Instance = this;
        FusionController.SetTimeScale(1f);

        Hand = new Hand(config.MaxHandSize);
        Fusion = new FusionController(Hand, config);

        PlayerHealth = player.GetComponent<PlayerHealth>();
        if (PlayerHealth == null) PlayerHealth = player.gameObject.AddComponent<PlayerHealth>();
        if (player.GetComponent<PlayerCards>() == null) player.gameObject.AddComponent<PlayerCards>();

        level = new LevelBuilder(platformTemplate, floor);
        pickups = gameObject.AddComponent<PickupSpawner>();
        pickups.Init(this, level);
        ui = GameUI.Build(this);
    }

    private void Start()
    {
        PlayerHealth.SetMax(config.PlayerMaxHp, true);
        PlayerHealth.OnDied += HandlePlayerDied;
        PlayerHealth.OnDamaged += amount => Stats.damageTaken += amount;
        Fusion.OnEnded += success => { if (success) Stats.fusions++; };
        Stats.startTime = Time.time;

        if (skipTitle)
        {
            StartCoroutine(BeginBoss(retryBossIndex));
        }
        else
        {
            State = GameState.Title;
            player.ControlsEnabled = false;
            PlayerHealth.forcedInvulnerable = true;
            ApplyLevel(0);
            ui.ShowTitle(true);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        Fusion.Tick();

        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool key = Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);

        switch (State)
        {
            case GameState.Title:
                if (click || key)
                {
                    StartCoroutine(BeginBoss(0));
                }
                break;
            case GameState.GameOver:
            case GameState.Victory:
                if ((click || key) && Time.unscaledTime >= acceptClickTime) Reload();
                break;
        }
    }

    // ===================== Fluxo =====================
    private void ApplyLevel(int bossIndex)
    {
        var data = config.Bosses[bossIndex];
        level.Build(data.LayoutIndex, data.PlatformColor);
        if (Camera.main != null) Camera.main.backgroundColor = data.BackgroundColor;
    }

    private IEnumerator BeginBoss(int index)
    {
        State = GameState.Transition;
        BossIndex = index;
        var data = config.Bosses[index];
        ui.ShowTitle(false);

        Fusion.Cancel();
        pickups.StopAndClear();
        ClearProjectiles();
        ApplyLevel(index);

        player.ResetTo(new Vector2(0f, LevelBuilder.FloorTop + 0.05f));
        player.ControlsEnabled = true;
        PlayerHealth.forcedInvulnerable = true;
        PlayerHealth.SetMax(config.PlayerMaxHp, true);

        RedrawsLeft = config.RedrawsPerBoss;
        OnRedrawsChanged?.Invoke();

        ui.ShowBanner($"BOSS {index + 1}/{config.Bosses.Count}", data.DisplayName, 2.2f);
        yield return DealUpTo(config.StartingHandSize);
        yield return new WaitForSeconds(0.6f);

        Boss = BossController.Spawn(config, data, PlayerHealth, LevelBuilder.FloorTop);
        Boss.OnHit += HandleBossHit;
        Boss.OnDefeated += HandleBossDefeated;
        OnBossSpawned?.Invoke(Boss);

        yield return new WaitForSeconds(1.4f);
        PlayerHealth.forcedInvulnerable = false;
        State = GameState.Playing;
        pickups.Begin();
    }

    private void HandleBossHit(HitResult r)
    {
        if (r.isHeal) PlayerHealth.Heal(r.amount);
        if (r.isCritical) Stats.criticals++;
        if (r.bonusCards > 0)
        {
            var bonus = new List<Card>();
            for (int i = 0; i < r.bonusCards; i++) bonus.Add(Card.Random());
            GiveCards(bonus);
        }
        OnBossHit?.Invoke(r);
    }

    private void HandleBossDefeated()
    {
        State = GameState.Transition;
        PlayerHealth.forcedInvulnerable = true;
        Fusion.Cancel();
        pickups.StopAndClear();
        StartCoroutine(AfterBossDefeated());
    }

    private IEnumerator AfterBossDefeated()
    {
        yield return new WaitForSeconds(0.6f);
        ClearProjectiles();
        ui.ShowBanner("DEFEATED", CurrentBossData.DisplayName + " FOLDED!", 2f);
        yield return new WaitForSeconds(2.4f);

        if (BossIndex + 1 < config.Bosses.Count)
        {
            StartCoroutine(BeginBoss(BossIndex + 1));
        }
        else
        {
            State = GameState.Victory;
            player.ControlsEnabled = false;
            retryBossIndex = 0;
            skipTitle = false;
            acceptClickTime = Time.unscaledTime + 1.5f;
            ui.ShowVictory(Stats, Time.time - Stats.startTime);
        }
    }

    private void HandlePlayerDied()
    {
        State = GameState.GameOver;
        Fusion.Cancel();
        retryBossIndex = BossIndex;
        skipTitle = true;
        acceptClickTime = Time.unscaledTime + 1.5f;
        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        FusionController.SetTimeScale(0.3f);
        yield return new WaitForSecondsRealtime(1.2f);
        FusionController.SetTimeScale(1f);
        ui.ShowGameOver(CurrentBossData.DisplayName);
    }

    private void Reload()
    {
        FusionController.SetTimeScale(1f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private static void ClearProjectiles()
    {
        foreach (var d in FindObjectsByType<DiceProjectile>()) Destroy(d.gameObject);
        foreach (var c in FindObjectsByType<CardProjectile>()) Destroy(c.gameObject);
    }

    // ===================== Cartas =====================
    public int GiveCards(IEnumerable<Card> cards)
    {
        int added = 0;
        foreach (var c in cards)
        {
            if (!Hand.Add(c)) break;
            added++;
        }
        return added;
    }

    private IEnumerator DealUpTo(int count)
    {
        while (Hand.Count < count && !Hand.IsFull)
        {
            Hand.Add(Card.Random());
            yield return new WaitForSeconds(0.08f);
        }
    }

    // R: descarta a mão inteira e compra uma nova (limite por chefe).
    public void Redraw()
    {
        if (!CanAct || Fusion.Active || RedrawsLeft <= 0) return;
        RedrawsLeft--;
        Hand.RemoveAll();
        StartCoroutine(DealUpTo(config.StartingHandSize));
        OnRedrawsChanged?.Invoke();
        Fx.Popup("REDRAW!", player.transform.position + Vector3.up * 2.5f, Color.white, 6f, 0.8f);
    }

    // ===================== Debug (chamado pela ponte do Editor) =====================
    public static string DebugSpawnPower(string type)
    {
        if (Instance == null) return "no game";
        Instance.pickups.SpawnPower(Enum.TryParse(type, out PowerType t) ? t : PowerType.Add);
        return "spawned";
    }

    public static string DebugStart()
    {
        if (Instance == null || Instance.State != GameState.Title) return "not in title";
        Instance.StartCoroutine(Instance.BeginBoss(0));
        return "started";
    }

    public static string DebugFusion(string type)
    {
        if (Instance == null) return "no game";
        bool ok = Instance.Fusion.Begin(Enum.TryParse(type, out PowerType t) ? t : PowerType.Add);
        if (ok) { Instance.Fusion.PutInSlot(true); Instance.Fusion.PutInSlot(false); }
        return ok ? "fusion on" : "fail";
    }

    public static string DebugThrowAtBoss()
    {
        if (Instance == null || Instance.Boss == null || Instance.Hand.IsEmpty) return "no";
        var gm = Instance;
        Card card = gm.Hand.RemoveAt(gm.Hand.SelectedIndex);
        Vector2 origin = (Vector2)gm.player.transform.position + Vector2.up;
        CardProjectile.Spawn(gm.config, card, origin, (Vector2)gm.Boss.transform.position - origin, gm.Boss.transform);
        return "thrown " + card + " secret " + gm.Boss.SecretNumber;
    }

    public static string DebugConfirm() => Instance != null && Instance.Fusion.TryConfirm() ? "fused" : "fail";

    // Arremessa cartas com o número exato (testa crítico, troca de número e morte do chefe).
    public static string DebugCrit()
    {
        var gm = Instance;
        if (gm == null || gm.Boss == null) return "no";
        Vector2 origin = (Vector2)gm.player.transform.position + Vector2.up;
        var card = new Card(Suit.Spades, gm.Boss.SecretNumber);
        CardProjectile.Spawn(gm.config, card, origin, (Vector2)gm.Boss.transform.position - origin, gm.Boss.transform);
        return "crit " + card + " hp " + gm.Boss.CurrentHp;
    }

    public static string DebugKillPlayer()
    {
        if (Instance == null) return "no";
        Instance.PlayerHealth.forcedInvulnerable = false;
        Instance.PlayerHealth.Damage(99, Vector2.zero);
        return "dead";
    }

    public static string DebugGodMode()
    {
        if (Instance == null) return "no";
        Instance.PlayerHealth.forcedInvulnerable = !Instance.PlayerHealth.forcedInvulnerable;
        return "god " + Instance.PlayerHealth.forcedInvulnerable;
    }

    public static string DebugBoss(string index)
    {
        if (Instance == null) return "no game";
        if (Instance.Boss != null) Destroy(Instance.Boss.gameObject);
        Instance.StopAllCoroutines();
        Instance.StartCoroutine(Instance.BeginBoss(int.Parse(index)));
        return "ok";
    }
}
