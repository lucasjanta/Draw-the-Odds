using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Chefe (placeholder com sprite de carta). A "IA" é uma coroutine (Brain) em loop:
// passear → antecipação (dado rolando; a face que sai escolhe o ataque) → ataque → repetir.
public class BossController : MonoBehaviour
{

    // Área onde o chefe voa (o chefe tem 3,6 × 5 unidades: assim não sai da tela nem cobre o HUD dos cantos)
    private const float ArenaMinX = -13f, ArenaMaxX = 13f, ArenaMinY = 3.5f, ArenaMaxY = 6.5f;

    // Topo de cada andar (chão + 3 plataformas; iguais em todos os layouts) — faixas do ataque dos coringas
    private static readonly float[] LaneTops = { LevelBuilder.FloorTop, -2.75f, 1.5f, 5.75f };

    public BossData Data { get; private set; }
    public Suit Suit { get; private set; }
    // Nome com o naipe colorido, ex.: "JACK ♠" (sem naipe no coringa do tutorial)
    public string Title => Data.HasSuits ? $"{Data.DisplayName} {SuitInfo.Colored(SuitInfo.Symbol(Suit), Suit)}" : Data.DisplayName;
    public int CurrentHp { get; private set; }
    // Número que vale AGORA (no Ás, o 1º número e, depois de travado, o 2º).
    public int SecretNumber { get; private set; }
    // Ás (dois números): o 2º número, e o 1º depois que foi acertado ("travado"; -1 = ainda não).
    public int SecondNumber { get; private set; }
    public int LockedNumber { get; private set; } = -1;
    public bool OnSecondNumber => LockedNumber >= 0;
    public bool IsDead { get; private set; }
    public bool IsVulnerable => !IsDead && !intro && !changingPhase;

    public event Action<HitResult> OnHit;
    public event Action<int, int> OnHealthChanged;
    public event Action OnNumberChanged;
    public event Action OnDefeated;
    public event Action OnSuitChanged;    // Dama (fase nova) e Rei (naipe do ataque): o HUD atualiza o nome

    // ===================== Fases (Dama) e gêmeos (27/11) =====================
    // Dama: cada vez que a vida zera ela troca de naipe e a vida enche de novo, até passar pelos 4.
    public int Phase { get; private set; }                 // 0..Phases-1
    public int PhaseCount => Data.Phases;
    private readonly List<Suit> phaseOrder = new List<Suit>();
    public IReadOnlyList<Suit> PhaseOrder => phaseOrder;
    private bool changingPhase;                             // invulnerável enquanto troca de fase
    private float nextPhaseShuffle;                         // fase de Ouros: troca o número de tempos em tempos
    // Gêmeos: o irmão (quando um cai, o outro fica com raiva) e onde cada um para ao entrar.
    public BossController Partner { get; private set; }
    private float entryX;
    private float startDelay;
    private bool avenging;
    public event Action OnTelegraph;      // começou a antecipação de um ataque (tutorial usa)
    public event Action OnAttackFinished;
    public event Action<string> OnDieLanded;
    public static string AttackLog = ""; // o dado parou: nome do ataque escolhido (ex.: "DICE x3")

    // Tutorial: o chefe só passeia até isto ser ligado.
    public bool AttacksEnabled { get; set; } = true;
    // Tutorial: a vida não passa de 1 (o chefe não morre antes da luta livre).
    public bool CannotDie { get; set; }

    private GameConfig config;
    private PlayerHealth player;
    private float floorY;
    private SpriteRenderer sr;
    private Transform visual;
    private Vector3 visualBase;
    private bool intro = true;
    private bool halfRerollDone;
    private float rerollTimer;
    private bool rerolling;
    private Coroutine brain;
    // Avisos de perigo na tela (setas pixel art + faixas translúcidas), animados por BlinkWarnings.
    private class Warning
    {
        public SpriteRenderer sr;
        public Color color;
        public Vector3 basePos;
        public Vector2 bob;      // direção para onde a seta "cutuca" (a faixa não se mexe)
        public bool stripe;
    }
    private readonly List<Warning> warnings = new List<Warning>(); // todos (para limpar ao morrer)

    // Dificuldade escolhida (EASY/NORMAL/HARDCORE) e qual chefe este é (0..2; −1 = tutorial).
    private DifficultySettings diff = new DifficultySettings();
    private int bossIndex = -1;
    private bool quickFollow;   // emendar o próximo ataque sem a pausa (Dama e Rei, de vez em quando)

    // ===================== Dificuldade =====================
    // Raiva: 0 com a vida cheia → Data.Enrage com a vida no fim. Deixa tudo mais rápido
    // (só Dama e Rei têm Enrage > 0 no GameConfig).
    public float Rage => Data.Enrage * (1f - (float)CurrentHp / Mathf.Max(1, Data.MaxHp));
    private float PaceFactor => (1f + Rage) * Boost * SuitPace;               // pausas, passeio e antecipação mais curtos
    private float ProjectileFactor => (1f + Rage * 0.5f) * Boost * SuitPace;  // projéteis um pouco mais rápidos

    // RE-RAISE aceito (CALL): o chefe fica mais rápido por alguns segundos.
    // Gêmeo que perdeu o irmão: fica mais rápido até o fim da luta.
    private float boostUntil;
    private float Boost => (Time.time < boostUntil ? 1.25f : 1f) * (avenging ? 1.35f : 1f);
    public void SpeedBoost(float seconds) => boostUntil = Mathf.Max(boostUntil, Time.time + seconds);
    // Dama na fase de Espadas: tudo 25% mais rápido.
    private float SuitPace => Data.SuitMode == BossSuitMode.Phases && Suit == Suit.Spades ? 1.25f : 1f;

    // entryX = onde o chefe para ao entrar (gêmeos: um de cada lado). delay = espera extra antes do 1º ataque.
    public static BossController Spawn(GameConfig config, BossData data, Suit suit, PlayerHealth player, float floorY,
        float entryX = 0f, float delay = 0f)
    {
        var go = new GameObject("Boss " + data.DisplayName);
        go.layer = LayerMask.NameToLayer("Enemy");
        go.transform.position = new Vector3(entryX, 16f, 0f);

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;

        var boss = go.AddComponent<BossController>();
        boss.config = config;
        boss.Data = data;
        boss.Suit = suit;
        boss.player = player;
        boss.floorY = floorY;
        boss.entryX = entryX;
        boss.startDelay = delay;
        // Dama: ordem dos 4 naipes (começa no sorteado, os outros em ordem aleatória)
        if (data.SuitMode == BossSuitMode.Phases)
        {
            boss.phaseOrder.Add(suit);
            var rest = new List<Suit> { Suit.Clubs, Suit.Spades, Suit.Hearts, Suit.Diamonds };
            rest.Remove(suit);
            while (rest.Count > 0) { int i = UnityEngine.Random.Range(0, rest.Count); boss.phaseOrder.Add(rest[i]); rest.RemoveAt(i); }
        }

        boss.visual = new GameObject("Visual").transform;
        boss.visual.SetParent(go.transform, false);
        Sprite sprite = data.SpriteFor(suit);
        boss.sr = Fx.MakeSprite("Sprite", sprite, go.transform.position, Color.white, 40, boss.visual);
        boss.sr.transform.localScale = Vector3.one * data.SpriteScale;

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        Vector2 size = sprite != null ? (Vector2)sprite.bounds.size * data.SpriteScale : new Vector2(3.6f, 5f);
        col.size = size * 0.8f; // 80% do sprite: encostar na borda da carta não machuca

        boss.CurrentHp = data.MaxHp;
        boss.RollNumbers();
        return boss;
    }

