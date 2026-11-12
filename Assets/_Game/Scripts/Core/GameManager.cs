using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Cérebro do jogo: fluxo (menu principal → tutorial → chefes 1..3 → vitória/derrota), pausa, mão, poderes e UI.
public class GameManager : MonoBehaviour
{
    public enum GameState { Title, Transition, Playing, GameOver, Victory }

    // Estatísticas da run (vão para o ranking). Sobrevivem ao "tentar de novo" (recarregar a cena).
    public class RunStats
    {
        public int cardsThrown, criticals, fusions, damageTaken, deaths;
        public float startTime;       // Time.time quando o trecho atual começou
        public float elapsedBefore;   // tempo acumulado das tentativas anteriores
        public float Elapsed => elapsedBefore + (Time.time - startTime);
    }

    public static GameManager Instance { get; private set; }

    // Sobrevive ao recarregar a cena: "tentar de novo" volta no mesmo chefe, sem tela de título.
    private static int retryBossIndex;
    private static bool skipTitle;
    private static int retryRedraws = -1;
    private static RunStats carriedStats;    // stats da run guardadas ao morrer (o retry continua a mesma run)
    private bool resumedRun;   // redraws que o player tinha ao começar o chefe (volta no retry)
    private int redrawsAtBossStart;

    // Opção de trilha (menu de pausa): 0 = automática (cada chefe com a sua), 1..n = sempre aquela música.
    private const string MusicTrackKey = "musicTrack";
    public static int MusicTrackChoice
    {
        get => PlayerPrefs.GetInt(MusicTrackKey, 0);
        set { PlayerPrefs.SetInt(MusicTrackKey, value); PlayerPrefs.Save(); }
    }

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
    public int BossIndex { get; private set; }        // -1 = tutorial
    public bool IsTutorial => BossIndex < 0;
    public int RedrawsLeft { get; private set; }
    public GameState State { get; private set; }
    public RunStats Stats { get; private set; } = new RunStats();
    public BossData CurrentBossData { get; private set; }
    public Suit CurrentBossSuit { get; private set; }
    public PickupSpawner Pickups { get; private set; }

    // O tutorial trava o input do jogador durante as pausas explicativas.
    public bool InputLocked { get; set; }
    public bool CanAct => State == GameState.Playing && !PlayerHealth.IsDead && !InputLocked && !GameTime.Paused;

    public event Action OnRedrawsChanged;
    public event Action<BossController> OnBossSpawned;
    public event Action<HitResult> OnBossHit;

    private LevelBuilder level;
    private GameUI ui;
    private TutorialDirector tutorial;
    private float acceptClickTime;

    private void Awake()
    {
        Instance = this;
        GameTime.Reset(); // ao recarregar a cena, garante que nada ficou pausado/lento

        Hand = new Hand(config.MaxHandSize);
        Fusion = new FusionController(Hand, config);

        // Física do player vem do GameConfig (velocidade, pulo, gravidade) para ajustar num lugar só.
        player.ApplyTuning(config.PlayerMoveSpeed, config.PlayerJumpVelocity, config.PlayerGravityScale, config.PlayerMaxFallSpeed);

        PlayerHealth = player.GetComponent<PlayerHealth>();
        if (PlayerHealth == null) PlayerHealth = player.gameObject.AddComponent<PlayerHealth>();
        if (player.GetComponent<PlayerCards>() == null) player.gameObject.AddComponent<PlayerCards>();

        level = new LevelBuilder(platformTemplate, floor, config);
        Pickups = gameObject.AddComponent<PickupSpawner>();
        Pickups.Init(this, level);
        ui = GameUI.Build(this);
    }

    private void Start()
    {
        PlayerHealth.SetMax(config.PlayerMaxHp, true);
        PlayerHealth.OnDied += HandlePlayerDied;
        PlayerHealth.OnDamaged += amount => Stats.damageTaken += amount;
        Fusion.OnEnded += success => { if (success) { Stats.fusions++; AudioManager.Sfx(config.SfxFusion); } };
        Fusion.OnStarted += () => AudioManager.Sfx(config.SfxPowerUp);
        PlayerHealth.OnDamaged += _ => AudioManager.Sfx(config.SfxPlayerHurt);
        Stats.startTime = Time.time;

        if (skipTitle)
        {
            if (carriedStats != null) { Stats = carriedStats; Stats.startTime = Time.time; resumedRun = true; }
            carriedStats = null;
            StartCoroutine(BeginBoss(retryBossIndex));
        }
        else
        {
            State = GameState.Title;
            player.ControlsEnabled = false;
            PlayerHealth.forcedInvulnerable = true;
            CurrentBossData = config.Bosses[0];
            ApplyLevel(CurrentBossData);
            ui.ShowTitle(true);
            AudioManager.Music(config.MenuMusicIntro, config.MenuMusic);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!GameTime.Paused) Fusion.Tick(); // o relógio da fusão para junto com a pausa
        player.InputFrozen = Fusion.Active || InputLocked || GameTime.Paused; // tempo parado = personagem parado

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
        bool enter = keyboard != null && keyboard.enterKey.wasPressedThisFrame;
        bool space = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        bool pauseKey = keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame);

