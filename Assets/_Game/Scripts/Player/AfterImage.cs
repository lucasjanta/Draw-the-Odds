using UnityEngine;

// Cópia do sprite do player que desaparece (rastro do dash).
public class AfterImage : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.22f;
    private SpriteRenderer sr;
    private float startAlpha, t;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        startAlpha = sr.color.a;
    }

    private void Update()
    {
        t += Time.deltaTime;
        var c = sr.color;
        c.a = startAlpha * (1f - t / lifetime);
        sr.color = c;
        if (t >= lifetime) Destroy(gameObject);
    }
}
