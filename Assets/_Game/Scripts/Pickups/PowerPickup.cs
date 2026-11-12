using TMPro;
using UnityEngine;

// Orbe de poder (+, −, ×). Ao encostar: o tempo para e abre a janela de fusão.
// Fica no mapa por um tempo limitado (lifetime); nos últimos segundos pisca e encolhe antes de sumir.
public class PowerPickup : MonoBehaviour
{
    public PowerType Type { get; private set; }

    private Vector3 basePos;
    private SpriteRenderer orb;
    private TextMeshPro symbol;
    private float age;
    private float lifetime;          // 0 = para sempre
    private SpriteRenderer ring;
    private const float FadeTime = 3f; // últimos segundos: pisca cada vez mais rápido

    public static PowerPickup Spawn(PowerType type, Vector2 position, float lifetime)
    {
        var go = new GameObject("Power " + type);
        go.transform.position = position;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.8f;

        Color color = FusionRules.GetColor(type);
        var ring = Fx.MakeSprite("Ring", Fx.Circle, position, new Color(color.r, color.g, color.b, 0.3f), 30, go.transform);
        ring.transform.localScale = Vector3.one * 1.8f;
        var p = go.AddComponent<PowerPickup>();
        p.ring = ring;
        p.lifetime = lifetime;
        p.orb = Fx.MakeSprite("Orb", Fx.Circle, position, color, 31, go.transform);
        p.orb.transform.localScale = Vector3.one * 1.15f;
        p.symbol = Fx.WorldText(FusionRules.Symbol(type), position, 8f, Color.white, 32, go.transform);
        p.Type = type;
        p.basePos = position;
        Fx.Burst(position, color, 10, 6f, 0.25f);
        return p;
    }

    private void Update()
    {
        age += Time.deltaTime;
        transform.position = basePos + Vector3.up * Mathf.Sin(age * 2.5f) * 0.25f;
        orb.transform.localScale = Vector3.one * (1.15f + Mathf.Sin(age * 6f) * 0.08f);

        // Sem 2 cartas não dá para fundir: o orbe fica apagado.
        var gm = GameManager.Instance;
        bool usable = gm != null && gm.Hand.Count >= 2;

        // Desaparecendo: pisca (cada vez mais rápido) e encolhe nos últimos FadeTime segundos.
        float visible = 1f, shrink = 1f;
        if (lifetime > 0f)
        {
            float left = lifetime - age;
            if (left <= 0f) { Fx.Burst(transform.position, orb.color, 8, 4f, 0.2f); Destroy(gameObject); return; }
            if (left < FadeTime)
            {
                float k = 1f - left / FadeTime;                 // 0 → 1 até sumir
                float blinkSpeed = Mathf.Lerp(4f, 16f, k);
                visible = Mathf.Repeat(age * blinkSpeed, 1f) < 0.5f ? 1f : 0.25f;
                shrink = Mathf.Lerp(1f, 0.6f, k);
            }
        }
        transform.localScale = Vector3.one * shrink;
        SetAlpha(orb, visible);
        SetAlpha(ring, 0.3f * visible);
        symbol.alpha = (usable ? 1f : 0.35f) * visible;
    }

    private static void SetAlpha(SpriteRenderer sr, float a)
    {
        var c = sr.color;
        c.a = a;
        sr.color = c;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);
    private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

    private void TryCollect(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        var gm = GameManager.Instance;
        if (gm == null || !gm.CanAct || !gm.Fusion.Begin(Type)) return;
        Fx.Burst(transform.position, FusionRules.GetColor(Type), 18, 9f, 0.3f);
        Destroy(gameObject);
    }
}