    // Sorteia o número (e o 2º, no Ás). O 2º é sempre diferente do 1º.
    private void RollNumbers()
    {
        int old = SecretNumber;
        do SecretNumber = UnityEngine.Random.Range(Data.MinNumber, Data.MaxNumber + 1);
        while (SecretNumber == old && old > 0 && Data.MaxNumber > Data.MinNumber);
        LockedNumber = -1;
        if (!Data.TwoNumbers) return;
        do SecondNumber = UnityEngine.Random.Range(Data.MinNumber, Data.MaxNumber + 1);
        while (SecondNumber == SecretNumber && Data.MaxNumber > Data.MinNumber);
    }

    private void Start()
    {
        visualBase = visual.localPosition;
        var gm = GameManager.Instance;
        // Nível de força (0..2) em vez da posição na lista: a lista de chefes cresceu (gêmeos, Valete de Espadas...)
        bossIndex = gm != null && gm.IsTutorial ? -1 : Data.Tier;
        if (gm != null) diff = gm.CurrentDifficulty;
        brain = StartCoroutine(Brain());
    }

    // Gêmeos: cada um conhece o outro (o GameManager liga os dois).
    public void SetPartner(BossController other) => Partner = other;

    // O irmão caiu: este fica com raiva (mais rápido até o fim da luta).
    public void Avenge()
    {
        if (IsDead || avenging) return;
        avenging = true;
        Fx.Popup(Loc.Pick("REVENGE!", "VINGANÇA!", "¡VENGANZA!", "VENGEANCE !", "RACHE!", "VENDETTA!"),
                 transform.position + Vector3.up * (sr.bounds.extents.y + 1.6f), new Color(1f, 0.35f, 0.3f), 11f, 1.8f, 1.6f, true, Color.white);
        StartCoroutine(HurtFlash());
        Fx.Shake(0.3f, 0.2f);
    }

    private void Update()
    {
        if (IsDead || intro || changingPhase) return;
        // Dama na fase de Ouros: o número muda a cada 14 s
        if (Data.SuitMode == BossSuitMode.Phases && Suit == Suit.Diamonds && Time.time >= nextPhaseShuffle && !rerolling)
        {
            nextPhaseShuffle = Time.time + 14f;
            StartCoroutine(Reroll("SHUFFLE!"));
        }
        if (Data.RerollEverySeconds <= 0f) return;
        rerollTimer += Time.deltaTime;
        if (rerollTimer >= Data.RerollEverySeconds && !rerolling)
        {
            rerollTimer = 0f;
            StartCoroutine(Reroll("SHUFFLE!"));
        }
    }

    // Troca o naipe (sprite e nome) sem mexer no número.
    private void SetSuit(Suit s)
    {
        if (Suit == s) return;
        Suit = s;
        Sprite next = Data.SpriteFor(s);
        if (next != null)
        {
            if (flipping) flipFront = next; // no meio do giro: a frente nova aparece quando ele acabar
            else sr.sprite = next;
        }
        OnSuitChanged?.Invoke();
    }

    // ===================== IA =====================
    private IEnumerator Brain()
    {
        // Entrada: tom sobe a cada chefe (Valete → Dama → Rei); tutorial (índice −1) mais grave.
        if (startDelay <= 0f) AudioManager.Sfx(config.SfxBossEntry, 0.9f, 0f, config.BossEntryPitch(bossIndex));
        yield return MoveTo(new Vector2(entryX, 6f), 1.4f);
        intro = false;
        if (startDelay > 0f) yield return Wander(startDelay); // 2º gêmeo: não ataca junto com o irmão

        while (!IsDead)
        {
            if (!AttacksEnabled)
            {
                yield return Wander(0.5f);
                continue;
            }
            // Pausa entre ataques (dificuldade: EASY pausa mais, HARDCORE menos). Às vezes emenda direto.
            float idle = Data.IdleTime * diff.IdleMultiplier / PaceFactor * UnityEngine.Random.Range(0.8f, 1.2f);
            yield return Wander(quickFollow ? 0.35f : idle);
            if (!AttacksEnabled) continue; // o tutorial pode desligar os ataques durante o passeio

            // O dado decide o ataque: cada face é um ataque (ou uma variação mais forte/fraca dele).
            int face = UnityEngine.Random.Range(1, 7);
            BossAttack attack = PickAttack(face);

            // Dama e Rei podem MISTURAR: um 2º dado sorteia outro tipo de ataque e os dois vêm juntos.
            float combo = Data.ComboChance * diff.ComboMultiplier;
            BossAttack second = null;
            int face2 = 0;
            if (UnityEngine.Random.value < combo)
            {
                for (int guard = 0; guard < 12 && second == null; guard++)
                {
                    face2 = UnityEngine.Random.Range(1, 7);
                    var candidate = PickAttack(face2);
                    if (candidate.Type != attack.Type) second = candidate;
                }
            }

            // Rei: o naipe gira junto com o dado e muda o ataque (♣ mais projéteis · ♠ mais rápido ·
            // ♥ ele se cura · ♦ chuva de fichas logo depois).
            bool rollSuit = Data.SuitMode == BossSuitMode.PerAttack;
            Suit rolled = rollSuit ? SuitInfo.Random() : Suit;

            OnTelegraph?.Invoke();
            yield return Telegraph(attack, face, second, face2, rollSuit, rolled);
            if (rollSuit)
            {
                attack = SuitTwist(attack, rolled);
                if (second != null) second = SuitTwist(second, rolled);
                if (rolled == Suit.Hearts) HealSelf(2);
            }

            if (second == null) yield return RunAttack(attack);
            else yield return RunTogether(attack, second);
            if (rollSuit && rolled == Suit.Diamonds && !IsDead) yield return Rain(3, 22f * ProjectileFactor);
            OnAttackFinished?.Invoke();
            quickFollow = UnityEngine.Random.value < Data.ChainChance * diff.ComboMultiplier;
        }
    }

    // Ataque de uma face, já com os ajustes da dificuldade.
    private BossAttack PickAttack(int face)
    {
        BossAttack attack = Data.AttackForFace(face);
        // De vez em quando um ataque extra (roleta, cartas marcadas...) no lugar da face.
        if (Data.HasBonusAttacks && UnityEngine.Random.value < Data.BonusAttackChance) attack = Data.RandomBonusAttack() ?? attack;
        if (attack.Type == BossAttackType.Jokers && config.RandomJoker() == null)
            attack = new BossAttack(BossAttackType.Volley, attack.Count, attack.Speed);

        // HARDCORE: limite de investidas maior (Valete 2, Dama 3, Rei 5). Quem não tem investida
        // no dado (Valete) ganha uma na face 2.
        int maxDashes = diff.MaxDashes(bossIndex);
        if (maxDashes > 0)
        {
            if (face == 2 && !Data.HasAttack(BossAttackType.Dash)) attack = new BossAttack(BossAttackType.Dash, maxDashes, 30f);
            if (attack.Type == BossAttackType.Dash)
            {
                int count = Mathf.Max(attack.Count, UnityEngine.Random.Range(Mathf.Max(1, maxDashes - 1), maxDashes + 1));
                attack = new BossAttack(BossAttackType.Dash, count, attack.Speed);
            }
        }
        return attack;
    }

