using UnityEngine;

// Ataque dos chefes: um coringa gigante entra por uma lateral da tela girando e atravessa
// o andar inteiro na horizontal. Não quebra ao acertar (passa direto). Desvia pulando, descendo ou com dash.
[RequireComponent(typeof(Rigidbody2D))]
public class JokerProjectile : MonoBehaviour
{
    public const float Scale = 0.5f;      // 58×80 px → 1,8 × 2,5 unidades (girando, a meia escala não aparece)
    public const float Radius = 0.95f;    // colisor (um pouco menor que a carta: dano justo)
    private const float ExitX = 24f;      // some depois de sair da tela (que vai de −20 a 20)

    private Transform spin;
    private float spinSpeed;

    public static JokerProjectile Spawn(Sprite sprite, float y, bool fromLeft, float speed)
    {
        float x = fromLeft ? -ExitX + 1f : ExitX - 1f;
        var go = new GameObject("Joker");
        go.layer = LayerMask.NameToLayer("Enemy");
        go.transform.position = new Vector3(x, y, 0f);

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.linearVelocity = new Vector2(fromLeft ? speed : -speed, 0f);

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = Radius;

        var j = go.AddComponent<JokerProjectile>();
        j.spin = new GameObject("Spin").transform;
        j.spin.SetParent(go.transform, false);
        var sr = Fx.MakeSprite("Sprite", sprite, go.transform.position, Color.white, 75, j.spin);
        sr.transform.localScale = Vector3.one * Scale;
        j.spinSpeed = fromLeft ? -55f : 55f; // gira bem devagar: dá para ver que é um coringa
        return j;
    }

    private void Update()
    {
        spin.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        if (Mathf.Abs(transform.position.x) > ExitX) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null) health.Damage(1, transform.position);
    }
}
