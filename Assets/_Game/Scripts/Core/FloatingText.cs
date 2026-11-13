using TMPro;
using UnityEngine;

// Texto flutuante: sobe e desaparece (tempo real, para ler mesmo em câmera lenta).
// "slam" = entrada com impacto: nasce GRANDE e inclinado, bate no tamanho final com um quique
// e treme um pouco (usado nas pistas quando a carta acerta o chefe).
public class FloatingText : MonoBehaviour
{
    private float duration, rise, t;
    private TextMeshPro[] texts;     // o texto + as cópias escuras do contorno (filhas)
    private float[] baseAlpha;
    private Vector3 start;
    private bool slam;
    private float tilt;
    private Color flashColor;
    private TextMeshPro main;
    private Color mainColor;

    public void Init(float duration, float rise, bool slam = false, Color? flash = null)
    {
        this.duration = duration;
        this.rise = rise;
        this.slam = slam;
        main = GetComponent<TextMeshPro>();
        mainColor = main.color;
        flashColor = flash ?? mainColor;
        texts = GetComponentsInChildren<TextMeshPro>();
        baseAlpha = new float[texts.Length];
        for (int i = 0; i < texts.Length; i++) baseAlpha[i] = texts[i].color.a;
        start = transform.position;
        tilt = slam ? Random.Range(-14f, 14f) : 0f;
        Apply(0f);
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        float k = t / duration;
        Apply(k);
        if (k >= 1f) Destroy(gameObject);
    }

    private void Apply(float k)
    {
        Vector3 pos = start + Vector3.up * rise * (1f - (1f - k) * (1f - k)); // ease-out
        float scale;
        if (slam)
        {
            // 0–7%: encolhe de 2,6× até 0,85× (a "pancada") · 7–20%: quica até 1,1× · depois assenta em 1×
            if (k < 0.07f) scale = Mathf.Lerp(2.6f, 0.85f, k / 0.07f);
            else if (k < 0.2f) scale = Mathf.Lerp(0.85f, 1.1f, Mathf.Sin((k - 0.07f) / 0.13f * Mathf.PI * 0.5f));
            else scale = Mathf.Lerp(1.1f, 1f, Mathf.Min(1f, (k - 0.2f) * 5f));
            float settle = Mathf.Clamp01(k / 0.25f);
            transform.rotation = Quaternion.Euler(0f, 0f, tilt * (1f - settle));
            if (k > 0.07f && k < 0.3f) pos += (Vector3)(Random.insideUnitCircle * 0.12f * (1f - k / 0.3f)); // tremidinha
            // Pisca na cor de destaque logo depois da pancada (crítico: branco ↔ dourado)
            main.color = k < 0.35f && Mathf.Repeat(t * 14f, 1f) < 0.5f ? flashColor : mainColor;
        }
        else
        {
            scale = k < 0.15f ? Mathf.Lerp(0.6f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, (k - 0.15f) * 4f);
        }
        transform.position = pos;
        transform.localScale = Vector3.one * scale;

        float fade = k > 0.6f ? 1f - (k - 0.6f) / 0.4f : 1f;
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            var c = texts[i].color;
            c.a = baseAlpha[i] * fade;
            texts[i].color = c;
        }
    }
}
