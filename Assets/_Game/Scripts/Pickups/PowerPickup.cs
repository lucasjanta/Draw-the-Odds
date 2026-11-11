using TMPro;
using UnityEngine;

// Orbe de poder (+, −, troca de naipe). Ao encostar: câmera lenta e janela de fusão.
public class PowerPickup : MonoBehaviour
{
    public PowerType Type { get; private set; }

    private Vector3 basePos;
    private SpriteRenderer orb;
    private TextMeshPro symbol;
    private float age;

    public static PowerPickup Spawn(PowerType type, Vector2 position)
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
        symbol.alpha = usable ? 1f : 0.35f;
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
