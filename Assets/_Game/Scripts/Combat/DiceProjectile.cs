using UnityEngine;

// Dado (ou ficha de poker) arremessado pelo chefe. Causa 1 de dano ao encostar no player.
// A ficha cai "girando como moeda" (achata no eixo X) em vez de rodar.
[RequireComponent(typeof(Rigidbody2D))]
public class DiceProjectile : MonoBehaviour
{
    private const float WorldSize = 1.1f; // tamanho do dado em unidades

    private float life, spinSpeed, floorY;
    private int damage = 1;
    private SpriteRenderer sr;
    private bool coinFlip;
    private float age;
    private AudioSource fallSound;   // som da ficha caindo (para quando ela quebra)
    private SpriteRenderer shadow;   // sombra no chão embaixo da ficha (mostra onde ela vai cair)
    private Color chipColor = Color.white;

    // Rastro de meteoro (só nos dados): brasas que ficam para trás. danger = versão "perigosa"
    // (Rei): brilho vermelho em volta do dado e brasas maiores.
    private bool meteor, danger;
    private Color trailColor;
    private float emberTimer;
    private SpriteRenderer dangerGlow;

    // Ficha: sprite de 20×20 px em escala 2 (regra de escala inteira) = 40×40 px = 2,5 unidades.
    public const float ChipScale = 2f;
    public const float ChipRadius = 1.05f;  // colisor um pouco menor que a ficha (1,25 de raio visual)

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
        // Sempre girando rápido (nunca quase parado), para um lado ou para o outro
        d.spinSpeed = Random.Range(600f, 900f) * (Random.value < 0.5f ? -1f : 1f);
        return d;
    }

    // Liga o rastro de meteoro (cor do rastro e se é o dado "perigoso" do último chefe).
    public DiceProjectile WithTrail(Color color, bool dangerous)
    {
        meteor = true;
        danger = dangerous;
        trailColor = color;
        if (dangerous)
        {
            dangerGlow = Fx.MakeSprite("DangerGlow", Fx.OrbGlow, transform.position, new Color(color.r, color.g, color.b, 0.8f), 69, transform);
            dangerGlow.transform.localScale = Vector3.one * 0.55f;
            sr.color = new Color(1f, 0.8f, 0.78f); // o dado fica "em brasa"
        }
        return this;
    }

    // Ficha de poker: sprite 20×20 px em escala inteira 2× (2,5 unidades). Cai girando "como moeda"
    // e tem uma sombra no chão que cresce conforme ela chega perto.
    public static DiceProjectile SpawnChip(Sprite chip, Vector2 position, Vector2 velocity, float floorY, float life = 5f)
    {
        var go = new GameObject("Chip");
        go.layer = LayerMask.NameToLayer("Enemy");
        go.transform.position = position;

        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearVelocity = velocity;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = ChipRadius;

        var d = go.AddComponent<DiceProjectile>();
        d.sr = Fx.MakeSprite("Sprite", chip, position, Color.white, 70, go.transform);
        d.sr.transform.localScale = Vector3.one * ChipScale;
        d.life = life;
        d.floorY = floorY;
        d.coinFlip = true;
        d.spinSpeed = Random.Range(7f, 10f); // velocidade do "flip"
        d.chipColor = ChipColor(chip);
        // Contorno escuro: cópia da ficha um pouco maior atrás (gira junto, é filha do sprite)
        var outline = Fx.MakeSprite("Outline", chip, position, new Color(0.08f, 0.05f, 0.11f, 1f), 69, d.sr.transform);
        outline.transform.localScale = Vector3.one * 1.12f;
        // Rastro na cor da ficha (risco vertical atrás dela)
        d.meteor = true;
        d.trailColor = Color.Lerp(d.chipColor, Color.white, 0.25f);
        var gm = GameManager.Instance;
        if (gm != null) d.fallSound = AudioManager.SfxStoppable(gm.Config.SfxChipFall, 0.6f, 0.08f);

        // Sombra: elipse escura achatada em cima do chão (fora da hierarquia da ficha para não girar junto)
        d.shadow = Fx.MakeSprite("ChipShadow", Fx.Circle, new Vector3(position.x, floorY + 0.1f, 0f), new Color(0f, 0f, 0f, 0f), 3);
        return d;
    }

    // Cor "principal" de cada ficha pelo nome do arquivo (os cacos e a poeira usam essa cor).
    private static Color ChipColor(Sprite chip)
    {
        string n = chip != null ? chip.name.ToLowerInvariant() : "";
        if (n.Contains("red")) return new Color32(0xE0, 0x3A, 0x3A, 0xFF);
        if (n.Contains("black")) return new Color32(0x3A, 0x36, 0x48, 0xFF);
        if (n.Contains("blue")) return new Color32(0x3A, 0x7A, 0xE0, 0xFF);
        return new Color32(0xEE, 0xEE, 0xEE, 0xFF);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (coinFlip)
        {
            // Achata e volta no X (parece uma moeda girando); mínimo 0,15 para nunca sumir.
            float k = Mathf.Abs(Mathf.Cos(age * spinSpeed));
            sr.transform.localScale = new Vector3(Mathf.Max(0.15f, k), 1f, 1f) * ChipScale;
            UpdateShadow();
        }
        else sr.transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        if (meteor) UpdateTrail();
        life -= Time.deltaTime;
        bool outOfArena = Mathf.Abs(transform.position.x) > 22f || transform.position.y > 14f;
        // A ficha quebra quando a borda de baixo encosta no chão (o dado, quando o centro chega perto)
        float groundOffset = coinFlip ? 1.15f : 0.5f;
        if (life <= 0f || outOfArena || transform.position.y < floorY + groundOffset)
            Break(transform.position.y < floorY + groundOffset + 0.3f);
    }

    // Brasas atrás do dado: nascem na posição atual (o dado anda, elas ficam) = rastro de meteoro.
    private void UpdateTrail()
    {
        emberTimer -= Time.deltaTime;
        while (emberTimer <= 0f)
        {
            emberTimer += danger ? 0.008f : 0.014f;
            Vector3 pos = transform.position + (Vector3)(Random.insideUnitCircle * 0.25f);
            pos.x = Mathf.Round(pos.x * 16f) / 16f; // no grid de pixels
            pos.y = Mathf.Round(pos.y * 16f) / 16f;
            float size = Random.Range(3f, danger ? 7f : 5f) / 16f;
            var e = Fx.MakeSprite("Ember", Fx.Pixel, pos, Color.white, 68);
            e.transform.localScale = Vector3.one * size;
            // Cabeça quente (branco/amarelo) → cor do rastro; o perigoso termina em vermelho escuro
            // Perigoso: cabeça amarela/laranja de fogo; normal: quase branca
            Color hot = danger ? new Color(1f, 0.85f, 0.3f) : Color.white;
            Color head = Color.Lerp(hot, trailColor, Random.Range(0.25f, 0.75f));
            Color tail = danger ? new Color(0.35f, 0.02f, 0.05f, 0.9f) : new Color(trailColor.r, trailColor.g, trailColor.b, 0.6f);
            e.gameObject.AddComponent<TrailEmber>().Init(Random.Range(0.22f, danger ? 0.45f : 0.32f), head, tail,
                Random.insideUnitCircle * 0.6f + Vector2.up * 0.4f);
        }
        if (dangerGlow != null)
        {
            // Brilho tremendo como fogo
            float f = 0.5f + Random.value * 0.12f;
            dangerGlow.transform.localScale = Vector3.one * f;
            dangerGlow.transform.rotation = Quaternion.identity;
        }
    }

    // Sombra: começa invisível e pequena lá no alto; perto do chão fica do tamanho da ficha e mais escura.
    private void UpdateShadow()
    {
        if (shadow == null) return;
        float height = transform.position.y - floorY;
        float k = Mathf.Clamp01(1f - (height - 1f) / 16f);   // 0 = longe (16 u acima), 1 = encostando
        shadow.transform.position = new Vector3(transform.position.x, floorY + 0.08f, 0f);
        shadow.transform.localScale = new Vector3(Mathf.Lerp(0.6f, 2.4f, k), Mathf.Lerp(0.15f, 0.45f, k), 1f) / 2f;
        shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.1f, 0.55f, k));
    }

    private void Break(bool onGround = false)
    {
        if (coinFlip) ShatterChip(onGround);
        else Fx.Burst(transform.position, new Color(0.95f, 0.95f, 0.9f), 6, 5f, 0.18f);
        if (shadow != null) Destroy(shadow.gameObject);
        Destroy(gameObject);
    }

    // Quebra da ficha: 4 cacos (os 4 quartos do próprio sprite) voam girando e quicam no chão,
    // poeira na cor da ficha, onda de choque achatada no chão e uma tremidinha de câmera.
    private void ShatterChip(bool onGround)
    {
        Vector3 pos = transform.position;
        // Som da ficha quebrando. Na chuva várias quebram quase juntas: o throttle evita estourar o som.
        var gm = GameManager.Instance;
        if (gm != null) AudioManager.SfxThrottled(gm.Config.SfxChipBreak, 0.06f, onGround ? 0.8f : 0.6f, 0.1f);
        Sprite chip = sr != null ? sr.sprite : null;
        if (chip != null)
        {
            Rect r = chip.textureRect;
            float half = r.width / 2f;
            for (int i = 0; i < 4; i++)
            {
                int qx = i % 2, qy = i / 2;
                var piece = Sprite.Create(chip.texture, new Rect(r.x + qx * half, r.y + qy * half, half, r.height / 2f),
                    new Vector2(0.5f, 0.5f), chip.pixelsPerUnit);
                Vector3 offset = new Vector3((qx - 0.5f) * 0.6f, (qy - 0.5f) * 0.6f, 0f);
                var shard = Fx.MakeSprite("ChipShard", piece, pos + offset, Color.white, 72);
                shard.transform.localScale = Vector3.one * ChipScale;
                shard.transform.localScale = Vector3.one * ChipScale * 0.6f; // cacos menores (menos distração)
                Vector2 v = new Vector2((qx - 0.5f) * Random.Range(5f, 8f), Random.Range(4f, 7f) + qy * 1.5f);
                shard.gameObject.AddComponent<Debris>().Init(v, Random.Range(-540f, 540f), Random.Range(0.3f, 0.45f), floorY + 0.3f);
            }
        }
        Fx.Burst(pos + Vector3.down * 0.6f, chipColor, 6, 5f, 0.2f);
        if (onGround)
        {
            // Onda de choque pequena no chão (anel achatado que cresce e some)
            var ring = Fx.MakeSprite("ChipShock", Fx.Ring, new Vector3(pos.x, floorY + 0.15f, 0f), new Color(chipColor.r, chipColor.g, chipColor.b, 0.6f), 4);
            ring.gameObject.AddComponent<ShockRing>().Init(2.2f, 0.22f);
        }
    }

    private void OnDestroy()
    {
        AudioManager.StopSfx(fallSound); // quebrou (ou sumiu): corta o som da queda
        if (shadow != null) Destroy(shadow.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;
        if (health.Damage(damage, transform.position)) Break();
    }
}
