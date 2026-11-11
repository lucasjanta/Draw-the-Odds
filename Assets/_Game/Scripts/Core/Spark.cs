using UnityEngine;

// Partícula simples (sem ParticleSystem): anda, cai um pouco e encolhe.
public class Spark : MonoBehaviour
{
    private Vector2 velocity;
    private float life, t;
    private Vector3 baseScale;

    public void Init(Vector2 velocity, float life)
    {
        this.velocity = velocity;
        this.life = life;
        baseScale = transform.localScale;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t += dt;
        velocity += Vector2.down * 18f * dt;
        transform.position += (Vector3)(velocity * dt);
        transform.localScale = baseScale * (1f - t / life);
        if (t >= life) Destroy(gameObject);
    }
}
