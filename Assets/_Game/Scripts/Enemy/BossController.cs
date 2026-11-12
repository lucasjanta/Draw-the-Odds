using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    public int SecretNumber { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsVulnerable => !IsDead && !intro;

    public event Action<HitResult> OnHit;
    public event Action<int, int> OnHealthChanged;
    public event Action OnNumberChanged;
    public event Action OnDefeated;
    public event Action OnTelegraph;      // começou a antecipação de um ataque (tutorial usa)
    public event Action OnAttackFinished;
    public event Action<string> OnDieLanded; // o dado parou: nome do ataque escolhido (ex.: "DICE x3")

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
    private readonly List<GameObject> warnings = new List<GameObject>();

    public static BossController Spawn(GameConfig config, BossData data, Suit suit, PlayerHealth player, float floorY)
    {
        var go = new GameObject("Boss " + data.DisplayName);
        go.layer = LayerMask.NameToLayer("Enemy");
        go.transform.position = new Vector3(0f, 16f, 0f);

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;

        var boss = go.AddComponent<BossController>();
        boss.config = config;
        boss.Data = data;
        boss.Suit = suit;
        boss.player = player;
        boss.floorY = floorY;

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
        boss.SecretNumber = UnityEngine.Random.Range(data.MinNumber, data.MaxNumber + 1);
        return boss;
    }

    private void Start()
    {
        visualBase = visual.localPosition;
        brain = StartCoroutine(Brain());
    }

    private void Update()
    {
        if (IsDead || intro || Data.RerollEverySeconds <= 0f) return;
        rerollTimer += Time.deltaTime;
        if (rerollTimer >= Data.RerollEverySeconds && !rerolling)
        {
            rerollTimer = 0f;
            StartCoroutine(Reroll("SHUFFLE!"));
        }
    }

    // ===================== IA =====================
    private IEnumerator Brain()
    {
        yield return MoveTo(new Vector2(0f, 6f), 1.4f);
        intro = false;

        while (!IsDead)
        {
            if (!AttacksEnabled)
            {
                yield return Wander(0.5f);
                continue;
            }
            yield return Wander(Data.IdleTime * UnityEngine.Random.Range(0.8f, 1.2f));
            if (!AttacksEnabled) continue; // o tutorial pode desligar os ataques durante o passeio

            // O dado decide o ataque: cada face é um ataque (ou uma variação mais forte/fraca dele).
            int face = UnityEngine.Random.Range(1, 7);
            BossAttack attack = Data.AttackForFace(face);
            if (attack.Type == BossAttackType.Jokers && config.RandomJoker() == null)
                attack = new BossAttack(BossAttackType.Volley, attack.Count, attack.Speed);
            OnTelegraph?.Invoke();
            yield return Telegraph(attack, face);

            switch (attack.Type)
            {
                case BossAttackType.Volley: yield return Volley(attack.Count, attack.Speed); break;
                case BossAttackType.Rain: yield return Rain(attack.Count, attack.Speed); break;
                case BossAttackType.Dash: yield return Dash(attack.Count, attack.Speed); break;
                case BossAttackType.Jokers: yield return Jokers(attack.Count, attack.Speed); break;
            }
            OnAttackFinished?.Invoke();
        }
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
            Vector2 pos = Vector2.MoveTowards(transform.position, dest, Data.MoveSpeed * Time.deltaTime);
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
    private IEnumerator Telegraph(BossAttack attack, int finalFace)
    {
        Color tint = attack.Tint;
        const float landAt = 0.6f; // 60% do tempo rolando, 40% com a face à mostra
        bool landed = false;

        Vector3 diePos = transform.position + Vector3.up * (sr.bounds.extents.y + 1.2f);
        var die = Fx.MakeSprite("TelegraphDie", config.DieFace(1), diePos, Color.white, 90, transform);
        float dieScale = 1.3f / Mathf.Max(0.01f, die.sprite != null ? die.sprite.bounds.size.x : 1f);
        die.transform.localScale = Vector3.one * dieScale;

        float duration = Data.TelegraphTime;
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
                faceTimer = 0.06f;
            }
            else if (k >= landAt)
            {
                die.sprite = config.DieFace(finalFace);
                die.transform.localRotation = Quaternion.identity;
                die.transform.localScale = Vector3.one * dieScale * (1f + 0.3f * Mathf.Sin((k - landAt) * 4f * Mathf.PI));
                if (!landed)
                {
                    landed = true;
                    // Nome do ataque ao lado do dado (em cima ficaria fora da tela quando o chefe voa alto)
                    Vector3 labelPos = die.transform.position + (die.transform.position.x > 10f ? Vector3.left : Vector3.right) * 3f;
                    Fx.Popup(attack.Label, labelPos, tint, 7f, duration * (1f - landAt) + 0.4f, 0.6f);
                    OnDieLanded?.Invoke(attack.Label);
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
    }

    // Leque de dados mirado no player.
    private IEnumerator Volley(int count, float speed)
    {
        const float spread = 13f;
        Vector2 origin = transform.position;
        Vector2 toPlayer = ((Vector2)player.transform.position + Vector2.up - origin).normalized;
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) / 2f) * spread;
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * toPlayer;
            DiceProjectile.Spawn(config, UnityEngine.Random.Range(1, 7), origin, dir * speed, 0f, floorY);
            yield return new WaitForSeconds(0.06f);
        }
        Fx.Shake(0.15f, 0.15f);
        yield return new WaitForSeconds(0.4f);
    }

    // Chuva de fichas: setas piscando na borda de cima marcam as colunas, depois as fichas caem.
    private IEnumerator Rain(int count, float speed)
    {
        var xs = new List<float> { Mathf.Clamp(player.transform.position.x, -18f, 18f) };
        int guard = 0;
        while (xs.Count < count && guard++ < 50)
        {
            float x = UnityEngine.Random.Range(-18f, 18f);
            bool farEnough = true;
            foreach (float other in xs) if (Mathf.Abs(other - x) < 2.5f) farEnough = false;
            if (farEnough) xs.Add(x);
        }

        var color = new Color(1f, 0.3f, 0.35f);
        foreach (float x in xs)
        {
            // Embaixo dos painéis do HUD (cantos de cima) a seta desce um pouco para aparecer.
            float y = x < HudLeftX - 2f || x > HudRightX + 2f ? 6.4f : 10.3f;
            EdgeArrow("\u25BC", new Vector3(x, y, 0f), color);
        }
        yield return BlinkWarnings(0.85f, color);

        foreach (float x in xs)
        {
            // Chuva de fichas de poker (vermelha, preta, azul, branca); sem fichas no config, cai dado.
            Sprite chip = config.RandomChip();
            if (chip != null) DiceProjectile.SpawnChip(chip, new Vector2(x, 12.5f), Vector2.down * speed, floorY);
            else DiceProjectile.Spawn(config, UnityEngine.Random.Range(1, 7), new Vector2(x, 12.5f), Vector2.down * speed, 0f, floorY);
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
            while (Vector2.Distance(transform.position, target) > 0.2f)
            {
                transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }
            sr.color = Color.white;
            Fx.Shake(0.3f, 0.2f);
            Fx.Burst(transform.position + Vector3.down * 2f, Color.white, 8, 6f, 0.25f);
            yield return new WaitForSeconds(0.35f);
            yield return MoveTo(RandomArenaPoint(), 0.7f);
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
        foreach (int lane in lanes)
        {
            bool left = UnityEngine.Random.value < 0.5f;
            fromLeft.Add(left);
            float y = LaneY(lane);
            EdgeArrow(left ? "\u25BA\u25BA" : "\u25C4\u25C4", new Vector3(left ? -18.4f : 18.4f, y, 0f), new Color(0.85f, 0.5f, 1f));
        }
        yield return BlinkWarnings(0.9f, new Color(0.85f, 0.5f, 1f));

        for (int i = 0; i < lanes.Count; i++)
        {
            JokerProjectile.Spawn(config.RandomJoker(), LaneY(lanes[i]), fromLeft[i], speed);
            yield return new WaitForSeconds(0.3f);
        }
        yield return new WaitForSeconds(0.8f);
    }

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

    // Debug: força o ataque dos coringas (call GameManager.DebugJokers).
    public void DebugJokers(int count) => StartCoroutine(Jokers(count, 16f));
    public void DebugRain(int count) => StartCoroutine(Rain(count, 16f));

    // Aviso discreto: seta na borda da tela (some junto com ClearWarnings).
    private void EdgeArrow(string glyph, Vector3 pos, Color color)
    {
        var t = Fx.WorldText(glyph, pos, 9f, color, 95);
        warnings.Add(t.gameObject);
    }

    // Faz as setas piscarem (liga/desliga rápido) e depois as remove.
    private IEnumerator BlinkWarnings(float duration, Color color)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            bool on = Mathf.Repeat(t * 7f, 1f) < 0.6f;
            float pulse = 1f + 0.15f * Mathf.Sin(t * 20f);
            foreach (var w in warnings)
            {
                if (w == null) continue;
                var tmp = w.GetComponent<TMPro.TextMeshPro>();
                if (tmp != null) tmp.color = new Color(color.r, color.g, color.b, on ? 1f : 0.15f);
                w.transform.localScale = Vector3.one * pulse;
            }
            yield return null;
        }
        ClearWarnings();
    }

    private void ClearWarnings()
    {
        foreach (var w in warnings) if (w != null) Destroy(w);
        warnings.Clear();
    }

    // ===================== Dano =====================
    public HitResult ReceiveCard(Card card, Vector3 hitPosition)
    {
        var r = LuckyMath.Evaluate(card, SecretNumber);
        if (IsDead) return r;

        if (!r.isHeal) CurrentHp = Mathf.Max(CannotDie ? 1 : 0, CurrentHp - r.amount);
        r.defeated = CurrentHp <= 0;

        // Feedback visual
        Vector3 top = transform.position + Vector3.up * (sr.bounds.extents.y + 0.5f);
        Color warm = LuckyMath.WarmthColor(r.distance);
        string hintText = r.hint switch
        {
            HintDirection.Higher => "▲ HIGHER",
            HintDirection.Lower => "▼ LOWER",
            _ => "EXACT!",
        };
        Fx.Popup(hintText, top, r.isCritical ? new Color(1f, 0.9f, 0.2f) : warm, r.isCritical ? 11f : 8f, 1.3f);
        // Copas só cura se a carta estiver perto do número (mesma tabela do dano): longe = "NO HEAL".
        if (r.isHeal) Fx.Popup(r.amount > 0 ? $"+{r.amount} HP" : "NO HEAL", hitPosition + Vector3.left, SuitInfo.GetColor(Suit.Hearts), r.amount > 0 ? 6f : 5f, 1f);
        else Fx.Popup(r.amount > 0 ? $"-{r.amount}" : "0", hitPosition + Vector3.right, Color.white, r.amount > 0 ? 7f : 5f, 0.9f);
        Fx.Burst(hitPosition, SuitInfo.GetColor(card.suit), r.isCritical ? 22 : 10, r.isCritical ? 12f : 7f, 0.3f);
        Fx.Shake(r.isCritical ? 0.5f : 0.12f + 0.06f * r.amount, r.isCritical ? 0.35f : 0.15f);
        if (r.amount > 0 && !r.isHeal) StartCoroutine(HurtFlash());

        OnHit?.Invoke(r);
        OnHealthChanged?.Invoke(CurrentHp, Data.MaxHp);

        if (r.defeated)
        {
            Die();
        }
        else if (r.isCritical)
        {
            StartCoroutine(Reroll(null));
        }
        else if (Data.RerollAtHalfHp && !halfRerollDone && CurrentHp <= Data.MaxHp / 2)
        {
            halfRerollDone = true;
            StartCoroutine(Reroll("SHUFFLE!"));
        }
        return r;
    }

    private IEnumerator HurtFlash()
    {
        for (int i = 0; i < 2; i++)
        {
            sr.color = new Color(1f, 0.35f, 0.35f);
            yield return new WaitForSeconds(0.06f);
            sr.color = Color.white;
            yield return new WaitForSeconds(0.05f);
        }
    }

    // Sorteia um novo número secreto (com um dado girando acima da cabeça).
    private IEnumerator Reroll(string message)
    {
        if (rerolling) yield break;
        rerolling = true;
        if (message != null)
            Fx.Popup(message, transform.position + Vector3.up * (sr.bounds.extents.y + 2f), new Color(0.8f, 0.6f, 1f), 9f, 1.4f);

        Vector3 pos = transform.position + Vector3.down * (sr.bounds.extents.y + 0.8f);
        var die = Fx.MakeSprite("RerollDie", config.DieFace(1), pos, Color.white, 90, transform);
        AudioManager.Sfx(config.SfxDiceShake, 0.8f);
        die.transform.localScale = Vector3.one * (1.1f / Mathf.Max(0.01f, die.sprite != null ? die.sprite.bounds.size.x : 1f));
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            die.sprite = config.DieFace(UnityEngine.Random.Range(1, 7));
            die.transform.Rotate(0f, 0f, 900f * Time.deltaTime);
            yield return null;
        }
        Destroy(die.gameObject);

        int old = SecretNumber;
        while (SecretNumber == old && Data.MaxNumber > Data.MinNumber)
            SecretNumber = UnityEngine.Random.Range(Data.MinNumber, Data.MaxNumber + 1);
        rerolling = false;
        OnNumberChanged?.Invoke();
        // Destaque: o número mudou, as pistas antigas não valem mais.
        Fx.Popup("NEW NUMBER!", transform.position + Vector3.up * (sr.bounds.extents.y + 1.6f), new Color(1f, 0.42f, 0.18f), 11f, 1.8f);
        Fx.Shake(0.25f, 0.2f);
    }

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
        if (brain != null) StopCoroutine(brain);
        ClearWarnings();
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
