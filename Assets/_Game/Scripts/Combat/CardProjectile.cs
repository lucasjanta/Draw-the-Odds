using TMPro;
using UnityEngine;

// Carta arremessada pelo player: voa girando, com homing leve até o chefe.
[RequireComponent(typeof(Rigidbody2D))]
public class CardProjectile : MonoBehaviour
{
    private const float SpinSpeed = 900f;
    private const float Lifetime = 1.8f;

    private Card card;
    private Transform target;
    private float speed, homing, cone, age;
    private Rigidbody2D body;
    private Transform spin;
    private bool consumed;

    // Fábrica: monta o objeto inteiro em código (sem prefab).
    public static CardProjectile Spawn(GameConfig config, Card card, Vector2 position, Vector2 direction, Transform target)
    {
        var go = new GameObject($"Card {card}");
        go.layer = LayerMask.NameToLayer("Projectile");
        go.transform.position = position;

        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.7f;

        var spin = new GameObject("Spin").transform;
        spin.SetParent(go.transform, false);
        var sr = Fx.MakeSprite("Sprite", config.Blank(card.suit), position, Color.white, 60, spin);
        sr.transform.localScale = Vector3.one * 0.5f;
        var number = Fx.WorldText(card.Label, position, 7f, SuitInfo.GetColor(card.suit), 61, spin);
        number.outlineColor = Color.white;
        number.outlineWidth = 0.3f;

        var p = go.AddComponent<CardProjectile>();
        p.card = card;
        p.target = target;
        p.speed = config.CardSpeed;
        p.homing = config.CardHomingDegreesPerSecond;
        p.cone = config.CardHomingConeDegrees;
        p.body = body;
        p.spin = spin;
        body.linearVelocity = direction.normalized * p.speed;
        return p;
    }

    private void Update()
    {
        spin.Rotate(0f, 0f, -SpinSpeed * Time.deltaTime);
        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            Fx.Burst(transform.position, SuitInfo.GetColor(card.suit), 6, 4f, 0.2f);
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (target == null) return;
        // Homing leve: gira a velocidade um pouco em direção ao alvo a cada passo.
        Vector2 toTarget = (Vector2)target.position - body.position;
        Vector2 v = body.linearVelocity;
        float angle = Vector2.SignedAngle(v, toTarget);
        if (Mathf.Abs(angle) > cone) return; // jogou longe do chefe: sem ajuda
        float step = Mathf.Clamp(angle, -homing * Time.fixedDeltaTime, homing * Time.fixedDeltaTime);
        body.linearVelocity = Quaternion.Euler(0f, 0f, step) * v.normalized * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        var boss = other.GetComponentInParent<BossController>();
        if (boss == null || boss.IsDead) return;
        consumed = true;
        boss.ReceiveCard(card, transform.position);
        Destroy(gameObject);
    }
}
