using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Pacote de cartas no mapa. Pode ter naipes aleatórios ou um naipe específico.
// Visual no mesmo estilo dos orbes, só que AZUL: brilho suave pulsando, anel saindo de tempos em
// tempos, luz 2D azul no cenário e as cartas com contorno preto.
public class CardPickup : MonoBehaviour
{
    private static readonly Color Blue = new Color32(0x4A, 0xA8, 0xFF, 0xFF);

    private List<Card> cards;
    private Vector3 basePos;
    private float age, rippleTimer;
    private SpriteRenderer glow;
    private Light2D glowLight;

    public static CardPickup Spawn(GameConfig config, List<Card> cards, bool specificSuit, Vector2 position)
    {
        var go = new GameObject("CardPickup");
        go.transform.position = position;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.6f, 2f);

        var p = go.AddComponent<CardPickup>();
        p.cards = cards;
        p.basePos = position;
        p.glow = Fx.MakeSprite("Glow", Fx.OrbGlow, position, Blue, 29, go.transform);

        // Naipe específico → mostra a carta do naipe; aleatório → verso da carta.
        Sprite sprite = specificSuit ? config.Blank(cards[0].suit) : config.CardBack;
        // Um segundo "verso" atrás (inclinado) para parecer um pacote
        AddCard(go.transform, sprite, (Vector3)position + new Vector3(0.25f, 0.15f), new Color(0.75f, 0.75f, 0.75f), 30, -12f);
        AddCard(go.transform, sprite, position, Color.white, 32, 0f);

        var label = Fx.WorldText("+" + cards.Count, position + Vector2.up * 1.6f, 5f,
            specificSuit ? Color.Lerp(SuitInfo.GetColor(cards[0].suit), Color.white, 0.35f) : Color.white, 35, go.transform);
        Fx.AddWorldOutline(label);

        p.glowLight = go.AddComponent<Light2D>();
        p.glowLight.lightType = Light2D.LightType.Point;
        p.glowLight.color = Blue;
        p.glowLight.pointLightInnerRadius = 0.4f;
        p.glowLight.pointLightOuterRadius = 4.5f;
        p.glowLight.falloffIntensity = 0.75f;
        p.glowLight.intensity = 0f;

        Fx.Burst(position, Blue, 10, 6f, 0.22f);
        return p;
    }

    // Carta + contorno preto (cópia escura 1 px maior em volta, atrás dela).
    private static void AddCard(Transform parent, Sprite sprite, Vector3 pos, Color color, int order, float angle)
    {
        const float scale = 0.45f;
        var outline = Fx.MakeSprite("CardOutline", sprite, pos, new Color(0.06f, 0.04f, 0.09f, 1f), order - 1, parent);
        var card = Fx.MakeSprite("Card", sprite, pos, color, order, parent);
        card.transform.localScale = Vector3.one * scale;
        card.transform.localRotation = outline.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        if (sprite != null)
        {
            // +2 px de cada lado no tamanho final (1 px de tela = 1/16 de unidade)
            Vector2 size = sprite.bounds.size * scale;
            outline.transform.localScale = new Vector3(scale * (size.x + 2f / 16f) / size.x, scale * (size.y + 2f / 16f) / size.y, 1f);
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        transform.position = basePos + Vector3.up * Mathf.Round(Mathf.Sin(age * 3f) * 3f) / 16f;

        float pulse = 0.5f + 0.5f * Mathf.Sin(age * 5f);
        glow.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 1.3f, pulse);
        glow.color = new Color(Blue.r, Blue.g, Blue.b, 0.55f + 0.45f * pulse);
        glowLight.intensity = Mathf.Lerp(0.8f, 1.3f, pulse) * Mathf.Clamp01(age * 2f);

        rippleTimer -= Time.deltaTime;
        if (rippleTimer <= 0f)
        {
            rippleTimer = 1.3f;
            var ring = Fx.MakeSprite("Ripple", Fx.Ring, transform.position, new Color(Blue.r, Blue.g, Blue.b, 0.8f), 30);
            ring.gameObject.AddComponent<ShockRing>().Init(3.6f, 0.7f, 1f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        var gm = GameManager.Instance;
        if (gm == null || !gm.CanAct) return;
        if (gm.Hand.IsFull)
        {
            Fx.Popup("HAND FULL", transform.position + Vector3.up * 2f, Color.white, 5f, 0.8f);
            return;
        }
        int added = gm.GiveCards(cards);
        AudioManager.Sfx(gm.Config.SfxPowerUp, 0.8f); // mesmo som de pegar orbe (o draw card toca no GiveCards)
        Fx.Popup($"+{added} CARDS", transform.position + Vector3.up * 1.5f, new Color(0.7f, 0.85f, 1f), 6f, 1f);
        Fx.Burst(transform.position, Blue, 12, 7f, 0.25f);
        var collect = Fx.MakeSprite("CollectRing", Fx.Ring, transform.position, Color.white, 150);
        collect.gameObject.AddComponent<ShockRing>().Init(5f, 0.35f, 1f);
        Destroy(gameObject);
    }
}