    // Rei: ajusta o ataque pelo naipe sorteado.
    private static BossAttack SuitTwist(BossAttack a, Suit s)
    {
        switch (s)
        {
            case Suit.Clubs:  // mais projéteis (investida: +1)
                return new BossAttack(a.Type, a.Count + (a.Type == BossAttackType.Dash ? 1 : 2), a.Speed);
            case Suit.Spades: // 30% mais rápido
                return new BossAttack(a.Type, a.Count, a.Speed * 1.3f);
        }
        return a;
    }

    // Cura o próprio chefe (Rei com ♥, Dama de Copas punindo o chute).
    private void HealSelf(int amount)
    {
        if (IsDead || CurrentHp >= Data.MaxHp) return;
        CurrentHp = Mathf.Min(Data.MaxHp, CurrentHp + amount);
        OnHealthChanged?.Invoke(CurrentHp, Data.MaxHp);
        Fx.Popup($"+{amount}", transform.position + Vector3.up * (sr.bounds.extents.y + 0.6f), SuitInfo.GetColor(Suit.Hearts), 9f, 1.2f, 1.6f, true, Color.white);
        AudioManager.Sfx(config.SfxHeal, 0.7f, 0.05f, 0.8f);
    }

    private IEnumerator RunAttack(BossAttack attack)
    {
        float speed = attack.Speed * ProjectileFactor * diff.AttackSpeedMultiplier;
        switch (attack.Type)
        {
            case BossAttackType.Volley: yield return Volley(attack.Count, speed); break;
            case BossAttackType.Rain: yield return Rain(attack.Count, speed); break;
            case BossAttackType.Dash: yield return Dash(attack.Count, speed); break;
            case BossAttackType.Jokers: yield return Jokers(attack.Count, speed); break;
            case BossAttackType.Roulette: yield return Roulette(attack.Count, speed); break;
            case BossAttackType.MarkedCards: yield return MarkedCards(attack.Count); break;
        }
    }

    // Dois ataques ao mesmo tempo (o 2º começa um pouquinho depois, para dar para ler).
    private IEnumerator RunTogether(BossAttack a, BossAttack b)
    {
        int done = 0;
        StartCoroutine(Track(RunAttack(a), () => done++));
        yield return new WaitForSeconds(0.3f);
        StartCoroutine(Track(RunAttack(b), () => done++));
        while (done < 2) yield return null;
    }

    private IEnumerator Track(IEnumerator routine, Action onDone)
    {
        yield return routine;
        onDone();
    }

