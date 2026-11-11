using System;
using System.Collections;
using UnityEngine;

// Vida do player: dano com invencibilidade temporária (pisca), knockback e morte.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 8;
    [SerializeField] private float invulnerableTime = 1.1f;
    [SerializeField] private Vector2 knockbackVelocity = new Vector2(9f, 10f);
    [SerializeField] private float knockbackLockTime = 0.25f;

    public int Current { get; private set; }
    public int Max => maxHp;
    public bool IsDead { get; private set; }
    public bool IsInvulnerable => Time.time < invulnerableUntil || forcedInvulnerable || controller.IsDashing;

    // Tutorial: o player não morre (fica com 1 de vida).
    public bool cannotDie;

    // Quando true (transições entre chefes) o player não toma dano.
    public bool forcedInvulnerable;

    public event Action<int, int> OnHealthChanged; // atual, máximo
    public event Action<int> OnDamaged;
    public event Action<int> OnHealed;
    public event Action OnDied;

    private PlayerController controller;
    private SpriteRenderer spriteRenderer;
    private float invulnerableUntil;
    private Coroutine blinkRoutine;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        Current = maxHp;
    }

    public void SetMax(int value, bool refill)
    {
        maxHp = value;
        if (refill) Current = maxHp;
        Current = Mathf.Min(Current, maxHp);
        OnHealthChanged?.Invoke(Current, maxHp);
    }

    public bool Damage(int amount, Vector2 fromPosition)
    {
        if (IsDead || IsInvulnerable || amount <= 0) return false;

        Current = Mathf.Max(cannotDie ? 1 : 0, Current - amount);
        OnDamaged?.Invoke(amount);
        OnHealthChanged?.Invoke(Current, maxHp);
        Fx.Shake(0.35f, 0.25f);
        Fx.Burst(transform.position + Vector3.up, new Color(1f, 0.3f, 0.3f), 8, 7f, 0.25f);

        if (Current <= 0)
        {
            IsDead = true;
            controller.Die();
            OnDied?.Invoke();
            return true;
        }

        // Empurra para longe de quem bateu
        float side = transform.position.x >= fromPosition.x ? 1f : -1f;
        controller.Knockback(new Vector2(knockbackVelocity.x * side, knockbackVelocity.y), knockbackLockTime);
        controller.PlayOneShot("hurt", 0.3f);

        invulnerableUntil = Time.time + invulnerableTime;
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(Blink());
        return true;
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        int before = Current;
        Current = Mathf.Min(maxHp, Current + amount);
        if (Current != before) OnHealed?.Invoke(Current - before);
        OnHealthChanged?.Invoke(Current, maxHp);
    }

    private IEnumerator Blink()
    {
        while (Time.time < invulnerableUntil)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(0.07f);
        }
        spriteRenderer.enabled = true;
    }
}
