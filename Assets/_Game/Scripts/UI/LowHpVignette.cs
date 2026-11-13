using UnityEngine;
using UnityEngine.UI;

// Vinheta vermelha nas bordas da tela que pulsa quando o player está com pouca vida (2 ou 1).
// Com 1 de vida pulsa mais forte e mais rápido (como um coração acelerado).
public class LowHpVignette : MonoBehaviour
{
    private GameManager gm;
    private RawImage image;
    private float alpha;

    public static LowHpVignette Create(Transform parent, GameManager gm)
    {
        var go = new GameObject("LowHpVignette", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<RawImage>();
        img.texture = MakeTexture();
        img.color = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = false;
        img.rectTransform.Stretch();
        var v = go.AddComponent<LowHpVignette>();
        v.gm = gm;
        v.image = img;
        return v;
    }

    // Degradê: transparente no meio, vermelho escuro nas bordas (elipse do tamanho da tela).
    private static Texture2D MakeTexture()
    {
        const int w = 128, h = 72;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 0.8f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.15f, d));
                tex.SetPixel(x, y, new Color(0.85f, 0.05f, 0.08f, a));
            }
        tex.Apply();
        return tex;
    }

    private void Update()
    {
        var health = gm.PlayerHealth;
        bool playing = gm.State == GameManager.GameState.Playing || gm.State == GameManager.GameState.Transition;
        int hp = health != null ? health.Current : 99;
        float target = 0f;
        if (playing && hp > 0 && hp <= 2 && !gm.IsTutorial)
        {
            // batida: sobe rápido e desce devagar
            float speed = hp == 1 ? 2.4f : 1.5f;
            float beat = Mathf.Pow(1f - Mathf.Repeat(Time.unscaledTime * speed, 1f), 3f);
            target = (hp == 1 ? 0.55f : 0.35f) + beat * (hp == 1 ? 0.45f : 0.3f);
        }
        alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * 3f);
        image.color = new Color(1f, 1f, 1f, alpha);
    }
}
