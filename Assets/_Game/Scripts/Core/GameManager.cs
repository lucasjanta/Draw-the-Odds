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
        public int chipsEarned;       // fichas ganhas nesta run (vão para a taverna)
        public float rankedElapsed = -1f; // tempo da run ao vencer o Rei (o Ás secreto não conta no ranking)
        public float startTime;       // Time.time quando o trecho atual começou
        public float elapsedBefore;   // tempo acumulado das tentativas anteriores
        public float Elapsed => elapsedBefore + (Time.time - startTime);
    }

    public static GameManager Instance { get; private set; }

    // Sobrevive ao recarregar a cena: "tentar de novo" começa uma run nova no 1º chefe, sem tela de título
    // (e sem escolher a dificuldade de novo).
    private static bool skipTitle;

    // Dificuldade (botão ao lado do JOGAR ou tela depois do tutorial). Fica salva no navegador.
    private const string DifficultyKey = "dto_diff";
    public static Difficulty SelectedDifficulty
    {
        get => (Difficulty)PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Normal);
        set { PlayerPrefs.SetInt(DifficultyKey, (int)value); PlayerPrefs.Save(); }
    }

    // ===================== Modo infinito =====================
    // Liberado ao derrotar a Dama (2º chefe) pela primeira vez. static: vale no "recomeçar".
    private const string EndlessUnlockKey = "dto_endless";
    public static bool EndlessUnlocked
    {
        get => PlayerPrefs.GetInt(EndlessUnlockKey, 0) == 1;
        private set { PlayerPrefs.SetInt(EndlessUnlockKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool EndlessMode { get; private set; }
    public int EndlessRound { get; private set; }               // 1, 2, 3...
    public int EndlessBeaten => Mathf.Max(0, EndlessRound - 1); // chefes vencidos (pontuação)
    public EndlessRule CurrentRule { get; private set; }
    private static EndlessRule lastRule;

    // ===================== Continuar (anúncio recompensado) =====================
    // Ao morrer, o jogador pode ver um anúncio e recomeçar DESTE chefe (1 vez por chefe/rodada).
    // A cena é recarregada; estas informações atravessam o recarregamento.
    private class Carry
    {
        public bool endless;
        public int stage;           // índice do chefe (história) ou rodada (infinito)
        public EndlessRule rule;
        public int hp, redraws;
        public RunStats stats;
    }
    private static Carry carry;
    private static int continuedStage = int.MinValue;   // StageKey onde o "continuar" já foi usado
    private int StageKey => EndlessMode ? 1000 + EndlessRound : BossIndex;
    public bool CanContinue => Portal.RewardedAvailable && continuedStage != StageKey && !IsTutorial;
    private int stageStartHp, stageStartRedraws;           // como o chefe atual começou (o "continuar" volta assim)
    private int pendingHp = -1, pendingRedraws = -1;      // aplicados no PrepareFight depois de um "continuar"
    // Tutorial sempre no NORMAL (a escolha só acontece no fim dele).
    public DifficultySettings CurrentDifficulty => config.GetDifficulty(IsTutorial ? Difficulty.Normal : SelectedDifficulty);

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
    public RaiseController Raise { get; } = new RaiseController();
    public bool IsSecretBoss => !EndlessMode && BossIndex == config.SecretBossIndex;

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
        // Portal (CrazyGames etc.): inicia o SDK. Quando ele responder, aplica o idioma do jogador
        // (só se ele nunca escolheu um nas Opções).
        Portal.Init();
        Portal.OnReady -= ApplyPortalLanguage;
        Portal.OnReady += ApplyPortalLanguage;
        if (Portal.Ready) ApplyPortalLanguage();
        GameTime.Reset(); // ao recarregar a cena, garante que nada ficou pausado/lento
        AudioManager.SetLevels(config.SfxGains, config.SfxMakeupDb); // equalização dos sons

        Hand = new Hand(config.MaxHandSize);
        Fusion = new FusionController(Hand, config);

        // Física do player vem do GameConfig (velocidade, pulo, gravidade) para ajustar num lugar só.
        player.ApplyTuning(config.PlayerMoveSpeed, config.PlayerJumpVelocity, config.PlayerGravityScale, config.PlayerMaxFallSpeed, config.PlayerDashCooldown);

        PlayerHealth = player.GetComponent<PlayerHealth>();
        if (PlayerHealth == null) PlayerHealth = player.gameObject.AddComponent<PlayerHealth>();
        if (player.GetComponent<PlayerCards>() == null) player.gameObject.AddComponent<PlayerCards>();

        level = new LevelBuilder(platformTemplate, floor, config);
        Arena.OnChanged += level.Refit; // tela mudou de largura: paredes e chão vão para a nova borda
        Pickups = gameObject.AddComponent<PickupSpawner>();
        Pickups.Init(this, level);
        ui = GameUI.Build(this);
    }

    private void Start()
    {
        PlayerHealth.SetMax(config.PlayerMaxHp, true);
        PlayerHealth.OnDied += HandlePlayerDied;
        PlayerHealth.OnDamaged += amount => Stats.damageTaken += amount;
        Fusion.OnEnded += success => { if (success) Stats.fusions++; }; // o som da carta nova toca no FusionPanel (na revelação)
        Fusion.OnStarted += () => AudioManager.Sfx(config.SfxPowerUp);
        PlayerHealth.OnDamaged += _ => AudioManager.Sfx(config.SfxPlayerHurt);
        PlayerHealth.OnDied += () => AudioManager.Sfx(config.SfxPlayerDeath, 1f, 0f);
        // Sons do movimento do player (eventos que o PlayerController já dispara)
        player.OnJumped += () => AudioManager.Sfx(config.SfxJump, 0.7f, 0.08f);
        player.OnDroppedThrough += () => AudioManager.Sfx(config.SfxJump, 0.45f, 0.05f, 0.75f); // mesmo som, mais grave
        player.OnDashed += () => AudioManager.Sfx(config.SfxDash, 0.8f, 0.08f);
        player.OnLanded += () => AudioManager.Sfx(config.SfxLand, 0.6f, 0.08f);
        // Fusão: o "mix" toca quando as cartas começam a se juntar (o card_result continua no fim)
        Fusion.OnResolved += (a, b, result) => AudioManager.Sfx(config.SfxMixResult, 0.9f, 0f);
        Stats.startTime = Time.time;

        // A janela da fusão e o preview do resultado dependem da dificuldade.
        // Coringa Relógio Parado: +5 s (se houver relógio) e mostra o resultado.
        Fusion.WindowProvider = () => CurrentDifficulty.FusionWindow > 0f ? CurrentDifficulty.FusionWindow + Jokers.ExtraFusionSeconds : 0f;
        Fusion.PreviewProvider = () => CurrentDifficulty.FusionPreview || Jokers.FusionPreview;

        // Boatos: acompanha a luta atual (dano levado, críticos, 1º acerto, RAISE ganho).
        PlayerHealth.OnDamaged += amount => Rumors.Track.damageTaken += amount;
        OnBossHit += r =>
        {
            var t = Rumors.Track;
            if (t.hits == 0 && r.isCritical) t.firstHitCritical = true;
            t.hits++;
            if (r.isCritical) t.crits++;
            if (r.raiseWon) t.raiseWon = true;
        };

        // Dicas curtas (só fora do tutorial e uma vez cada)
        Fusion.OnStarted += () => ui.Tips.Request("fusion");
        Hand.OnCardRemoved += (_, __) => { if (State == GameState.Playing && Hand.Count <= 2 && RedrawsLeft > 0) ui.Tips.Request("redraw"); };

        if (carry != null)
        {
            // Viu o anúncio: volta para o mesmo chefe/rodada, com a vida e os redraws do começo dele.
            var c = carry;
            carry = null;
            skipTitle = false;
            Stats = c.stats;
            Stats.startTime = Time.time;
            pendingHp = c.hp;
            pendingRedraws = c.redraws;
            if (c.endless) { lastRule = c.rule; StartCoroutine(BeginEndlessRound(c.stage, true, c.rule)); }
            else StartCoroutine(BeginBoss(c.stage, true));
        }
        else if (skipTitle)
        {
            skipTitle = false;
            StartCoroutine(EndlessMode ? BeginEndlessRound(1) : BeginBoss(0));
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

    private static void ApplyPortalLanguage()
    {
        var lang = Portal.PreferredLanguage;
        if (lang.HasValue) Loc.ApplyDefault(lang.Value);
        VirtualInput.NotifyChanged(); // o portal pode ter dito que é celular (liga os controles de toque)
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (level != null) Arena.OnChanged -= level.Refit;
    }

    private void Update()
    {
        // Portal (GamePix etc.) pausou: anúncio na tela ou aba escondida. Nada anda e nenhuma tecla vale.
        bool portalPause = Portal.PausedByPortal;
        if (!GameTime.Paused && !portalPause) Fusion.Tick(); // o relógio da fusão para junto com a pausa
        player.InputFrozen = Fusion.Active || InputLocked || GameTime.Paused || portalPause; // tempo parado = personagem parado
        if (portalPause || leaving) return;

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
        // Enquanto o jogador escolhe uma tecla nova (Opções → CONTROLS), o teclado é só da tela de teclas.
        bool keysFree = keyboard != null && !KeyBindings.Busy;
        bool enter = keysFree && keyboard.enterKey.wasPressedThisFrame;
        bool space = keysFree && keyboard.spaceKey.wasPressedThisFrame;
        bool pauseKey = keysFree && (keyboard.escapeKey.wasPressedThisFrame || KeyBindings.Pressed(GameKey.Pause));

        if (DevKeys && keyboard != null) HandleDevKeys(keyboard);

        switch (State)
        {
            case GameState.Playing:
            case GameState.Transition:
                if (pauseKey) TogglePause();
                break;
            case GameState.GameOver:
                // Os botões da tela (CONTINUAR / RECOMEÇAR / MENU) fazem tudo; Enter/Espaço = recomeçar.
                if ((enter || space) && Time.unscaledTime >= acceptClickTime && !ui.GameOverBoardOpen) RestartRun();
                break;
            // Vitória: a tela do ranking tem botões (e campo de nome), então clique solto não recarrega.
        }
    }

    // ===================== Menus e pausa =====================
    // Chamado pelos botões do menu principal.
    public void StartGame(bool withTutorial)
    {
        if (State != GameState.Title) return;
        EndlessMode = false;
        if (withTutorial) StartCoroutine(BeginTutorial());
        else StartCoroutine(ChooseDifficultyThenStart(false));
    }

    // JOGAR: direto no Valete, com a dificuldade escolhida no menu e dicas curtas na tela.
    public void StartRun()
    {
        if (State != GameState.Title) return;
        EndlessMode = false;
        StartCoroutine(BeginBoss(0));
    }

    // INFINITO: chefes em sequência, cada rodada mais forte (liberado depois de vencer a Dama).
    public void StartEndless()
    {
        if (State != GameState.Title || !EndlessUnlocked) return;
        EndlessMode = true;
        StartCoroutine(BeginEndlessRound(1));
    }

    // Tela de dificuldade (depois do tutorial ou no Skip Tutorial) e então o 1º chefe.
    private IEnumerator ChooseDifficultyThenStart(bool afterTutorial)
    {
        EndlessMode = false;
        State = GameState.Transition;
        ui.ShowTitle(false);
        player.ControlsEnabled = false;
        PlayerHealth.forcedInvulnerable = true;
        bool chosen = false;
        ui.Difficulty.Open(SelectedDifficulty, d => { SelectedDifficulty = d; chosen = true; });
        while (!chosen) yield return null;
        ui.Difficulty.Close();
        yield return BeginBoss(0);
    }

    // ESC/P (P = tecla configurável): abre ou fecha a pausa. Com as opções abertas, volta para o menu de pausa.
    private void TogglePause()
    {
        if (!GameTime.Paused) { GameTime.SetPaused(true); ui.Pause.Open(); }
        else if (ui.Pause.OptionsOpen) ui.Pause.ShowOptions(false);
        else ResumeGame();
    }

    // Botão "II" dos controles de toque.
    public void TogglePauseFromButton()
    {
        if (State == GameState.Playing || State == GameState.Transition) TogglePause();
    }

    // Celular: o app saiu da tela (botão Home, ligação, notificação aberta). Abre a pausa para o jogador
    // não voltar e encontrar o personagem já morto. (No PC/web perder o foco não pausa: segue como antes.)
    private void OnApplicationPause(bool paused)
    {
        if (!paused || !Application.isMobilePlatform || GameTime.Paused) return;
        if (State == GameState.Playing || State == GameState.Transition) TogglePause();
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
        skipTitle = false;
        carry = null;
        EndlessMode = false;
        // Saindo do fim da run (vitória ou derrota) é uma pausa natural: pode ter anúncio.
        // Saindo pelo menu de pausa no meio da luta: sem anúncio.
        // (CrazyGames: nunca em botão de navegação, então lá o menu volta sem anúncio.)
        bool endOfRun = State == GameState.Victory || State == GameState.GameOver;
        if (endOfRun && Portal.AdsOnNavigation) StartCoroutine(ReloadAfterAd());
        else Reload();
    }

    // RECOMEÇAR (derrota): run nova do Valete (ou rodada 1 do infinito), com anúncio entre as runs.
    public void RestartRun()
    {
        if (State != GameState.GameOver) return;
        skipTitle = true;
        StartCoroutine(ReloadAfterAd());
    }

    // CONTINUAR (derrota): anúncio recompensado → volta para este chefe/rodada.
    public void ContinueWithAd()
    {
        if (State != GameState.GameOver || !CanContinue || leaving) return;
        StartCoroutine(ContinueRoutine());
    }

    private IEnumerator ContinueRoutine()
    {
        leaving = true;
        bool ok = false;
        yield return Portal.Rewarded(result => ok = result);
        if (!ok)
        {
            // Sem anúncio (falhou, adblock, desistiu): sem recompensa, o botão some.
            leaving = false;
            continuedStage = StageKey;
            ui.RefreshGameOverButtons();
            yield break;
        }
        continuedStage = StageKey;
        Stats.elapsedBefore = Stats.Elapsed;
        Stats.deaths++;
        carry = new Carry
        {
            endless = EndlessMode,
            stage = EndlessMode ? EndlessRound : BossIndex,
            rule = CurrentRule,
            hp = Mathf.Max(stageStartHp, Mathf.CeilToInt(config.PlayerMaxHp / 2f)),
            redraws = stageStartRedraws,
            stats = Stats,
        };
        skipTitle = false;
        Reload();
    }

    // Fim de run (derrota → tentar de novo, vitória → menu): anúncio do portal e então recarrega.
    // "leaving" impede cliques repetidos de pedirem vários anúncios.
    private bool leaving;

    private IEnumerator ReloadAfterAd()
    {
        if (leaving) yield break;
        leaving = true;
        yield return Portal.Interstitial();
        Reload();
    }

    // ===================== Fluxo =====================
    private void ApplyLevel(BossData data)
    {
        level.Build(data.LayoutIndex, data.PlatformColor);
        if (Camera.main != null) Camera.main.backgroundColor = data.BackgroundColor;
    }

    // Prepara a fase (limpa tudo, reposiciona o player, mão inicial). Usado por chefes e tutorial.
    // refill = começo de run (vida e redraws cheios). "continuar" usa pendingHp/pendingRedraws.
    private IEnumerator PrepareFight(BossData data, int index, bool refill, IEnumerable<Card> startingHand = null)
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
        // A vida só enche no começo da run (tutorial ou 1º chefe): derrotar um chefe NÃO cura.
        // Morreu = run nova, do 1º chefe (vida e redraws cheios de novo).
        if (refill) PlayerHealth.SetMax(config.PlayerMaxHp, true);

        // Redraws valem para a RUN inteira: só enchem no começo (tutorial ou 1º chefe).
        if (refill) RedrawsLeft = config.RedrawsPerBoss;
        // Coringa Ficha da Sorte: +1 redraw a cada chefe novo (o "continuar" já volta com os do começo).
        else if (Jokers.Has(JokerId.LuckyChip) && pendingRedraws < 0) RedrawsLeft++;
        Jokers.NewBoss();
        Hand.MaxSize = config.MaxHandSize + Jokers.ExtraMaxHand; // coringa Mão Grande
        Raise.NewFight(index >= 0);
        Rumors.BeginFight();
        if (pendingHp > 0) { PlayerHealth.SetCurrent(pendingHp); pendingHp = -1; }
        if (pendingRedraws >= 0) { RedrawsLeft = pendingRedraws; pendingRedraws = -1; }
        OnRedrawsChanged?.Invoke();

        if (startingHand != null)
        {
            Hand.RemoveAll();
            foreach (var c in startingHand) { Hand.Add(c); yield return new WaitForSeconds(0.08f); }
        }
        else
        {
            AudioManager.Sfx(config.SfxNewHand);
            yield return DealUpTo(StartingHand);
        }
    }

    private int StartingHand => config.StartingHandSize + Jokers.ExtraStartingHand;

    private IEnumerator BeginBoss(int index, bool resumed = false)
    {
        EndlessMode = false;
        // Primeira run de quem pulou o tutorial: a abertura da história antes do Valete.
        if (index == 0 && !resumed && !StoryLines.Seen(StoryLines.Intro))
        {
            State = GameState.Transition;
            ui.ShowTitle(false);
            yield return PlayStory(StoryLines.Intro);
        }
        var data = config.BossAt(index);
        // Começo de uma run nova (1º chefe): zera as estatísticas e os coringas (o tutorial não conta),
        // e o jogador escolhe o 1º coringa da run.
        if (index == 0 && !resumed)
        {
            Stats = new RunStats { startTime = Time.time };
            continuedStage = int.MinValue;
            Jokers.Clear();
            State = GameState.Transition;
            ui.ShowTitle(false);
            yield return PickJoker();
        }
        CurrentBossSuit = SuitInfo.Random(); // cada vez que um chefe surge, o naipe é sorteado
        string bossSub = index == config.SecretBossIndex
            ? Loc.Pick("SECRET BOSS", "CHEFE SECRETO", "JEFE SECRETO", "BOSS SECRET", "GEHEIMER BOSS", "BOSS SEGRETO")
            : $"BOSS {index + 1}/{config.Bosses.Count}";
        ui.ShowBanner(bossSub, BossTitle(data), 2.2f);
        PlayBossMusic(data);
        yield return PrepareFight(data, index, index <= 0 && !resumed);
        stageStartHp = PlayerHealth.Current;
        stageStartRedraws = RedrawsLeft;
        yield return new WaitForSeconds(0.6f);

        SpawnBoss(data);
        yield return new WaitForSeconds(1.4f);
        if (!resumed) yield return PlayStory(StoryLines.BossIntro(index)); // o chefe se apresenta (1ª vez)
        PlayerHealth.forcedInvulnerable = false;
        State = GameState.Playing;
        Pickups.PowersEnabled = Pickups.CardsEnabled = true;
        Pickups.Begin();
        if (index == 0 && !resumed) StartCoroutine(OpeningTips());
    }

    // Dicas do começo (quem não fez o tutorial). Cada uma só aparece se ainda não foi vista.
    private IEnumerator OpeningTips()
    {
        ui.Tips.Request("move");
        ui.Tips.Request("throw");
        yield return new WaitForSeconds(9f);
        ui.Tips.Request("orb");
        yield return new WaitForSeconds(18f);
        ui.Tips.Request("suits");
    }

    // ===================== Modo infinito =====================
    // Rodada n: Valete → Dama → Rei → Valete... cada vez mais forte (BossData.ForEndless).
    // A partir da rodada 4, uma "regra da casa" sorteada. Entre rodadas: +2 de vida; a cada 3, +1 redraw.
    private IEnumerator BeginEndlessRound(int round, bool resumed = false, EndlessRule resumedRule = EndlessRule.None)
    {
        EndlessMode = true;
        EndlessRound = round;
        int template = (round - 1) % config.Bosses.Count;
        CurrentRule = resumed ? resumedRule : round >= 4 ? PickRule() : EndlessRule.None;
        var data = config.Bosses[template].ForEndless(round, CurrentRule);
        bool fresh = round == 1 && !resumed;
        if (fresh)
        {
            Stats = new RunStats { startTime = Time.time };
            continuedStage = int.MinValue;
            Jokers.Clear();
            State = GameState.Transition;
            ui.ShowTitle(false);
            yield return PickJoker();
        }

        CurrentBossSuit = SuitInfo.Random();
        string sub = Loc.Pick($"ENDLESS - ROUND {round}", $"INFINITO - RODADA {round}", $"INFINITO - RONDA {round}",
                              $"INFINI - MANCHE {round}", $"ENDLOS - RUNDE {round}", $"INFINITO - ROUND {round}");
        if (CurrentRule != EndlessRule.None) sub += "  ·  " + RuleName(CurrentRule);
        ui.ShowBanner(sub, BossTitle(data), CurrentRule != EndlessRule.None ? 2.8f : 2.2f);
        PlayBossMusic(data);
        yield return PrepareFight(data, template, fresh);

        if (round > 1 && !resumed)
        {
            PlayerHealth.Heal(2);
            if ((round - 1) % 3 == 0 && RedrawsLeft < config.RedrawsPerBoss)
            {
                RedrawsLeft++;
                OnRedrawsChanged?.Invoke();
            }
        }
        stageStartHp = PlayerHealth.Current;
        stageStartRedraws = RedrawsLeft;
        yield return new WaitForSeconds(0.6f);

        SpawnBoss(data);
        yield return new WaitForSeconds(1.4f);
        PlayerHealth.forcedInvulnerable = false;
        State = GameState.Playing;
        Pickups.PowersEnabled = Pickups.CardsEnabled = true;
        Pickups.Begin();
    }

    private static EndlessRule PickRule()
    {
        EndlessRule rule;
        do rule = (EndlessRule)UnityEngine.Random.Range(1, 6);
        while (rule == lastRule);
        lastRule = rule;
        return rule;
    }

    public static string RuleName(EndlessRule rule)
    {
        switch (rule)
        {
            case EndlessRule.Shuffle: return Loc.Pick("SHUFFLE: THE NUMBER KEEPS CHANGING", "EMBARALHA: O NÚMERO VIVE MUDANDO",
                "BARAJA: EL NÚMERO NO PARA DE CAMBIAR", "MÉLANGE : LE NOMBRE CHANGE SANS CESSE", "GEMISCHT: DIE ZAHL ÄNDERT SICH STÄNDIG", "MESCOLA: IL NUMERO CAMBIA DI CONTINUO");
            case EndlessRule.LoadedDice: return Loc.Pick("LOADED DICE", "DADOS VICIADOS", "DADOS TRUCADOS", "DÉS PIPÉS", "GEZINKTE WÜRFEL", "DADI TRUCCATI");
            case EndlessRule.DoubleTrouble: return Loc.Pick("DOUBLE TROUBLE", "ATAQUE EM DOBRO", "DOBLE ATAQUE", "DOUBLE ATTAQUE", "DOPPELTER ÄRGER", "DOPPIO ATTACCO");
            case EndlessRule.WideOpen: return Loc.Pick("WIDE OPEN: 1 TO 26", "TUDO OU NADA: 1 A 26", "TODO O NADA: 1 A 26",
                "QUITTE OU DOUBLE : 1 À 26", "ALLES ODER NICHTS: 1 BIS 26", "TUTTO O NIENTE: DA 1 A 26");
            case EndlessRule.ThickSkin: return Loc.Pick("THICK SKIN: +30% HP", "COURO GROSSO: +30% DE VIDA", "PIEL GRUESA: +30% DE VIDA",
                "PEAU ÉPAISSE : +30% PV", "DICKE HAUT: +30% LP", "PELLE DURA: +30% VITA");
        }
        return "";
    }

    // Tutorial: mão com números baixos (o Ás esconde 1–8) e o TutorialDirector conduz o resto.
    private IEnumerator BeginTutorial()
    {
        // Abertura da história (só na primeira vez)
        if (!StoryLines.Seen(StoryLines.Intro))
        {
            State = GameState.Transition;
            ui.ShowTitle(false);
            yield return PlayStory(StoryLines.Intro);
        }
        Jokers.Clear(); // o tutorial não usa coringas
        ui.ShowBanner("TUTORIAL", config.TutorialBoss.DisplayName, 1.6f);
        AudioManager.Music(config.MenuMusicIntro, config.MenuMusic); // tutorial continua com a música do menu
        var hand = new[]
        {
            new Card(Suit.Clubs, 2), new Card(Suit.Spades, 7), new Card(Suit.Hearts, 4),
            new Card(Suit.Diamonds, 5), new Card(Suit.Clubs, 1), new Card(Suit.Spades, 3), new Card(Suit.Hearts, 8),
        };
        yield return PrepareFight(config.TutorialBoss, -1, true, hand);
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
        ui.Story.Hide(); // o diálogo do Coringa pode estar aberto
        InputLocked = false;
        FusionController.SetTimeScale(1f);
        Fusion.Cancel();
        Pickups.StopAndClear();
        ClearProjectiles();
        if (Boss != null) Destroy(Boss.gameObject);
        StartCoroutine(ChooseDifficultyThenStart(true));
    }

    // ===================== História =====================
    // Mostra uma cena da história (StoryLines). Cada cena aparece só na primeira vez, e nunca no infinito.
    // O tempo fica parado durante o diálogo (o relógio da run usa Time.time, então não conta) e
    // o input volta ao estado anterior depois.
    public IEnumerator PlayStory(string id, bool force = false)
    {
        if (!force && (EndlessMode || StoryLines.Seen(id))) yield break;
        var lines = StoryLines.Get(id);
        if (lines == null) yield break;
        StoryLines.MarkSeen(id); // marca já: pular ou fechar o jogo no meio também conta como visto
        bool wasLocked = InputLocked;
        InputLocked = true;
        FusionController.SetTimeScale(0f);
        yield return ui.Story.Play(lines);
        FusionController.SetTimeScale(1f);
        yield return null; // o clique que fechou o diálogo não vira arremesso
        InputLocked = wasLocked;
    }

    // Debug (ponte do Editor): call GameManager.DebugStory boss1_intro
    public static string DebugStory(string id)
    {
        if (Instance == null) return "no game";
        if (StoryLines.Get(id) == null) return "unknown id";
        Instance.StartCoroutine(Instance.PlayStory(id, true));
        return "story " + id;
    }

    public static string DebugStoryNext() { Instance.ui.Story.RequestNext(); return "next"; }
    public static string DebugStorySkip() { Instance.ui.Story.RequestSkip(); return "skip"; }

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

    // "Match": quanto mais perto do número do chefe, maior o match (vale para dano e para cura de Copas).
    // Mesma faixa da tabela de dano: 1 → match_max · 2 → match_mid · 3–4 → match_lower · 5+ → nada (já toca o "no damage").
    // O número exato (distância 0) é o crítico, que tem som próprio.
    private void PlayCloseness(int distance)
    {
        var clips = config.SfxCloseness;
        if (clips == null || clips.Length < 3 || distance <= 0) return;
        int index = distance == 1 ? 2 : distance == 2 ? 1 : distance <= 4 ? 0 : -1;
        if (index >= 0) AudioManager.Sfx(clips[index], 0.75f, 0f);
    }

    private void HandleBossHit(HitResult r)
    {
        if (r.isHeal) PlayerHealth.Heal(r.amount);
        if (r.isCritical) AudioManager.Sfx(config.SfxCritical, 1f, 0f);
        else if (r.isHeal) AudioManager.Sfx(config.SfxHeal);
        else if (r.amount > 0) AudioManager.Sfx(config.SfxHit);
        else AudioManager.Sfx(config.SfxMiss, 0.8f); // acertou longe do número: som próprio de "sem dano"
        if (!r.isCritical) PlayCloseness(r.distance);
        if (r.isCritical) Stats.criticals++;
        ui.Tips.Request(r.isCritical ? "crit" : "hint");
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
        if (!IsTutorial) Portal.HappyMoment();
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
            QuickTips.MarkAllSeen(); // fez o tutorial completo: as dicas curtas não precisam aparecer
            ui.HidePrompt();
            InputLocked = false;
            yield return PlayStory(StoryLines.TutorialOutro);
            ui.ShowBanner("TUTORIAL COMPLETE", "GOOD LUCK!", 2f);
            yield return new WaitForSeconds(2.4f);
            StartCoroutine(ChooseDifficultyThenStart(true));
            yield break;
        }

        // Última fala do chefe derrotado (1ª vez; nunca no infinito)
        yield return PlayStory(StoryLines.BossDefeat(BossIndex));

        // Fichas (taverna) e boatos (só nos chefes da história)
        int chips = AwardBossChips();
        var rumors = EndlessMode ? new List<int>() : Rumors.Evaluate(BossIndex);
        chips += rumors.Count * Rumors.ChipReward;
        Stats.chipsEarned += rumors.Count * Rumors.ChipReward;

        // Venceu a Dama pela primeira vez: libera o modo infinito.
        bool unlockedNow = !EndlessMode && BossIndex >= 1 && !EndlessUnlocked;
        if (unlockedNow) EndlessUnlocked = true;
        string chipText = $"+{chips} " + Loc.Pick("CHIPS", "FICHAS", "FICHAS", "JETONS", "CHIPS", "FICHE");
        string sub = unlockedNow ? Loc.Pick("ENDLESS MODE UNLOCKED!", "MODO INFINITO LIBERADO!", "¡MODO INFINITO DESBLOQUEADO!",
                                            "MODE INFINI DÉBLOQUÉ !", "ENDLOSMODUS FREIGESCHALTET!", "MODALITÀ INFINITA SBLOCCATA!") + "  ·  " + chipText
                                 : "DEFEATED  ·  " + chipText;
        ui.ShowBanner(sub, BossTitle(CurrentBossData) + " FOLDED!", unlockedNow ? 2.6f : 2f);
        yield return new WaitForSeconds(unlockedNow ? 3f : 2.4f);
        // Boatos novos: um aviso por boato (o texto completo fica no mural da taverna)
        foreach (int rumor in rumors)
        {
            ui.ShowBanner(Loc.Pick("NEW RUMOR!", "BOATO NOVO!", "¡RUMOR NUEVO!", "NOUVELLE RUMEUR !", "NEUES GERÜCHT!", "NUOVA VOCE!") + $"  +{Rumors.ChipReward}",
                          "<size=60%>" + Rumors.Lore(rumor) + "</size>", 2.6f);
            yield return new WaitForSeconds(3f);
        }

        bool kingBeaten = !EndlessMode && BossIndex == config.Bosses.Count - 1;
        if (kingBeaten) Stats.rankedElapsed = Stats.Elapsed; // o ranking conta a run até o Rei
        bool toAce = kingBeaten && ProgressSave.AceUnlocked;

        if (IsSecretBoss)
        {
            // Ás derrotado: a escolha do final
            yield return PlayStory(StoryLines.AceDefeat, true);
            int ending = -1;
            ui.Ending.Open(e => ending = e);
            while (ending < 0) yield return null;
            ui.Ending.Close();
            ProgressSave.MarkEnding(ending);
            yield return PlayStory(ending == 0 ? StoryLines.EndingThrone : StoryLines.EndingFree, true);
            ShowFinalVictory();
            yield break;
        }

        if (EndlessMode)
        {
            if (!Jokers.BeltFull) yield return PickJoker();
            yield return ui.Curtain.Cover();
            yield return Portal.Interstitial();
            StartCoroutine(BeginEndlessRound(EndlessRound + 1));
            yield return null;
            yield return ui.Curtain.Reveal();
        }
        else if (BossIndex + 1 < config.Bosses.Count || toAce)
        {
            // O Ás se revela depois do Rei (só com os 3 boatos do Rei descobertos)
            if (toAce) yield return PlayStory(StoryLines.AceReveal, true);
            // Escolhe um coringa novo antes do próximo chefe
            if (!Jokers.BeltFull) yield return PickJoker();
            // Transição: cartas caem e cobrem a tela, a fase troca por baixo, e as cartas caem embora.
            yield return ui.Curtain.Cover();
            // Tela coberta pelas cartas, entre um chefe e outro: momento certo para o anúncio do portal.
            yield return Portal.Interstitial();
            StartCoroutine(BeginBoss(BossIndex + 1)); // monta a fase nova na hora (antes do 1º yield)
            yield return null;
            yield return ui.Curtain.Reveal();
        }
        else
        {
            yield return PlayStory(StoryLines.Ending); // final "normal" (gancho para o Ás)
            // Bobo dá a pista dos boatos (uma vez), enquanto o Ás não foi liberado
            if (!ProgressSave.AceUnlocked) yield return PlayStory(StoryLines.AceHint);
            ShowFinalVictory();
        }
    }

    private void ShowFinalVictory()
    {
        State = GameState.Victory;
        player.ControlsEnabled = false;
        skipTitle = false;
        acceptClickTime = Time.unscaledTime + 1.5f;
        AudioManager.Sfx(config.SfxVictory, 1f, 0f);
        Portal.HappyMoment(true); // vitória final: o momento mais raro
        AudioManager.Music(config.MenuMusicIntro, config.MenuMusic, 3f);
        float time = Stats.rankedElapsed > 0f ? Stats.rankedElapsed : Stats.Elapsed;
        ui.ShowVictory(Stats, time, SelectedDifficulty);
    }

    // ===================== Fichas e coringas =====================
    // Fichas por chefe vencido: Valete 15 · Dama 25 · Rei 40 · Ás 60 (infinito: 10 + 2 por rodada)
    // + 1 por crítico na luta. EASY rende metade e HARDCORE 50% a mais.
    private int AwardBossChips()
    {
        int baseChips = EndlessMode ? 10 + 2 * EndlessRound
            : BossIndex switch { 0 => 15, 1 => 25, 2 => 40, _ => 60 };
        baseChips += Rumors.Track.crits;
        float mult = SelectedDifficulty == Difficulty.Easy ? 0.5f : SelectedDifficulty == Difficulty.Hardcore ? 1.5f : 1f;
        int chips = Mathf.Max(1, Mathf.RoundToInt(baseChips * mult));
        ProgressSave.AddChips(chips);
        Stats.chipsEarned += chips;
        return chips;
    }

    // Tela "ESCOLHA UM CORINGA": 3 sorteados entre os liberados na taverna. Espera o jogador escolher.
    private IEnumerator PickJoker()
    {
        var choices = JokerCatalog.RollChoices(3);
        if (choices.Count == 0 || Jokers.BeltFull) yield break;
        bool wasLocked = InputLocked;
        InputLocked = true;
        JokerId? picked = null;
        ui.JokerPick.Open(choices, id => picked = id);
        while (picked == null) yield return null;
        ui.JokerPick.Close();
        Jokers.Add(picked.Value);
        // Ficha da Sorte: +1 redraw na hora
        if (picked.Value == JokerId.LuckyChip) { RedrawsLeft++; OnRedrawsChanged?.Invoke(); }
        if (picked.Value == JokerId.BigHand) Hand.MaxSize = config.MaxHandSize + Jokers.ExtraMaxHand;
        yield return null; // o clique que escolheu não vira arremesso
        InputLocked = wasLocked;
    }

    // ===================== RAISE / TRUCO =====================
    // Botão RAISE (ou tecla T): a próxima carta vale a aposta. Às vezes o chefe responde RE-RAISE.
    public void TryRaise()
    {
        if (!CanAct || Fusion.Active || Boss == null || Boss.IsDead || IsTutorial || !Raise.Available) return;
        float chance = EndlessMode ? 0.4f : CurrentBossData.ReRaiseChance;
        bool reRaise = Raise.Start(chance);
        Fx.Popup(RaiseController.RaiseWord, player.transform.position + Vector3.up * 2.8f, new Color(1f, 0.83f, 0.28f), 12f, 1.3f, 1.6f, true, Color.white);
        AudioManager.Sfx(config.SfxPowerUp, 0.9f, 0f, 1.2f);
        Fx.Shake(0.2f, 0.15f);
        if (reRaise) StartCoroutine(ReRaiseRoutine());
    }

    private IEnumerator ReRaiseRoutine()
    {
        yield return new WaitForSeconds(0.45f); // o grito do jogador aparece antes da resposta
        if (Boss == null || Boss.IsDead || State != GameState.Playing) { Raise.Answer(false); yield break; }
        bool wasLocked = InputLocked;
        InputLocked = true;
        FusionController.SetTimeScale(0f); // tempo parado enquanto decide
        Fx.Popup(RaiseController.ReRaiseWord, Boss.transform.position + Vector3.up * 3.6f, new Color(1f, 0.35f, 0.3f), 13f, 1.6f, 1.2f, true, Color.white);
        AudioManager.Sfx(config.SfxDiceLand, 1f, 0f, 0.8f);
        bool? call = null;
        ui.RaiseUi.Ask(c => call = c);
        while (call == null) yield return null;
        Raise.Answer(call.Value);
        if (call.Value && Boss != null) Boss.SpeedBoost(8f);
        FusionController.SetTimeScale(1f);
        yield return null;
        InputLocked = wasLocked;
    }

    private void HandlePlayerDied()
    {
        State = GameState.GameOver;
        Fusion.Cancel();
        Portal.SadMoment();
        skipTitle = true; // "tentar de novo" = run nova a partir do 1º chefe
        acceptClickTime = Time.unscaledTime + 1.5f;
        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        FusionController.SetTimeScale(0.3f);
        yield return new WaitForSecondsRealtime(1.2f);
        FusionController.SetTimeScale(1f);
        ui.ShowGameOver(this);
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
        StartCoroutine(DealUpTo(StartingHand));
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

    // "Easy" | "Normal" | "Hardcore"
    public static string DebugDifficulty(string name)
    {
        SelectedDifficulty = Enum.TryParse(name, true, out Difficulty d) ? d : Difficulty.Normal;
        return "difficulty " + SelectedDifficulty;
    }

    // Abre a tela de dificuldade como no Skip Tutorial.
    public static string DebugChoose()
    {
        if (Instance == null || Instance.State != GameState.Title) return "not in title";
        Instance.StartGame(false);
        return "choosing";
    }

    public static string DebugPick(string name)
    {
        if (Instance == null) return "no game";
        return Instance.ui.Difficulty.DebugPick(Enum.TryParse(name, true, out Difficulty d) ? d : Difficulty.Normal);
    }

    // Troca o idioma (testa a tradução): "next" ou o nome ("German", "Spanish"...)
    public static string DebugLang(string arg)
    {
        if (Enum.TryParse(arg, true, out Language lang)) Loc.Current = lang;
        else Loc.Next();
        return "lang " + Loc.Current;
    }

    public static string DebugPack()
    {
        if (Instance == null) return "no game";
        Instance.Pickups.SpawnCardPack();
        return "pack";
    }

    // Debug: só a cortina de cartas (cobre, espera "hold" segundos, revela). Ex.: call GameManager.DebugCurtain 3
    public static string DebugCurtain(string hold)
    {
        if (Instance == null) return "no game";
        float h = float.TryParse(hold, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 2f;
        Instance.StartCoroutine(Instance.CurtainTest(h));
        return "ok";
    }

    private IEnumerator CurtainTest(float hold)
    {
        yield return ui.Curtain.Cover();
        yield return new WaitForSecondsRealtime(hold);
        yield return ui.Curtain.Reveal();
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
    // Debug: força o chefe a trocar de número (testa o giro da carta). Ex.: call GameManager.DebugReroll
    public static string DebugReroll()
    {
        if (Instance == null || Instance.Boss == null) return "no boss";
        Instance.Boss.ForceReroll();
        return "ok";
    }

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

    // Câmera lenta para fotografar efeitos (ex.: "0.1"; "1" volta ao normal).
    public static string DebugSlow(string scale)
    {
        FusionController.SetTimeScale(float.Parse(scale, System.Globalization.CultureInfo.InvariantCulture));
        return "ts " + Time.timeScale;
    }

    // Troca o fundo na hora (índice da lista "backgrounds" do GameConfig) — usado para comparar fundos.
    public static string DebugBackground(string index)
    {
        if (Instance == null) return "no game";
        Instance.config.BackgroundIndex = int.Parse(index);
        Instance.ApplyLevel(Instance.CurrentBossData);
        return "bg " + index;
    }

    public static string DebugVolley(string count)
    {
        if (Instance == null || Instance.Boss == null) return "no boss";
        Instance.Boss.DebugVolley(int.Parse(count));
        return "volley";
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
        gm.ui.ShowVictory(gm.Stats, gm.Stats.Elapsed, SelectedDifficulty);
        return "victory";
    }

    // Ataques sorteados até agora (testa os ataques misturados). "clear" zera.
    public static string DebugAttackLog(string arg)
    {
        string log = BossController.AttackLog;
        if (arg == "clear") BossController.AttackLog = "";
        return log;
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

    // Atalhos de teste na build web: abrir a página com "?devkeys=1" no endereço.
    // F1 = fusão (+)  F2 = morrer  F3 = libera o infinito  F4 = acerto crítico  F5 = pacote de cartas
    // (Lido na primeira vez que é usado, no Update: o Unity não deixa ler a URL num inicializador estático.)
    private static bool? devKeys;
    private static bool DevKeys => devKeys ??= Application.absoluteURL != null && Application.absoluteURL.Contains("devkeys=1");

    private void HandleDevKeys(Keyboard kb)
    {
        if (kb.f1Key.wasPressedThisFrame) Fusion.Begin(PowerType.Add); // slots vazios: testa arrastar as cartas
        if (kb.f2Key.wasPressedThisFrame) DebugKillPlayer();
        if (kb.f3Key.wasPressedThisFrame) { DebugUnlockEndless("on"); ui.ShowTitle(State == GameState.Title); }
        if (kb.f4Key.wasPressedThisFrame) DebugCrit();
        if (kb.f5Key.wasPressedThisFrame) DebugPack();
    }

    // Debug: libera o infinito ("on") ou bloqueia de novo ("off").
    public static string DebugUnlockEndless(string arg)
    {
        EndlessUnlocked = arg != "off";
        return "endless unlocked " + EndlessUnlocked;
    }

    // Debug: começa o infinito numa rodada. Ex.: call GameManager.DebugEndless 4
    public static string DebugEndless(string round)
    {
        if (Instance == null || Instance.State != GameState.Title) return "not in title";
        Instance.StartCoroutine(Instance.BeginEndlessRound(Mathf.Max(1, int.Parse(round))));
        return "endless " + round;
    }

    // Debug: botões da tela de derrota. "continue" | "restart" | "menu"
    public static string DebugGameOver(string arg)
    {
        var gm = Instance;
        if (gm == null || gm.State != GameState.GameOver) return "not game over (state " + gm?.State + ")";
        if (arg == "continue") gm.ContinueWithAd();
        else if (arg == "restart") gm.RestartRun();
        else gm.BackToMainMenu();
        return arg + " canContinue=" + gm.CanContinue;
    }

    // Debug: ataques novos. Ex.: call GameManager.DebugRoulette 9 · DebugMarked 4
    public static string DebugRoulette(string steps)
    {
        if (Instance == null || Instance.Boss == null) return "no boss";
        Instance.Boss.DebugRoulette(int.Parse(steps));
        return "roulette";
    }

    public static string DebugMarked(string count)
    {
        if (Instance == null || Instance.Boss == null) return "no boss";
        Instance.Boss.DebugMarked(int.Parse(count));
        return "marked";
    }

    // Debug: dá um coringa na run atual. Ex.: call GameManager.DebugJoker SharpSword
    public static string DebugJoker(string id)
    {
        if (!Enum.TryParse(id, true, out JokerId j)) return "unknown joker";
        Jokers.Add(j);
        return "belt: " + string.Join(",", Jokers.Belt);
    }

    public static string DebugRaise()
    {
        if (Instance == null) return "no game";
        Instance.TryRaise();
        return "raise " + Instance.Raise.State;
    }

    public static string DebugBoss(string index)
    {
        if (Instance == null) return "no game";
        Instance.StopAllCoroutines();
        Instance.StartCoroutine(Instance.BeginBoss(int.Parse(index)));
        return "ok";
    }
}
