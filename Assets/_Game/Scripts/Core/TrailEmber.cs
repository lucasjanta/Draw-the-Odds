using UnityEngine;

// Brasa do rastro (dados "meteoro"): fica parada onde nasceu, sobe um tiquinho, encolhe e some.
// Usa o tempo do jogo: na fusão (tempo parado) o rastro congela junto.
public class TrailEmber : MonoBehaviour
{
    private float life, t;
    private Vector3 baseScale;
    private SpriteRenderer sr;
    private Color startColor, endColor;
    private Vector2 drift;

    public void Init(float life, Color start, Color end, Vector2 drift)
    {
        this.life = life;
        this.drift = drift;
        startColor = start;
        endColor = end;
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = t / life;
        if (k >= 1f) { Destroy(gameObject); return; }
        transform.position += (Vector3)(drift * Time.deltaTime);
        transform.localScale = baseScale * (1f - k);
        // Cor: começa na cor da "cabeça" (quente) e vai para a cor do fim, apagando
        Color c = Color.Lerp(startColor, endColor, k);
        c.a *= 1f - k * k;
        sr.color = c;
    }
}
