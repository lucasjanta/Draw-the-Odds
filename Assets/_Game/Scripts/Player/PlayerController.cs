using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpVelocity = 19f;
    [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.5f;
    [SerializeField] private float maxFallSpeed = 25f;

    [Header("Tolerâncias (game feel)")]
    [SerializeField] private float coyoteTime = 0.1f; // tempo que o player pode pular depois de sair do chão
    [SerializeField] private float jumpBufferTime = 0.12f; // tempo que o player pode apertar o botão de pulo antes de tocar o chão

    [Header("Chão")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.7f, 0.1f);
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float dropThroughTime = 0.5f; // tempo que o player pode apertar o botão de pulo para cair através do chão

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 24f;
    [SerializeField] private float dashTime = 0.16f;
    [SerializeField] private float dashCooldown = 1.4f;   // "relativamente longo"
    [SerializeField] private Color afterImageColor = new Color(0.45f, 0.85f, 1f, 0.55f);

    [Header("Debug (só leitura)")]
    [SerializeField] private string currentStateName;

    // ---------- O que os estados podem ler ----------
    public Rigidbody2D Body { get; private set; }
    public Vector2 MoveInput { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool FacingRight { get; private set; } = true;

    // Propriedades calculadas ("=>"): recalculadas toda vez que alguém lê.
    public bool JumpBuffered => Time.time - lastJumpPressedTime <= jumpBufferTime;
    public bool CanUseCoyote => Time.time - lastGroundedTime <= coyoteTime;

    // ---------- Estados (criados 1x, reutilizados) ----------
    public PlayerIdleState IdleState { get; private set; }
    public PlayerRunState RunState { get; private set; }
    public PlayerJumpState JumpState { get; private set; }
    public PlayerFallState FallState { get; private set; }
    public PlayerDashState DashState { get; private set; }

    // ---------- Dash ----------
    public bool IsDashing => stateMachine.Current == DashState;
    public float DashTime => dashTime;
    public float DashCooldown01 => Mathf.Clamp01((Time.time - lastDashTime) / dashCooldown); // 1 = pronto

    // Eventos usados pelo tutorial
    public event Action OnJumped;
    public event Action OnDroppedThrough;
    public event Action OnDashed;

    // ---------- Internos ----------
    private Collider2D bodyCollider;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private InputSystem_Actions input;
    private StateMachine stateMachine;
    private Collider2D groundUnderFeet;
    private string currentAnimation;
    private string desiredAnimation;      // animação que o estado quer (guardada durante um "one shot")
    private float oneShotUntil;           // até quando uma animação especial (arremesso/dano) tem prioridade
    private float knockbackUntil;         // durante o knockback o input não controla o X
    private float faceOverrideUntil;
    private bool faceOverrideRight;

    // Controles ligados? (desligado na morte, nas transições e na tela inicial)
    public bool ControlsEnabled { get; set; } = true;
    // Input congelado temporariamente (tempo parado na fusão, pausas do tutorial).
    public bool InputFrozen { get; set; }

    // NegativeInfinity = "nunca aconteceu" (Time.time - (-inf) = +inf, nunca é <= tolerância)
    private float lastJumpPressedTime = float.NegativeInfinity;
    private float lastGroundedTime = float.NegativeInfinity;
    private float dropUntilTime;
    private float lastDashPressedTime = float.NegativeInfinity;
    private float lastDashTime = float.NegativeInfinity;
    private float savedGravity;

    // ================= Ciclo de vida =================
    private void Awake()
    {
        Body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        animator = GetComponentInChildren<Animator>();             // está no filho "Visual"
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        input = new InputSystem_Actions();

        // "this" = este PlayerController. Cada estado guarda a referência para usar.
        IdleState = new PlayerIdleState(this);
        RunState = new PlayerRunState(this);
        JumpState = new PlayerJumpState(this);
        FallState = new PlayerFallState(this);
        DashState = new PlayerDashState(this);

        stateMachine = new StateMachine();
    }

    private void OnEnable() => input.Player.Enable();
    private void OnDisable() => input.Player.Disable();
    private void OnDestroy() => input.Dispose();

    private void Start()
    {
        stateMachine.ChangeState(IdleState);
    }

    private void Update()
    {
        ReadInput();
        if (Time.time >= oneShotUntil && desiredAnimation != null && desiredAnimation != currentAnimation)
            PlayAnimationNow(desiredAnimation);
        CheckGround();
        UpdateFacing();
        stateMachine.Tick();

        // GetType().Name = nome da classe do estado atual, ex.: "PlayerRunState"
        currentStateName = stateMachine.Current?.GetType().Name;
    }

    private void FixedUpdate()
    {
        stateMachine.FixedTick();
    }

    // ================= Sensores =================
    private void ReadInput()
    {
        if (!ControlsEnabled || InputFrozen)
        {
            MoveInput = Vector2.zero;
            JumpHeld = false;
            return;
        }

        MoveInput = input.Player.Move.ReadValue<Vector2>();
        JumpHeld = input.Player.Jump.IsPressed();

        if (input.Player.Sprint.WasPressedThisFrame())   // "Sprint" = Shift no InputSystem_Actions
            lastDashPressedTime = Time.time;

        if (input.Player.Jump.WasPressedThisFrame())
            lastJumpPressedTime = Time.time; // guarda QUANDO apertou (jump buffer)
    }

    private void CheckGround()
    {
        // Caixa invisível nos pés: tem algo da layer Ground encostando?
        Collider2D hit = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);

        // Só "pousa" se não estiver subindo (atravessando plataforma one-way por baixo)
        // e se não estiver no meio de um "descer da plataforma".
        bool canLand = Body.linearVelocity.y <= 0.1f && Time.time >= dropUntilTime;

        IsGrounded = hit != null && canLand;
        groundUnderFeet = IsGrounded ? hit : null; // operador ternário: cond ? seVerdade : seFalso

        if (IsGrounded)
            lastGroundedTime = Time.time;
    }

    private void UpdateFacing()
    {
        if (Time.time < faceOverrideUntil) FacingRight = faceOverrideRight;
        else if (MoveInput.x > 0.01f) FacingRight = true;
        else if (MoveInput.x < -0.01f) FacingRight = false;

        spriteRenderer.flipX = !FacingRight; // o sprite original olha para a direita
    }

    // ================= Ações (os estados chamam) =================
    public void ChangeState(IState next) => stateMachine.ChangeState(next);

    public void PlayAnimation(string clipName)
    {
        desiredAnimation = clipName;
        if (Time.time < oneShotUntil) return;      // uma animação especial está tocando: espera ela acabar
        if (currentAnimation == clipName) return;  // evita reiniciar o clip todo frame
        PlayAnimationNow(clipName);
    }

    private void PlayAnimationNow(string clipName)
    {
        animator.Play(clipName, 0, 0f);
        currentAnimation = clipName;
    }

    // Toca uma animação por cima do estado atual por "duration" segundos (ex.: arremesso).
    public void PlayOneShot(string clipName, float duration)
    {
        oneShotUntil = Time.time + duration;
        PlayAnimationNow(clipName);
    }

    // Vira o personagem para um lado por um tempo (para arremessar na direção do mouse).
    public void FaceTowards(float directionX, float duration)
    {
        faceOverrideRight = directionX >= 0f;
        faceOverrideUntil = Time.time + duration;
    }

    // Empurrão ao levar dano: o input horizontal fica ignorado por "lockTime".
    public void Knockback(Vector2 velocity, float lockTime)
    {
        Body.linearVelocity = velocity;
        knockbackUntil = Time.time + lockTime;
    }

    // Teleporta (início de cada chefe).
    public void ResetTo(Vector2 position)
    {
        Body.position = position;
        transform.position = position;
        Body.linearVelocity = Vector2.zero;
        knockbackUntil = 0f;
        ChangeState(IdleState);
    }

    // Morte: trava os controles e congela a animação "die" no último quadro.
    public void Die()
    {
        ControlsEnabled = false;
        knockbackUntil = float.PositiveInfinity;
        Body.linearVelocity = new Vector2(0f, Body.linearVelocity.y);
        PlayOneShot("die", float.PositiveInfinity);
        StartCoroutine(FreezeAnimatorAfter(0.7f));
    }

    private IEnumerator FreezeAnimatorAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        animator.speed = 0f;
    }

    public void ApplyHorizontalMovement()
    {
        if (Time.time < knockbackUntil) return; // empurrado: deixa a física levar
        float x = MoveInput.x * moveSpeed;
        float y = Mathf.Max(Body.linearVelocity.y, -maxFallSpeed); // limita a velocidade de queda
        Body.linearVelocity = new Vector2(x, y);
    }

    public void DoJump()
    {
        OnJumped?.Invoke();
        Body.linearVelocity = new Vector2(Body.linearVelocity.x, jumpVelocity);
        // "Consome" o buffer e o coyote para não pular 2x
        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity;
    }

    public void CutJump()
    {
        if (Body.linearVelocity.y > 0f)
            Body.linearVelocity = new Vector2(Body.linearVelocity.x, Body.linearVelocity.y * jumpCutMultiplier);
    }

    // S + Espaço em cima de plataforma one-way → atravessa para baixo.
    public bool TryDropThrough()
    {
        bool wantsDrop = MoveInput.y < -0.5f && JumpBuffered;
        if (!wantsDrop || groundUnderFeet == null) return false;

        // Só plataformas com PlatformEffector2D (o chão sólido não deixa descer).
        // "out _" = descarta o component encontrado; só queremos saber se existe.
        if (!groundUnderFeet.TryGetComponent(out PlatformEffector2D _)) return false;

        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity;   // sem coyote depois de descer
        dropUntilTime = Time.time + dropThroughTime;
        OnDroppedThrough?.Invoke();
        StartCoroutine(IgnorePlatformFor(groundUnderFeet, dropThroughTime));
        return true;
    }

    // ================= Dash =================
    // Os estados chamam isto: se o Shift foi apertado e o cooldown acabou, entra no DashState.
    public bool TryStartDash()
    {
        bool pressed = Time.time - lastDashPressedTime <= 0.1f;
        if (!pressed || Time.time < lastDashTime + dashCooldown || !ControlsEnabled) return false;
        ChangeState(DashState);
        return true;
    }

    public void BeginDash()
    {
        lastDashPressedTime = float.NegativeInfinity;
        lastDashTime = Time.time;
        // Direção: para onde está apertando; parado = para onde está olhando.
        float dir = Mathf.Abs(MoveInput.x) > 0.01f ? Mathf.Sign(MoveInput.x) : (FacingRight ? 1f : -1f);
        FaceTowards(dir, dashTime);
        savedGravity = Body.gravityScale;
        Body.gravityScale = 0f;
        Body.linearVelocity = new Vector2(dir * dashSpeed, 0f);
        knockbackUntil = 0f;
        PlayOneShot("slide", dashTime);
        OnDashed?.Invoke();
    }

    public void EndDash()
    {
        Body.gravityScale = savedGravity;
        Body.linearVelocity = new Vector2(Body.linearVelocity.x * 0.35f, 0f);
    }

    // "Fantasma" do sprite atual que some aos poucos (rastro do dash).
    public void SpawnAfterImage()
    {
        var ghost = Fx.MakeSprite("AfterImage", spriteRenderer.sprite, spriteRenderer.transform.position, afterImageColor, spriteRenderer.sortingOrder - 1);
        ghost.flipX = spriteRenderer.flipX;
        ghost.transform.localScale = spriteRenderer.transform.lossyScale;
        ghost.gameObject.AddComponent<AfterImage>();
    }

    // Coroutine (≈ função com "await" do Godot): pausa no yield e continua depois.
    private IEnumerator IgnorePlatformFor(Collider2D platform, float seconds)
    {
        Physics2D.IgnoreCollision(bodyCollider, platform, true);
        yield return new WaitForSeconds(seconds);
        Physics2D.IgnoreCollision(bodyCollider, platform, false);
    }

    // Desenha a caixa do sensor na Scene view quando o Player está selecionado.
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }

}
