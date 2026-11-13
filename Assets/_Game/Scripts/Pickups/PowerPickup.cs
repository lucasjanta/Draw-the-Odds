using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Orbe de poder (+, −, ×). Ao encostar: o tempo para e abre a janela de fusão.
// Visual: orbe pixel art com contorno escuro + reflexo, brilho em degraus que pulsa, ondas que
// saem dele, faíscas orbitando e uma LUZ 2D da cor do orbe que ilumina o cenário em volta.
// Fica no mapa por um tempo limitado (lifetime); nos últimos segundos pisca e encolhe antes de sumir.
public class PowerPickup : MonoBehaviour
{
    public PowerType Type { get; private set; }

    private const float Px = 1f / 16f;          // 1 pixel em unidades do mundo
    private const float FadeTime = 3f;          // últimos segundos: pisca cada vez mais rápido
    private const int SparkCount = 3;

    private Vector3 basePos;
    private Color color;
    private Transform visual;                   // tudo que balança junto (orbe, brilho, símbolo)
    private SpriteRenderer orb, glow, shine;
    private SpriteRenderer symbol;              // + − × em pixel art (centralizado no orbe)
    private SpriteRenderer[] sparks;
    private Light2D glowLight;
    private float age, rippleTimer;
    private float lifetime;                     // 0 = para sempre

    public static PowerPickup Spawn(PowerType type, Vector2 position, float lifetime)
    {
        var go = new GameObject("Power " + type);
        go.transform.position = position;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.75f;

        var p = go.AddComponent<PowerPickup>();
        p.Type = type;
        p.lifetime = lifetime;
        p.basePos = position;
        p.color = FusionRules.GetColor(type);
        p.visual = new GameObject("Visual").transform;
        p.visual.SetParent(go.transform, false);

        p.glow = Fx.MakeSprite("Glow", Fx.OrbGlow, position, p.color, 29, p.visual);
        p.orb = Fx.MakeSprite("Orb", Fx.OrbBody, position, p.color, 31, p.visual);
        // Reflexo: 2×2 px + 1 px brancos em cima à esquerda
        p.shine = Fx.MakeSprite("Shine", Fx.Pixel, (Vector3)position + new Vector3(-6f, 6f) * Px, Color.white, 32, p.visual);
        p.shine.transform.localScale = Vector3.one * 2f * Px;
        var dot = Fx.MakeSprite("ShineDot", Fx.Pixel, (Vector3)position + new Vector3(-3.5f, 8.5f) * Px, Color.white, 32, p.visual);
        dot.transform.localScale = Vector3.one * Px;

        p.symbol = Fx.MakeSprite("Symbol", Fx.PowerSymbol(type), position, Color.white, 34, p.visual);

        p.sparks = new SpriteRenderer[SparkCount];
        for (int i = 0; i < SparkCount; i++)
        {
            p.sparks[i] = Fx.MakeSprite("Spark", Fx.Pixel, position, i == 0 ? Color.white : Color.Lerp(p.color, Color.white, 0.5f), 33, go.transform);
            p.sparks[i].transform.localScale = Vector3.one * (i == 0 ? 2f : 1f) * Px;
        }

        // Luz 2D: ilumina chão, plataformas e fundo em volta com a cor do orbe.
        p.glowLight = go.AddComponent<Light2D>();
        p.glowLight.lightType = Light2D.LightType.Point;
        p.glowLight.color = p.color;
        p.glowLight.pointLightInnerRadius = 0.4f;
        p.glowLight.pointLightOuterRadius = 5f;
        p.glowLight.falloffIntensity = 0.75f;
        p.glowLight.intensity = 0f; // acende aos poucos no Update

        Fx.Burst(position, p.color, 10, 6f, 0.25f);
        return p;
    }

    private void Update()
    {
        age += Time.deltaTime;

        // Balanço vertical de pixel em pixel (sem "meio pixel" borrando a pixel art)
        float bob = Mathf.Round(Mathf.Sin(age * 2.5f) * 4f) * Px;
        transform.position = basePos + Vector3.up * bob;

        // Sem 2 cartas não dá para fundir: o orbe fica apagado (brilho e luz fracos, símbolo transparente).
        var gm = GameManager.Instance;
        bool usable = gm != null && gm.Hand.Count >= 2;

        // Desaparecendo: pisca (cada vez mais rápido) e encolhe nos últimos FadeTime segundos.
        float visible = 1f, shrink = 1f;
        if (lifetime > 0f)
        {
            float left = lifetime - age;
            if (left <= 0f) { Fx.Burst(transform.position, color, 8, 4f, 0.2f); Destroy(gameObject); return; }
            if (left < FadeTime)
            {
                float k = 1f - left / FadeTime;                 // 0 → 1 até sumir
                float blinkSpeed = Mathf.Lerp(4f, 16f, k);
                visible = Mathf.Repeat(age * blinkSpeed, 1f) < 0.5f ? 1f : 0.25f;
                shrink = Mathf.Lerp(1f, 0.6f, k);
            }
        }
        visual.localScale = Vector3.one * shrink;

        // Brilho: pulsa suave no tamanho e na transparência
        float pulse = 0.5f + 0.5f * Mathf.Sin(age * 5f);
        glow.transform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1.35f, pulse);
        float power = usable ? 1f : 0.35f;
        SetAlpha(glow, (0.55f + 0.45f * pulse) * power * visible);
        SetAlpha(orb, visible);
        SetAlpha(shine, visible);
        SetAlpha(symbol, (usable ? 1f : 0.35f) * visible);

        // Luz no cenário: acende no primeiro meio segundo e "respira" junto com o brilho
        float fadeIn = Mathf.Clamp01(age * 2f);
        glowLight.intensity = Mathf.Lerp(0.9f, 1.5f, pulse) * power * visible * fadeIn;
        glowLight.pointLightOuterRadius = Mathf.Lerp(4.5f, 5.5f, pulse) * shrink;

        // Faíscas orbitando (posição arredondada no pixel)
        for (int i = 0; i < sparks.Length; i++)
        {
            float a = age * (2.2f + i * 0.6f) + i * Mathf.PI * 2f / sparks.Length;
            float radius = (1.25f + 0.15f * Mathf.Sin(age * 3f + i)) * shrink;
            Vector3 off = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.6f, 0f);
            off.x = Mathf.Round(off.x * 16f) * Px;
            off.y = Mathf.Round(off.y * 16f) * Px;
            sparks[i].transform.position = transform.position + off;
            // Atrás do orbe na metade de cima da órbita (dá sensação de profundidade)
            sparks[i].sortingOrder = Mathf.Sin(a) > 0f ? 28 : 33;
            SetAlpha(sparks[i], visible * (usable ? 1f : 0.4f));
        }

        // Onda: um anel sai do orbe de tempos em tempos
        rippleTimer -= Time.deltaTime;
        if (rippleTimer <= 0f && usable && visible > 0.5f)
        {
            rippleTimer = 1.3f;
            var ring = Fx.MakeSprite("Ripple", Fx.Ring, transform.position, new Color(color.r, color.g, color.b, 0.8f), 30);
            ring.gameObject.AddComponent<ShockRing>().Init(3.4f, 0.7f, 1f);
        }
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
        Fx.Burst(transform.position, color, 18, 9f, 0.3f);
        var ring = Fx.MakeSprite("CollectRing", Fx.Ring, transform.position, Color.white, 150);
        ring.gameObject.AddComponent<ShockRing>().Init(5f, 0.35f, 1f);
        Destroy(gameObject);
    }
}