    private IEnumerator MoveTo(Vector2 target, float duration)
    {
        Vector2 start = transform.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            transform.position = Vector2.Lerp(start, target, k);
            yield return null;
        }
        transform.position = target;
    }

    private IEnumerator Wander(float duration)
    {
        Vector2 dest = RandomArenaPoint();
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            Vector2 pos = Vector2.MoveTowards(transform.position, dest, Data.MoveSpeed * PaceFactor * Time.deltaTime);
            transform.position = pos;
            if (Vector2.Distance(pos, dest) < 0.1f) dest = RandomArenaPoint();
            visual.localPosition = visualBase + Vector3.up * Mathf.Sin(Time.time * 3f) * 0.25f;
            yield return null;
        }
    }

    // Ponto aleatório da área. Embaixo dos painéis do HUD (cantos de cima) o chefe voa mais baixo:
    // painel do player vai até x ≈ −8,5 e o do chefe começa em x ≈ 6,6; ambos descem até y ≈ 7,1.
    private const float HudLeftX = -6.5f, HudRightX = 4.6f, UnderHudMaxY = 4.4f;

    private Vector2 RandomArenaPoint()
    {
        float x = UnityEngine.Random.Range(ArenaMinX, ArenaMaxX);
        float maxY = x < HudLeftX || x > HudRightX ? UnderHudMaxY : ArenaMaxY;
        return new Vector2(x, UnityEngine.Random.Range(ArenaMinY, maxY));
    }

    // Antecipação: um dado rola acima da cabeça e o chefe treme. Quando o dado para, a face mostra
    // qual ataque vem (nome escrito em cima) e o chefe pisca com a cor do ataque.
    // Com second != null, rolam DOIS dados (ataques misturados) e os dois nomes aparecem.
    // rollSuit (Rei): a carta do chefe troca de naipe enquanto o dado gira e para em "rolledSuit".
    private IEnumerator Telegraph(BossAttack attack, int finalFace, BossAttack second = null, int secondFace = 0,
        bool rollSuit = false, Suit rolledSuit = Suit.Clubs)
    {
        Color tint = attack.Tint;
        const float landAt = 0.6f; // 60% do tempo rolando, 40% com a face à mostra
        bool landed = false;

        Vector3 diePos = transform.position + Vector3.up * (sr.bounds.extents.y + 1.2f);
        if (second != null) diePos += Vector3.left * 0.85f;
        var die = Fx.MakeSprite("TelegraphDie", config.DieFace(1), diePos, Color.white, 90, transform);
        float dieScale = 1.3f / Mathf.Max(0.01f, die.sprite != null ? die.sprite.bounds.size.x : 1f);
        die.transform.localScale = Vector3.one * dieScale;
        SpriteRenderer die2 = null;
        if (second != null)
        {
            die2 = Fx.MakeSprite("TelegraphDie2", config.DieFace(1), diePos + Vector3.right * 1.7f, Color.white, 90, transform);
            die2.transform.localScale = Vector3.one * dieScale;
        }
        string label = second != null ? attack.Label + " + " + second.Label : attack.Label;
        if (rollSuit) label += " " + SuitInfo.Symbol(rolledSuit) + " " + SuitTwistName(rolledSuit);

        // Ataque misturado: um pouco mais de tempo para ler os dois dados.
        float duration = Data.TelegraphTime * diff.TelegraphMultiplier * (second != null ? 1.2f : 1f) / (1f + Rage * 0.6f);
        float faceTimer = 0f;
        AudioManager.Sfx(config.SfxDiceShake, 0.8f);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            faceTimer -= Time.deltaTime;
            if (faceTimer <= 0f && k < landAt)
            {
                die.sprite = config.DieFace(UnityEngine.Random.Range(1, 7));
                die.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-25f, 25f));
                if (die2 != null)
                {
                    die2.sprite = config.DieFace(UnityEngine.Random.Range(1, 7));
                    die2.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-25f, 25f));
                }
                if (rollSuit) SetSuit((Suit)(((int)Suit + 1) % 4)); // a carta do Rei passa pelos naipes
                faceTimer = rollSuit ? 0.09f : 0.06f;
            }
            else if (k >= landAt)
            {
                die.sprite = config.DieFace(finalFace);
                die.transform.localRotation = Quaternion.identity;
                die.transform.localScale = Vector3.one * dieScale * (1f + 0.3f * Mathf.Sin((k - landAt) * 4f * Mathf.PI));
                if (die2 != null)
                {
                    die2.sprite = config.DieFace(secondFace);
                    die2.transform.localRotation = Quaternion.identity;
                    die2.transform.localScale = die.transform.localScale;
                }
                if (!landed)
                {
                    landed = true;
                    if (rollSuit) SetSuit(rolledSuit);
                    // Nome do ataque ao lado do dado (em cima ficaria fora da tela quando o chefe voa alto)
                    // Perto da borda direita o nome vai para a esquerda dos dados.
                    // Nome do ataque logo ABAIXO do chefe (em cima ficava atrás dos painéis do HUD).
                    // Cada letra tem ~0,58 u neste tamanho: segura o texto dentro da tela.
                    float half = label.Length * 0.29f;
                    Vector3 labelPos = transform.position + Vector3.down * (sr.bounds.extents.y + 0.9f);
                    labelPos.x = Mathf.Clamp(labelPos.x, -Arena.HalfWidth + 0.5f + half, Arena.HalfWidth - 0.5f - half);
                    Fx.Popup(label, labelPos, tint, 7f, duration * (1f - landAt) + 0.4f, 0.6f);
                    OnDieLanded?.Invoke(label);
                    // "Clack" do dado parando; com 2 dados (ataque misturado) toca de novo, mais agudo, como alerta.
                    AudioManager.Sfx(config.SfxDiceLand, 0.9f);
                    if (second != null) AudioManager.Sfx(config.SfxDiceLand, 0.7f, 0.03f, 1.25f);
                    AttackLog += label + " | "; // debug (ponte): GameManager.DebugAttackLog
                }
            }

            // Antes do dado parar o chefe pisca branco; depois, na cor do ataque escolhido.
            Color flash = landed ? tint : new Color(0.85f, 0.85f, 0.85f);
            sr.color = Color.Lerp(Color.white, flash, Mathf.PingPong(t * 6f, 1f));
            visual.localPosition = visualBase + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.12f * k);
            yield return null;
        }

        sr.color = Color.white;
        visual.localPosition = visualBase;
        Destroy(die.gameObject);
        if (die2 != null) Destroy(die2.gameObject);
    }

    // Efeito do naipe do Rei escrito junto com o nome do ataque.
    private static string SuitTwistName(Suit s) => s switch
    {
        Suit.Clubs => Loc.Pick("MORE", "MAIS", "MÁS", "PLUS", "MEHR", "PIÙ"),
        Suit.Spades => Loc.Pick("FASTER", "RÁPIDO", "RÁPIDO", "RAPIDE", "SCHNELL", "VELOCE"),
        Suit.Hearts => Loc.Pick("HEAL", "CURA", "CURA", "SOIN", "HEILT", "CURA"),
        _ => Loc.Pick("+ CHIPS", "+ FICHAS", "+ FICHAS", "+ JETONS", "+ CHIPS", "+ FICHE"),
    };

    // Leque de dados mirado no player.
    private IEnumerator Volley(int count, float speed)
    {
        const float spread = 13f;
        Vector2 origin = transform.position;
        Vector2 toPlayer = ((Vector2)player.transform.position + Vector2.up - origin).normalized;
        // Disparo: som de dado rolando, mais agudo e baixo (no Rei, com fogo, um pouco mais grave e forte).
        if (Data.DangerousDice) AudioManager.Sfx(config.SfxDiceLand, 0.7f, 0.06f, 1.05f);
        else AudioManager.Sfx(config.SfxDiceLand, 0.5f, 0.06f, 1.3f);
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) / 2f) * spread;
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * toPlayer;
            DiceProjectile.Spawn(config, UnityEngine.Random.Range(1, 7), origin, dir * speed * Data.DiceSpeedMultiplier, 0f, floorY)
                .WithTrail(Data.DiceTrailColor, Data.DangerousDice);
            yield return new WaitForSeconds(0.06f);
        }
        Fx.Shake(0.15f, 0.15f);
        yield return new WaitForSeconds(0.4f);
    }

    // Chuva de fichas: cada coluna ganha uma faixa vermelha translúcida + seta pixel art (contorno preto)
    // piscando no topo; depois as fichas (grandes, 2,5 u) caem com sombra no chão.
    private static readonly Color RainDanger = new Color32(0xFF, 0x4A, 0x3D, 0xFF);

    private IEnumerator Rain(int count, float speed)
    {
        const float spacing = 3.4f; // fichas de 2,5 u: sempre sobra um vão para desviar
        float rainEdge = Arena.HalfWidth - 2.5f; // fichas não caem coladas na parede
        var xs = new List<float> { Mathf.Clamp(player.transform.position.x, -rainEdge, rainEdge) };
        int guard = 0;
        while (xs.Count < count && guard++ < 60)
        {
            float x = UnityEngine.Random.Range(-rainEdge, rainEdge);
            bool farEnough = true;
            foreach (float other in xs) if (Mathf.Abs(other - x) < spacing) farEnough = false;
            if (farEnough) xs.Add(x);
        }

        var group = new List<Warning>();
        foreach (float x in xs)
        {
            // Embaixo dos painéis do HUD (cantos de cima) a seta desce um pouco para aparecer.
            float y = x < HudLeftX - 2f || x > HudRightX + 2f ? 6.2f : 10f;
            const float top = 11.5f;
            AddStripe(group, new Vector3(x, (top + floorY) / 2f, 0f), new Vector2(DiceProjectile.ChipRadius * 2.2f, top - floorY), RainDanger);
            AddArrow(group, new Vector3(x, y, 0f), 0f, RainDanger);
        }
        yield return BlinkWarnings(group, 0.85f);

        foreach (float x in xs)
        {
            // Chuva de fichas de poker (vermelha, preta, azul, branca); sem fichas no config, cai dado.
            Sprite chip = config.RandomChip();
            if (chip != null) DiceProjectile.SpawnChip(chip, new Vector2(x, 13f), Vector2.down * speed, floorY);
            else DiceProjectile.Spawn(config, UnityEngine.Random.Range(1, 7), new Vector2(x, 13f), Vector2.down * speed, 0f, floorY);
            yield return new WaitForSeconds(0.08f);
        }
        yield return new WaitForSeconds(0.5f);
    }

    // Investida: recua, avança rápido até onde o player estava e volta para cima.
    private IEnumerator Dash(int times, float speed)
    {
        for (int i = 0; i < times; i++)
        {
            Vector2 target = player.transform.position + Vector3.up * 1.5f;
            target.y = Mathf.Max(target.y, floorY + 2.5f);
            Vector2 start = transform.position;
            Vector2 back = start - (target - start).normalized * 1.5f;

            yield return MoveTo(back, 0.25f);
            sr.color = new Color(1f, 0.5f, 0.5f);
            AudioManager.Sfx(config.SfxDash, 0.9f, 0.05f, 0.7f); // arrancada (whoosh grave)
            while (Vector2.Distance(transform.position, target) > 0.2f)
            {
                transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }
            sr.color = Color.white;
            // Fim da investida: batida forte + tremida de câmera maior (antes era 0,3)
            AudioManager.Sfx(config.SfxBossDashImpact, 1f, 0.05f);
            Fx.Shake(0.55f, 0.28f);
            Fx.Burst(transform.position + Vector3.down * 2f, Color.white, 12, 7f, 0.3f);
            bool last = i == times - 1;
            yield return new WaitForSeconds(last ? 0.35f : 0.2f);
            if (last) yield return MoveTo(RandomArenaPoint(), 0.7f);
            else
            {
                // Entre uma investida e outra só sobe um pouco (5 seguidas não demoram uma eternidade)
                Vector2 up = new Vector2(transform.position.x, Mathf.Clamp(transform.position.y + 4f, ArenaMinY, ArenaMaxY));
                yield return MoveTo(up, 0.35f);
            }
        }
    }

    // Coringas: setas piscando na borda de onde cada um vem (sempre inclui o andar do player),
    // depois cada coringa atravessa a tela vindo de uma lateral, girando.
    private IEnumerator Jokers(int count, float speed)
    {
        var lanes = new List<int> { PlayerLane() };
        int guard = 0;
        while (lanes.Count < Mathf.Min(count, LaneTops.Length) && guard++ < 30)
        {
            int lane = UnityEngine.Random.Range(0, LaneTops.Length);
            if (!lanes.Contains(lane)) lanes.Add(lane);
        }

        var fromLeft = new List<bool>();
        var group = new List<Warning>();
        foreach (int lane in lanes)
        {
            bool left = UnityEngine.Random.value < 0.5f;
            fromLeft.Add(left);
            float y = LaneY(lane);
            // Faixa do andar inteiro + duas setas na borda apontando para onde o coringa vai.
            AddStripe(group, new Vector3(0f, y, 0f), new Vector2(Arena.HalfWidth * 2f - 1.5f, JokerProjectile.Radius * 2.3f), JokerDanger);
            float angle = left ? 90f : -90f;   // a seta base aponta para baixo: +90° = direita
            float edge = left ? -(Arena.HalfWidth - 2.4f) : Arena.HalfWidth - 2.4f, step = left ? 1.3f : -1.3f;
            AddArrow(group, new Vector3(edge, y, 0f), angle, JokerDanger);
            AddArrow(group, new Vector3(edge + step, y, 0f), angle, JokerDanger);
        }
        yield return BlinkWarnings(group, 0.9f);

        for (int i = 0; i < lanes.Count; i++)
        {
            JokerProjectile.Spawn(config.RandomJoker(), LaneY(lanes[i]), fromLeft[i], speed);
            AudioManager.SfxThrottled(config.SfxJoker, 0.5f, 0.75f); // risada do coringa (não empilha com vários)
            yield return new WaitForSeconds(0.3f);
        }
        yield return new WaitForSeconds(0.8f);
    }

    private static readonly Color JokerDanger = new Color32(0xC0, 0x6A, 0xFF, 0xFF);

    // Centro do coringa: logo acima do piso do andar (pega o player em pé; pulando dá para passar por cima).
    private static float LaneY(int lane) => LaneTops[lane] + JokerProjectile.Radius + 0.15f;

    // Andar em que o player está (o mais alto cujo topo fica abaixo dos pés dele).
    private int PlayerLane()
    {
        float feet = player.transform.position.y + 0.2f;
        int best = 0;
        for (int i = 0; i < LaneTops.Length; i++) if (LaneTops[i] <= feet) best = i;
        return best;
    }

    // ===================== Ataques novos =====================
    // Roleta: o chefe para e gira, soltando dados em DOIS braços de espiral (como uma roleta girando).
    // steps = quantos "cliques" da roleta (cada passo solta 2 dados). Sempre sobra vão entre os braços.
    private static readonly Color RouletteColor = new Color(0.45f, 1f, 0.6f);

    private IEnumerator Roulette(int steps, float speed)
    {
        Vector2 origin = transform.position;
        float angle = UnityEngine.Random.Range(0f, 360f);
        float turn = UnityEngine.Random.value < 0.5f ? 24f : -24f; // gira para um lado ou para o outro
        // Aviso: anel verde saindo do chefe
        var ring = Fx.MakeSprite("RouletteRing", Fx.Ring, origin, RouletteColor, 95);
        ring.gameObject.AddComponent<ShockRing>().Init(6f, 0.5f, 1f);
        yield return new WaitForSeconds(0.35f);
        for (int i = 0; i < steps; i++)
        {
            for (int arm = 0; arm < 2; arm++)
            {
                Vector2 dir = Quaternion.Euler(0f, 0f, angle + arm * 180f) * Vector2.right;
                DiceProjectile.Spawn(config, UnityEngine.Random.Range(1, 7), origin, dir * speed * Data.DiceSpeedMultiplier, 0f, floorY)
                    .WithTrail(RouletteColor, Data.DangerousDice);
            }
            AudioManager.SfxThrottled(config.SfxDiceLand, 0.1f, 0.45f, 0.04f, 1.3f + 0.03f * i);
            angle += turn;
            yield return new WaitForSeconds(0.11f);
        }
        yield return new WaitForSeconds(0.4f);
    }

    // Cartas Marcadas: cartas caem nos andares (uma sempre perto do player), piscam e explodem.
    private static readonly Color MarkedDanger = new Color32(0xFF, 0xD4, 0x47, 0xFF);
    private const float MarkedRadius = 1.9f;

    private IEnumerator MarkedCards(int count)
    {
        var spots = new List<Vector2>();
        int mask = LayerMask.GetMask("Ground");
        float edge = Arena.HalfWidth - 2f;
        for (int guard = 0; spots.Count < count && guard < 40; guard++)
        {
            // 1ª carta: embaixo do player; as outras, em lugares sorteados longe umas das outras
            bool first = spots.Count == 0;
            float x = first ? Mathf.Clamp(player.transform.position.x, -edge, edge) : UnityEngine.Random.Range(-edge, edge);
            float fromY = first ? player.transform.position.y + 1f : 11f;
            var hit = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, 30f, mask);
            if (hit.collider == null) continue;
            Vector2 p = hit.point + Vector2.up * 0.7f;
            bool far = true;
            foreach (var o in spots) if (Vector2.Distance(o, p) < MarkedRadius * 2f) far = false;
            if (far) spots.Add(p);
        }

        // As cartas voam do chefe até o lugar (girando) e o círculo mostra a área da explosão
        var cards = new List<SpriteRenderer>();
        var group = new List<Warning>();
        foreach (var p in spots)
        {
            var card = Fx.MakeSprite("MarkedCard", config.CardBack, transform.position, new Color(1f, 0.85f, 0.4f), 70);
            card.transform.localScale = Vector3.one * 0.3f;
            cards.Add(card);
            StartCoroutine(DropCard(card.transform, p));
            var zone = Fx.MakeSprite("MarkedZone", Fx.Circle, p, new Color(1f, 0.83f, 0.28f, 0f), -40);
            zone.transform.localScale = Vector3.one * MarkedRadius * 2f / Mathf.Max(0.01f, Fx.Circle.bounds.size.x);
            var w = new Warning { sr = zone, color = MarkedDanger, basePos = p, stripe = true };
            group.Add(w);
            warnings.Add(w);
        }
        AudioManager.Sfx(config.SfxThrow, 0.7f, 0.05f, 0.8f);
        yield return BlinkWarnings(group, 1.25f);

        foreach (var card in cards)
        {
            if (card == null) continue;
            Vector2 p = card.transform.position;
            Fx.Burst(p, MarkedDanger, 16, 10f, 0.35f);
            Fx.Burst(p, Color.white, 8, 6f, 0.25f);
            var boom = Fx.MakeSprite("MarkedBoom", Fx.Ring, p, MarkedDanger, 150);
            boom.gameObject.AddComponent<ShockRing>().Init(MarkedRadius * 2.2f, 0.3f, 1f);
            Vector2 body = (Vector2)player.transform.position + Vector2.up * 0.85f;
            if (Vector2.Distance(body, p) <= MarkedRadius) player.Damage(1, p);
            Destroy(card.gameObject);
        }
        AudioManager.Sfx(config.SfxBossDashImpact, 0.7f, 0.05f, 1.2f);
        Fx.Shake(0.35f, 0.2f);
        yield return new WaitForSeconds(0.4f);
    }

    private IEnumerator DropCard(Transform card, Vector2 target)
    {
        Vector2 start = card.position;
        for (float t = 0f; t < 0.4f && card != null; t += Time.deltaTime)
        {
            float k = t / 0.4f;
            card.position = Vector2.Lerp(start, target, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 2f;
            card.rotation = Quaternion.Euler(0f, 0f, k * 720f);
            yield return null;
        }
        if (card != null) { card.position = target; card.rotation = Quaternion.identity; }
    }

    // Debug: força o ataque dos coringas (call GameManager.DebugJokers).
    public void DebugJokers(int count) => StartCoroutine(Jokers(count, 16f));
    public void DebugRain(int count) => StartCoroutine(Rain(count, 16f));
    public void DebugVolley(int count) => StartCoroutine(Volley(count, 14f));
    public void DebugRoulette(int steps) => StartCoroutine(Roulette(steps, 9f));
    public void DebugMarked(int count) => StartCoroutine(MarkedCards(count));

    // Seta de perigo (pixel art com contorno preto, escala inteira 2×). angle 0 = aponta para baixo.
    private void AddArrow(List<Warning> group, Vector3 pos, float angle, Color color)
    {
        var sr = Fx.MakeSprite("DangerArrow", Fx.Arrow, pos, color, 96);
        sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        sr.transform.localScale = Vector3.one * 2f;
        Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.down;
        var w = new Warning { sr = sr, color = color, basePos = pos, bob = dir };
        group.Add(w);
        warnings.Add(w);
    }

    // Faixa translúcida marcando a área que vai ser atingida (fica atrás do chão e das plataformas).
    private void AddStripe(List<Warning> group, Vector3 center, Vector2 size, Color color)
    {
        var sr = Fx.MakeSprite("DangerZone", Fx.Pixel, center, new Color(color.r, color.g, color.b, 0f), -50);
        sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        var w = new Warning { sr = sr, color = color, basePos = center, stripe = true };
        group.Add(w);
        warnings.Add(w);
    }

    // Anima os avisos: setas piscam e "cutucam" na direção do perigo; faixas acendem aos poucos e pulsam,
    // piscando mais rápido no fim (o ataque está chegando). Depois remove tudo.
    // Cada ataque anima só o SEU grupo de avisos (dois ataques misturados não apagam os do outro).
    private IEnumerator BlinkWarnings(List<Warning> group, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            float blinkRate = Mathf.Lerp(5f, 12f, k);
            bool on = Mathf.Repeat(t * blinkRate, 1f) < 0.6f;
            float bob = Mathf.Round(Mathf.Abs(Mathf.Sin(t * 14f)) * 4f) / 16f; // anda de pixel em pixel (0–4 px)
            foreach (var w in group)
            {
                if (w.sr == null) continue;
                if (w.stripe)
                {
                    float a = Mathf.Lerp(0.1f, 0.3f, Mathf.Min(1f, k * 2f)) * (on ? 1f : 0.55f);
                    w.sr.color = new Color(w.color.r, w.color.g, w.color.b, a);
                }
                else
                {
                    w.sr.enabled = on;
                    w.sr.transform.position = w.basePos + (Vector3)(w.bob * bob);
                }
            }
            yield return null;
        }
        foreach (var w in group) { if (w.sr != null) Destroy(w.sr.gameObject); warnings.Remove(w); }
    }

    private void ClearWarnings()
    {
        foreach (var w in warnings) if (w.sr != null) Destroy(w.sr.gameObject);
        warnings.Clear();
    }

    // ===================== Dano =====================
    // raise: 0 = carta normal · 1 = RAISE · 2 = CALL (ver LuckyMath.ApplyRaise)
    public HitResult ReceiveCard(Card card, Vector3 hitPosition, int raise = 0)
    {
        // Coringa Carta Marcada: a 1ª carta que acerta cada chefe vira o número exato.
        bool marked = !IsDead && !intro && global::Jokers.TryUseMarked();
        if (marked)
        {
            card = new Card(card.suit, SecretNumber);
            Fx.Popup("MARKED!", hitPosition + Vector3.up * 1.2f, new Color(1f, 0.83f, 0.28f), 8f, 1f);
        }
        var r = LuckyMath.Evaluate(card, SecretNumber);
        r.marked = marked;
        LuckyMath.ApplyRaise(ref r, raise);
        if (IsDead) return r;
        if (changingPhase) { Fx.Popup("0", hitPosition + Vector3.right, Color.white, 5f, 0.9f); return r; } // trocando de fase: imune

        // Ás (dois números): acertar o 1º só TRAVA ele (dano de crítico); o 2º logo depois = ROYAL FLUSH (x1,5).
        if (Data.TwoNumbers && r.isCritical)
        {
            if (!OnSecondNumber) r.firstLocked = true;
            else { r.royalFlush = true; r.amount = Mathf.CeilToInt(r.amount * 1.5f); }
        }

        if (!r.isHeal) CurrentHp = Mathf.Max(CannotDie ? 1 : 0, CurrentHp - r.amount);
        // Dama: a vida zerou mas ainda tem naipe pela frente → troca de fase em vez de morrer.
        bool nextPhase = CurrentHp <= 0 && Phase < PhaseCount - 1;
        r.defeated = CurrentHp <= 0 && !nextPhase;

        // Feedback visual
        Vector3 top = transform.position + Vector3.up * (sr.bounds.extents.y + 0.5f);
        Color warm = LuckyMath.WarmthColor(r.distance);
        string hintText = r.hint switch
        {
            HintDirection.Higher => "▲ HIGHER",
            HintDirection.Lower => "▼ LOWER",
            _ => r.royalFlush ? "ROYAL FLUSH!" : r.firstLocked ? "1 OF 2!" : "EXACT!",
        };
        // Pista com impacto (entra grande e "bate"); mais perto do número = maior.
        float hintSize = r.isCritical ? 14f : r.distance <= 2 ? 10f : 8.5f;
        Fx.Popup(hintText, top, r.isCritical ? new Color(1f, 0.9f, 0.2f) : warm, hintSize, r.isCritical ? 1.8f : 1.3f, 1.6f,
            true, r.isCritical ? Color.white : (Color?)null);
        // ♥ com coringa: cura o jogador além do dano
        if (r.healAmount > 0) Fx.Popup($"+{r.healAmount} HP", hitPosition + Vector3.left, SuitInfo.GetColor(Suit.Hearts), 7f, 1f, 1.6f, true);
        // Com dano, o número voa até a barra de vida (BossHUD). Sem dano, só um "0" pequeno aqui.
        if (r.amount <= 0) Fx.Popup("0", hitPosition + Vector3.right, Color.white, 5f, 0.9f);
        if (r.amount > 0 && !r.isHeal && !r.isCritical) Fx.HitStop(0.05f); // congeladinha em todo acerto
        Fx.Burst(hitPosition, SuitInfo.GetColor(card.suit), r.isCritical ? 22 : 10, r.isCritical ? 12f : 7f, 0.3f);
        Fx.Shake(r.isCritical ? 0.75f : 0.12f + 0.06f * r.amount, r.isCritical ? 0.45f : 0.15f);
        if (r.isCritical)
        {
            // Número exato: congela um instante, clarão dourado, anel de choque e faíscas brancas.
            Fx.HitStop(0.12f);
            ScreenFlash.Play(new Color(1f, 0.92f, 0.55f), 0.35f, 0.3f);
            var ring = Fx.MakeSprite("CritRing", Fx.Ring, transform.position, new Color(1f, 0.9f, 0.3f), 150);
            ring.gameObject.AddComponent<ShockRing>().Init(12f, 0.45f, 1f);
            Fx.Burst(transform.position, Color.white, 16, 14f, 0.25f);
        }
        if (r.amount > 0 && !r.isHeal && !r.defeated) StartCoroutine(HurtFlash()); // golpe final: sem brilho (o chefe só morre)
        if (r.nearMiss) Fx.Popup("NEAR MISS!", top + Vector3.up * 1.4f, new Color(1f, 0.54f, 0.24f), 8f, 1.2f);
        // RAISE: ganhou (dano multiplicado) ou estourou (o player leva dano)
        if (r.raiseWon)
        {
            Fx.Popup($"x{LuckyMath.RaiseMultiplier(r.raise)}!", top + Vector3.up * 2.4f, new Color(1f, 0.83f, 0.28f), 13f, 1.5f, 1.6f, true, Color.white);
            ScreenFlash.Play(new Color(1f, 0.83f, 0.3f), 0.25f, 0.25f);
        }
        else if (r.raiseBust > 0)
        {
            Fx.Popup("BUSTED!", player.transform.position + Vector3.up * 2.6f, new Color(1f, 0.3f, 0.3f), 10f, 1.4f, 1.6f, true);
            player.Damage(r.raiseBust, hitPosition);
        }

        OnHit?.Invoke(r);
        OnHealthChanged?.Invoke(CurrentHp, Data.MaxHp);

        if (r.defeated)
        {
            Die();
        }
        else if (nextPhase)
        {
            StartCoroutine(NextPhase());
        }
        else if (Data.SuitMode == BossSuitMode.Phases && Suit == Suit.Hearts && r.amount <= 0)
        {
            HealSelf(1); // Dama de Copas: chutar longe do número cura ela
        }
        else if (r.firstLocked)
        {
            // Ás: o 1º número fica travado à mostra e o jogo passa a valer o 2º
            LockedNumber = SecretNumber;
            SecretNumber = SecondNumber;
            StartCoroutine(FlipCard());
            OnNumberChanged?.Invoke();
            Fx.Popup("NUMBER 2!", transform.position + Vector3.up * (sr.bounds.extents.y + 1.6f), RouletteColor, 11f, 1.8f);
        }
        else if (r.isCritical)
        {
            StartCoroutine(Reroll(null, 0.3f)); // crítico: gira logo depois do som do crítico
        }
        else if (Data.RerollAtHalfHp && !halfRerollDone && CurrentHp <= Data.MaxHp / 2)
        {
            halfRerollDone = true;
            StartCoroutine(Reroll("SHUFFLE!"));
        }
        return r;
    }

    // Clarão branco: uma luz 2D forte em cima do chefe por um instante (o sprite "estoura" de branco),
    // depois pisca vermelho.
    private Light2D flashLight;

    private IEnumerator HurtFlash()
    {
        if (flashLight == null)
        {
            flashLight = new GameObject("HitFlash").AddComponent<Light2D>();
            flashLight.transform.SetParent(transform, false);
            flashLight.lightType = Light2D.LightType.Point;
            flashLight.pointLightOuterRadius = 4.5f;
            flashLight.pointLightInnerRadius = 3f;
            flashLight.color = Color.white;
        }
        flashLight.intensity = 5f;
        yield return new WaitForSecondsRealtime(0.07f);
        flashLight.intensity = 0f;
        for (int i = 0; i < 2; i++)
        {
            sr.color = new Color(1f, 0.35f, 0.35f);
            yield return new WaitForSeconds(0.06f);
            sr.color = Color.white;
            yield return new WaitForSeconds(0.05f);
        }
    }

    // Sorteia um novo número secreto (com um dado girando acima da cabeça).
    public void ForceReroll() => StartCoroutine(Reroll("SHUFFLE!")); // só debug

    // dieTime = quanto tempo o dado gira antes da carta virar (crítico é mais rápido).
    private IEnumerator Reroll(string message, float dieTime = 0.7f)
    {
        if (rerolling) yield break;
        rerolling = true;
        if (message != null)
            Fx.Popup(message, transform.position + Vector3.up * (sr.bounds.extents.y + 2f), new Color(0.8f, 0.6f, 1f), 9f, 1.4f);

        Vector3 pos = transform.position + Vector3.down * (sr.bounds.extents.y + 0.8f);
        var die = Fx.MakeSprite("RerollDie", config.DieFace(1), pos, Color.white, 90, transform);
        // No crítico não toca nada aqui (o som do crítico ainda está tocando); nos outros casos, o dado chacoalha.
        if (message != null) AudioManager.Sfx(config.SfxDiceShake, 0.7f);
        die.transform.localScale = Vector3.one * (1.1f / Mathf.Max(0.01f, die.sprite != null ? die.sprite.bounds.size.x : 1f));
        for (float t = 0f; t < dieTime; t += Time.deltaTime)
        {
            die.sprite = config.DieFace(UnityEngine.Random.Range(1, 7));
            die.transform.Rotate(0f, 0f, 900f * Time.deltaTime);
            yield return null;
        }
        Destroy(die.gameObject);
        StartCoroutine(FlipCard()); // aviso visual: a carta do chefe gira (vira o verso e volta)

        RollNumbers(); // (no Ás, os dois números mudam e a trava some)
        rerolling = false;
        OnNumberChanged?.Invoke();
        // Destaque: o número mudou, as pistas antigas não valem mais.
        Fx.Popup("NEW NUMBER!", transform.position + Vector3.up * (sr.bounds.extents.y + 1.6f), new Color(1f, 0.42f, 0.18f), 11f, 1.8f);
        Fx.Shake(0.25f, 0.2f);
    }

    // Carta do chefe girando no próprio eixo (como virar uma carta na mesa): a escala X segue um
    // cosseno, então a carta "afina" até sumir de lado e volta. Quando o cosseno fica negativo
    // estamos vendo as COSTAS da carta → troca o sprite pelo verso. 2 voltas rápidas + um "pulo"
    // de tamanho e um brilho branco, para o jogador perceber que o número mudou.
    private bool flipping;
    private Sprite flipFront; // frente da carta guardada durante o giro (para restaurar se morrer no meio)
    private IEnumerator FlipCard()
    {
        if (flipping || sr == null) yield break;
        flipping = true;
        flipFront = sr.sprite; // SetSuit pode trocar a frente durante o giro
        AudioManager.Sfx(config.SfxNewNumber, 0.85f, 0f); // o som da troca de número toca junto com o giro
        const float duration = 0.5f, turns = 2f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            float ease = 1f - (1f - k) * (1f - k);               // começa rápido e desacelera
            float cos = Mathf.Cos(ease * turns * 2f * Mathf.PI);
            float punch = 1f + 0.25f * Mathf.Sin(k * Mathf.PI);  // cresce no meio e volta
            // Largura mínima de 0,05 para a carta não sumir de vez (fica uma "linha" no meio do giro)
            float width = Mathf.Max(0.05f, Mathf.Abs(cos));
            visual.localScale = new Vector3(width * punch, punch, 1f);
            if (config.CardBack != null) sr.sprite = cos < 0f ? config.CardBack : flipFront;
            sr.color = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.6f), Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        visual.localScale = Vector3.one;
        sr.sprite = flipFront;
        sr.color = Color.white;
        flipping = false;
    }

    // Dama: a vida zerou → troca para o próximo naipe, enche a vida e sorteia um número novo.
    // Fica imune durante a troca (a carta gira e o naipe muda no meio do giro).
    private IEnumerator NextPhase()
    {
        changingPhase = true;
        Phase++;
        Suit next = Phase < phaseOrder.Count ? phaseOrder[Phase] : SuitInfo.Random();
        ClearWarnings();
        Fx.Shake(0.5f, 0.35f);
        ScreenFlash.Play(SuitInfo.GetColor(next), 0.3f, 0.35f);
        var ring = Fx.MakeSprite("PhaseRing", Fx.Ring, transform.position, SuitInfo.GetColor(next), 150);
        ring.gameObject.AddComponent<ShockRing>().Init(12f, 0.5f, 1f);
        StartCoroutine(FlipCard());
        yield return new WaitForSeconds(0.25f);
        SetSuit(next);
        RollNumbers();
        CurrentHp = Data.MaxHp;
        halfRerollDone = false;
        nextPhaseShuffle = Time.time + 14f;
        OnHealthChanged?.Invoke(CurrentHp, Data.MaxHp);
        OnNumberChanged?.Invoke();
        string phaseText = Loc.Pick("PHASE", "FASE", "FASE", "PHASE", "PHASE", "FASE") + $" {Phase + 1}/{PhaseCount}  {SuitInfo.Symbol(next)}";
        Fx.Popup(phaseText, transform.position + Vector3.up * (sr.bounds.extents.y + 1.6f), SuitInfo.GetColor(next), 12f, 2f, 1.6f, true, Color.white);
        Fx.Popup(PhaseRuleText(next), transform.position + Vector3.down * (sr.bounds.extents.y + 1f), Color.white, 7f, 2.4f);
        yield return new WaitForSeconds(0.9f);
        changingPhase = false;
    }

    // O que muda em cada naipe da Dama (aparece embaixo dela na troca de fase).
    public static string PhaseRuleText(Suit s) => s switch
    {
        Suit.Spades => Loc.Pick("SPADES: FASTER", "ESPADAS: MAIS RÁPIDA", "ESPADAS: MÁS RÁPIDA", "PIQUE : PLUS RAPIDE", "PIK: SCHNELLER", "PICCHE: PIÙ VELOCE"),
        Suit.Hearts => Loc.Pick("HEARTS: MISSES HEAL HER", "COPAS: ERRAR CURA ELA", "CORAZONES: FALLAR LA CURA", "CŒUR : RATER LA SOIGNE", "HERZ: FEHLWÜRFE HEILEN SIE", "CUORI: SBAGLIARE LA CURA"),
        Suit.Diamonds => Loc.Pick("DIAMONDS: NUMBER SHUFFLES", "OUROS: NÚMERO EMBARALHA", "DIAMANTES: EL NÚMERO SE BARAJA", "CARREAU : LE NOMBRE CHANGE", "KARO: ZAHL WECHSELT", "QUADRI: IL NUMERO CAMBIA"),
        _ => Loc.Pick("CLUBS: STEADY", "PAUS: NORMAL", "TRÉBOLES: NORMAL", "TRÈFLE : NORMAL", "KREUZ: NORMAL", "FIORI: NORMALE"),
    };

    // Tutorial: ao voltar uma dica o Coringa recupera a vida.
    public void RestoreFullHp()
    {
        if (IsDead) return;
        CurrentHp = Data.MaxHp;
        OnHealthChanged?.Invoke(CurrentHp, Data.MaxHp);
    }

    private void Die()
    {
        IsDead = true;
        StopAllCoroutines(); // a IA e os ataques que rodam em paralelo (misturados)
        // O StopAllCoroutines pode parar o HurtFlash no meio, com a luz branca ligada: apaga aqui.
        if (flashLight != null) flashLight.intensity = 0f;
        if (flipping) { visual.localScale = Vector3.one; sr.sprite = flipFront; flipping = false; } // morreu girando
        sr.color = Color.white;
        ClearWarnings();
        if (Partner != null && !Partner.IsDead) Partner.Avenge(); // gêmeos: o outro fica com raiva
        OnDefeated?.Invoke();
        StartCoroutine(DeathAnimation());
    }

    private IEnumerator DeathAnimation()
    {
        Fx.Shake(0.7f, 0.6f);
        for (int i = 0; i < 6; i++)
        {
            Fx.Burst(transform.position + (Vector3)UnityEngine.Random.insideUnitCircle * 2f, i % 2 == 0 ? Color.white : new Color(1f, 0.8f, 0.3f), 14, 10f, 0.35f);
            yield return new WaitForSeconds(0.12f);
        }
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            transform.position += Vector3.down * 6f * Time.deltaTime;
            visual.Rotate(0f, 0f, 120f * Time.deltaTime);
            sr.color = new Color(1f, 1f, 1f, 1f - t / 1.2f);
            yield return null;
        }
        Destroy(gameObject);
    }

    // Encostar no chefe machuca.
    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsDead || intro) return;
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null) health.Damage(1, transform.position);
    }

    private void OnDestroy() => ClearWarnings();
}
