using UnityEngine;

// Dado arremessado pelo chefe. Causa 1 de dano ao encostar no player.
[RequireComponent(typeof(Rigidbody2D))]
public class DiceProjectile : MonoBehaviour
{
    private const float WorldSize = 1.1f; // tamanho do dado em unidades

    private float life, spinSpeed, floorY;
    private int damage = 1;
    private SpriteRenderer sr;

    public static DiceProjectile Spawn(GameConfig config, int face, Vector2 position, Vector2 velocity, float gravity, float floorY, float life = 5f)
    {
        var go = new GameObject("Die");
        go.layer = LayerMask.NameToLayer("Enemy");
        go.transform.position = position;

        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = gravity;
        body.linearVelocity = velocity;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.one * WorldSize * 0.75f;

        var d = go.AddComponent<DiceProjectile>();
        d.sr = Fx.MakeSprite("Sprite", config.DieFace(face), position, Color.white, 70, go.transform);
        float spriteSize = d.sr.sprite != null ? d.sr.sprite.bounds.size.x : 1f;
        d.sr.transform.localScale = Vector3.one * (WorldSize / spriteSize);
        d.life = life;
        d.floorY = floorY;
        d.spinSpeed = Random.Range(-540f, 540f);
        return d;
    }

    private void Update()
    {
        sr.transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        life -= Time.deltaTime;
        bool outOfArena = Mathf.Abs(transform.position.x) > 22f || transform.position.y > 14f;
        if (life <= 0f || outOfArena || transform.position.y < floorY + 0.5f)
            Break();
    }

    private void Break()
    {
        Fx.Burst(transform.position, new Color(0.95f, 0.95f, 0.9f), 6, 5f, 0.18f);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;
        if (health.Damage(damage, transform.position)) Break();
    }
}
