using UnityEngine;

// Onda de choque: anel achatado (perspectiva de chão) que cresce rápido e some.
public class ShockRing : MonoBehaviour
{
    private float maxWidth, duration, t, flatten = 0.3f;
    private SpriteRenderer sr;
    private Color baseColor;

    // flatten: altura/largura (0,3 = achatado no chão; 1 = anel redondo, ex.: crítico)
    public void Init(float maxWidth, float duration, float flatten = 0.3f)
    {
        this.flatten = flatten;
        this.maxWidth = maxWidth;
        this.duration = duration;
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
        Apply(0f);
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime; // tempo real: continua no hit-stop
        Apply(t / duration);
        if (t >= duration) Destroy(gameObject);
    }

    private void Apply(float k)
    {
        // O sprite do anel tem 2 unidades de largura (32 px, PPU 16)
        float w = Mathf.Lerp(0.6f, maxWidth, 1f - (1f - k) * (1f - k)) / 2f;
        transform.localScale = new Vector3(w, w * flatten, 1f);
        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - k);
    }
}
