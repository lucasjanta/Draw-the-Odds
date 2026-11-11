using TMPro;
using UnityEngine;

// Texto flutuante: sobe e desaparece (tempo real, para ler mesmo em câmera lenta).
public class FloatingText : MonoBehaviour
{
    private float duration, rise, t;
    private TextMeshPro tmp;
    private Vector3 start;

    public void Init(float duration, float rise)
    {
        this.duration = duration;
        this.rise = rise;
        tmp = GetComponent<TextMeshPro>();
        start = transform.position;
        transform.localScale = Vector3.one * 0.6f;
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        float k = t / duration;
        transform.position = start + Vector3.up * rise * (1f - (1f - k) * (1f - k)); // ease-out
        float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, (k - 0.15f) * 4f);
        transform.localScale = Vector3.one * pop;
        if (k > 0.6f) tmp.alpha = 1f - (k - 0.6f) / 0.4f;
        if (k >= 1f) Destroy(gameObject);
    }
}
