using UnityEngine;

// Caco que voa e cai (pedaços da ficha quebrando). Diferente do Spark, não encolhe:
// gira, cai com gravidade, quica uma vez no chão e some piscando.
public class Debris : MonoBehaviour
{
    private Vector2 velocity;
    private float spin, life, t, floorY;
    private bool bounced;
    private SpriteRenderer sr;

    public void Init(Vector2 velocity, float spin, float life, float floorY)
    {
        this.velocity = velocity;
        this.spin = spin;
        this.life = life;
        this.floorY = floorY;
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        velocity += Vector2.down * 40f * dt;
        transform.position += (Vector3)(velocity * dt);
        transform.Rotate(0f, 0f, spin * dt);

        // Quica no chão uma vez (perde quase toda a força), depois atravessa e some.
        if (!bounced && velocity.y < 0f && transform.position.y < floorY)
        {
            bounced = true;
            velocity = new Vector2(velocity.x * 0.5f, -velocity.y * 0.35f);
            spin *= 0.5f;
        }

        // Últimos 30% da vida: pisca (estilo retrô) em vez de desbotar.
        if (sr != null && t > life * 0.7f) sr.enabled = Mathf.Repeat(t * 16f, 1f) < 0.5f;
        if (t >= life) Destroy(gameObject);
    }

    // O sprite do caco foi criado em código (Sprite.Create): destruir junto para não acumular na memória.
    private void OnDestroy()
    {
        if (sr != null && sr.sprite != null) Destroy(sr.sprite);
    }
}
