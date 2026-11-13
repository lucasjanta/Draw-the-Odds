using UnityEngine;

// Clarão de tela inteira (um retângulo na frente da câmera que some rápido). Usado no crítico.
public class ScreenFlash : MonoBehaviour
{
    private SpriteRenderer sr;
    private float duration, t, startAlpha;

    public static void Play(Color color, float alpha, float duration)
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 pos = cam.transform.position;
        pos.z = 0f;
        var sr = Fx.MakeSprite("ScreenFlash", Fx.Pixel, pos, new Color(color.r, color.g, color.b, alpha), 400);
        sr.transform.localScale = new Vector3(44f, 26f, 1f); // a tela tem 40 × 22,5 unidades
        var f = sr.gameObject.AddComponent<ScreenFlash>();
        f.sr = sr;
        f.duration = duration;
        f.startAlpha = alpha;
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        var c = sr.color;
        c.a = startAlpha * (1f - t / duration);
        sr.color = c;
        if (t >= duration) Destroy(gameObject);
    }
}