        switch (State)
        {
            case GameState.Playing:
            case GameState.Transition:
                if (pauseKey) TogglePause();
                break;
            case GameState.GameOver:
                if ((click || enter || space) && Time.unscaledTime >= acceptClickTime) Reload();
                break;
            // Vitória: a tela do ranking tem botões (e campo de nome), então clique solto não recarrega.
        }
    }

    // ===================== Menus e pausa =====================
    // Chamado pelos botões do menu principal.
    public void StartGame(bool withTutorial)
    {
        if (State != GameState.Title) return;
        StartCoroutine(withTutorial ? BeginTutorial() : BeginBoss(0));
    }

    // ESC/P: abre ou fecha a pausa. Com as opções abertas, ESC volta para o menu de pausa.
    private void TogglePause()
    {
        if (!GameTime.Paused) { GameTime.SetPaused(true); ui.Pause.Open(); }
        else if (ui.Pause.OptionsOpen) ui.Pause.ShowOptions(false);
        else ResumeGame();
    }

    public void ResumeGame()
    {
        ui.Pause.Close();
        GameTime.SetPaused(false);
    }

    public void SkipTutorialFromPause()
    {
        if (!IsTutorial || State != GameState.Playing) return;
        ResumeGame();
        SkipTutorial();
    }

    public void BackToMainMenu()
    {
        retryRedraws = -1;
        carriedStats = null;
        retryBossIndex = 0;
        skipTitle = false;
        Reload();
    }

    // ===================== Fluxo =====================
    private void ApplyLevel(BossData data)
    {
        level.Build(data.LayoutIndex, data.PlatformColor);
        if (Camera.main != null) Camera.main.backgroundColor = data.BackgroundColor;
    }

    // Prepara a fase (limpa tudo, reposiciona o player, mão inicial). Usado por chefes e tutorial.
    private IEnumerator PrepareFight(BossData data, int index, IEnumerable<Card> startingHand = null)
    {
        State = GameState.Transition;
        BossIndex = index;
        CurrentBossData = data;
        ui.ShowTitle(false);

        Fusion.Cancel();
        Pickups.StopAndClear();
        ClearProjectiles();
        if (Boss != null) Destroy(Boss.gameObject);
        ApplyLevel(data);

        player.ResetTo(new Vector2(0f, LevelBuilder.FloorTop + 0.05f));
        player.ControlsEnabled = true;
        PlayerHealth.forcedInvulnerable = true;
        PlayerHealth.cannotDie = index < 0;
        PlayerHealth.SetMax(config.PlayerMaxHp, true);

        // Redraws valem para a RUN inteira: só enchem no começo (tutorial ou 1º chefe).
        if (index <= 0) RedrawsLeft = config.RedrawsPerBoss;
        if (retryRedraws >= 0) { RedrawsLeft = retryRedraws; retryRedraws = -1; } // "tentar de novo" devolve os do início do chefe
        redrawsAtBossStart = RedrawsLeft;
        OnRedrawsChanged?.Invoke();

        if (startingHand != null)
        {
            Hand.RemoveAll();
            foreach (var c in startingHand) { Hand.Add(c); yield return new WaitForSeconds(0.08f); }
        }
        else
        {
            AudioManager.Sfx(config.SfxNewHand);
            yield return DealUpTo(config.StartingHandSize);
        }
    }

    private IEnumerator BeginBoss(int index)
    {
        var data = config.Bosses[index];
        // Começo de uma run nova (1º chefe, sem ser retry): zera as estatísticas (o tutorial não conta).
        if (index == 0 && !resumedRun) Stats = new RunStats { startTime = Time.time };
        resumedRun = false;
        CurrentBossSuit = SuitInfo.Random(); // cada vez que um chefe surge, o naipe é sorteado
        ui.ShowBanner($"BOSS {index + 1}/{config.Bosses.Count}", BossTitle(data), 2.2f);
        PlayBossMusic(data);
        yield return PrepareFight(data, index);
        yield return new WaitForSeconds(0.6f);

        SpawnBoss(data);
        yield return new WaitForSeconds(1.4f);
        PlayerHealth.forcedInvulnerable = false;
        State = GameState.Playing;
        Pickups.PowersEnabled = Pickups.CardsEnabled = true;
        Pickups.Begin();
    }

    // Tutorial: mão com números baixos (o Ás esconde 1–8) e o TutorialDirector conduz o resto.
    private IEnumerator BeginTutorial()
    {
        ui.ShowBanner("TUTORIAL", config.TutorialBoss.DisplayName, 1.6f);
        AudioManager.Music(config.MenuMusicIntro, config.MenuMusic); // tutorial continua com a música do menu
        var hand = new[]
        {
            new Card(Suit.Clubs, 2), new Card(Suit.Spades, 7), new Card(Suit.Hearts, 4),
            new Card(Suit.Diamonds, 5), new Card(Suit.Clubs, 1), new Card(Suit.Spades, 3), new Card(Suit.Hearts, 8),
        };
        yield return PrepareFight(config.TutorialBoss, -1, hand);
        PlayerHealth.forcedInvulnerable = false;
        State = GameState.Playing;
        Pickups.PowersEnabled = Pickups.CardsEnabled = false;
        Pickups.Begin();

        tutorial = gameObject.AddComponent<TutorialDirector>();
        tutorial.Run(this, ui);
    }

    private void SkipTutorial()
    {
        if (tutorial != null) Destroy(tutorial);
        ui.HidePrompt();
        InputLocked = false;
        FusionController.SetTimeScale(1f);
        StartCoroutine(BeginBoss(0));
    }

    // Música do chefe, respeitando a opção de trilha do menu de pausa.
    private void PlayBossMusic(BossData data)
    {
        var tracks = config.GameplayTracks();
        int choice = MusicTrackChoice;
        if (choice > 0 && choice <= tracks.Count) AudioManager.Music(tracks[choice - 1].intro, tracks[choice - 1].loop);
        else if (data.MusicLoop != null) AudioManager.Music(data.MusicIntro, data.MusicLoop);
        else AudioManager.Music(config.GameMusic);
    }

    // Chamado pelas opções ao trocar a trilha: aplica na hora se estiver lutando com um chefe.
    public void ApplyMusicChoice()
    {
        if (IsTutorial || State == GameState.Title || State == GameState.Victory) return;
        PlayBossMusic(CurrentBossData);
    }

    // Nome do chefe atual com o naipe (ex.: "JACK ♠").
    private string BossTitle(BossData data) =>
        data.HasSuits ? $"{data.DisplayName} {SuitInfo.Colored(SuitInfo.Symbol(CurrentBossSuit), CurrentBossSuit)}" : data.DisplayName;

    public BossController SpawnBoss(BossData data)
    {
        Boss = BossController.Spawn(config, data, CurrentBossSuit, PlayerHealth, LevelBuilder.FloorTop);
        Boss.OnHit += HandleBossHit;
        Boss.OnDefeated += HandleBossDefeated;
        OnBossSpawned?.Invoke(Boss);
        return Boss;
    }

    private void HandleBossHit(HitResult r)
    {
        if (r.isHeal) PlayerHealth.Heal(r.amount);
        if (r.isCritical) AudioManager.Sfx(config.SfxCritical, 1f, 0f);
        else if (r.isHeal) AudioManager.Sfx(config.SfxHeal);
        else if (r.amount > 0) AudioManager.Sfx(config.SfxHit);
        else AudioManager.Sfx(config.SfxMiss, 0.45f, 0.15f); // acertou longe do número: som fraco
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
        AudioManager.Sfx(config.SfxBossDefeated, 1f, 0f);
        Fusion.Cancel();
        Pickups.StopAndClear();
        StartCoroutine(AfterBossDefeated());
    }

    private IEnumerator AfterBossDefeated()
    {
        yield return new WaitForSeconds(0.6f);
        ClearProjectiles();

        if (IsTutorial)
        {
            if (tutorial != null) Destroy(tutorial);
            ui.HidePrompt();
            InputLocked = false;
            ui.ShowBanner("TUTORIAL COMPLETE", "GOOD LUCK!", 2f);
            yield return new WaitForSeconds(2.4f);
            StartCoroutine(BeginBoss(0));
            yield break;
        }

        ui.ShowBanner("DEFEATED", BossTitle(CurrentBossData) + " FOLDED!", 2f);
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
            AudioManager.Sfx(config.SfxVictory, 1f, 0f);
            AudioManager.Music(config.MenuMusicIntro, config.MenuMusic, 3f);
            ui.ShowVictory(Stats, Stats.Elapsed);
        }
    }

    private void HandlePlayerDied()
    {
        State = GameState.GameOver;
        Fusion.Cancel();
        retryBossIndex = Mathf.Max(0, BossIndex);
        retryRedraws = IsTutorial ? -1 : redrawsAtBossStart;
        Stats.deaths++;
        Stats.elapsedBefore = Stats.Elapsed;
        carriedStats = IsTutorial ? null : Stats;
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
        GameTime.Reset();
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
        if (added > 0) AudioManager.Sfx(config.SfxCardDraw);
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
    public event Action OnRedraw;

    public void Redraw()
    {
        if (!CanAct || Fusion.Active || RedrawsLeft <= 0) return;
        RedrawsLeft--;
        Hand.RemoveAll();
        AudioManager.Sfx(config.SfxNewHand);
        StartCoroutine(DealUpTo(config.StartingHandSize));
        OnRedrawsChanged?.Invoke();
        OnRedraw?.Invoke();
        Fx.Popup("REDRAW!", player.transform.position + Vector3.up * 2.5f, Color.white, 6f, 0.8f);
    }

    // ===================== Debug (chamado pela ponte do Editor) =====================
    public static string DebugSpawnPower(string type)
    {
        if (Instance == null) return "no game";
        Instance.Pickups.SpawnPower(Enum.TryParse(type, out PowerType t) ? t : PowerType.Add);
        return "spawned";
    }

    public static string DebugStart()
    {
        if (Instance == null || Instance.State != GameState.Title) return "not in title";
        Instance.StartCoroutine(Instance.BeginBoss(0));
        return "started";
    }

    public static string DebugTutorial()
    {
        if (Instance == null || Instance.State != GameState.Title) return "not in title";
        Instance.StartCoroutine(Instance.BeginTutorial());
        return "tutorial";
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

    // "toggle" | "options" | "resume" | "menu" (simula o menu de pausa sem teclado)
    public static string DebugPause(string arg)
    {
        var gm = Instance;
        if (gm == null) return "no game";
        switch (arg)
        {
            case "options": gm.ui.Pause.ShowOptions(true); break;
            case "resume": gm.ResumeGame(); break;
            case "menu": gm.BackToMainMenu(); break;
            default: gm.TogglePause(); break;
        }
        return $"paused={GameTime.Paused} ts={Time.timeScale}";
    }

    public static string DebugRain(string count)
    {
        if (Instance == null || Instance.Boss == null) return "no boss";
        Instance.Boss.DebugRain(int.Parse(count));
        return "rain";
    }

    public static string DebugJokers(string count)
    {
        if (Instance == null || Instance.Boss == null) return "no boss";
        Instance.Boss.DebugJokers(int.Parse(count));
        return "jokers";
    }

    // Mostra a tela de vitória com números de exemplo (testa o ranking sem jogar tudo).
    public static string DebugVictory()
    {
        var gm = Instance;
        if (gm == null) return "no game";
        gm.Stats = new RunStats { cardsThrown = 64, criticals = 7, fusions = 5, damageTaken = 9, deaths = 1, startTime = Time.time, elapsedBefore = 333f };
        gm.State = GameState.Victory;
        gm.ui.ShowTitle(false);
        gm.ui.ShowVictory(gm.Stats, gm.Stats.Elapsed);
        return "victory";
    }

    public static string DebugState()
    {
        var gm = Instance;
        if (gm == null) return "no game";
        var p = gm.player;
        return $"state={gm.State} locked={gm.InputLocked} frozen={p.InputFrozen} controls={p.ControlsEnabled} " +
               $"fusion={gm.Fusion.Active} ts={Time.timeScale} pos={p.transform.position} move={p.MoveInput} " +
               $"kb={UnityEngine.InputSystem.Keyboard.current?.aKey.isPressed}";
    }

    public static string DebugBoss(string index)
    {
        if (Instance == null) return "no game";
        Instance.StopAllCoroutines();
        Instance.StartCoroutine(Instance.BeginBoss(int.Parse(index)));
        return "ok";
    }
}
