using System.Collections;
using TMPro;
using UnityEngine;

// Efeitos visuais simples criados por código: texto flutuante, partículas, tremida de câmera.
public static class Fx
{
    private static Sprite circle;
    private static Sprite pixel;

    // Círculo gerado em código (usado nos poderes e partículas).
    public static Sprite Circle
    {
        get
        {
            if (circle != null) return circle;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float r = size / 2f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r, dy = y - r;
                    tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
                }
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            return circle;
        }
    }

    // Quadrado branco 1×1 unidade.
    public static Sprite Pixel
    {
        get
        {
            if (pixel != null) return pixel;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var cols = new Color[16];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return pixel;
        }
    }

    public static SpriteRenderer MakeSprite(string name, Sprite sprite, Vector3 pos, Color color, int order, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    public static TextMeshPro WorldText(string text, Vector3 pos, float fontSize, Color color, int order, Transform parent = null)
    {
        var go = new GameObject("Text");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.fontStyle = FontStyles.Bold;
        tmp.sortingOrder = order;
        tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        return tmp;
    }

    // Texto que sobe e some (dano, "HIGHER", etc.)
    public static void Popup(string text, Vector3 pos, Color color, float fontSize = 7f, float duration = 1f, float rise = 1.6f)
    {
        var tmp = WorldText(text, pos, fontSize, color, 200);
        tmp.gameObject.AddComponent<FloatingText>().Init(duration, rise);
    }

    // Explosão de quadradinhos.
    public static void Burst(Vector3 pos, Color color, int count = 10, float speed = 8f, float size = 0.3f)
    {
        for (int i = 0; i < count; i++)
        {
            var sr = MakeSprite("Spark", Pixel, pos, color, 150);
            sr.transform.localScale = Vector3.one * size * Random.Range(0.6f, 1.3f);
            Vector2 v = Random.insideUnitCircle.normalized * speed * Random.Range(0.4f, 1f);
            sr.gameObject.AddComponent<Spark>().Init(v, Random.Range(0.35f, 0.6f));
        }
    }

    public static void Shake(float strength, float duration)
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (!cam.TryGetComponent(out CameraShake shake)) shake = cam.gameObject.AddComponent<CameraShake>();
        shake.Shake(strength, duration);
    }

    public static IEnumerator FlashSprite(SpriteRenderer sr, Color flashColor, float duration)
    {
        if (sr == null) yield break;
        Color original = sr.color;
        sr.color = flashColor;
        yield return new WaitForSecondsRealtime(duration);
        if (sr != null) sr.color = original;
    }
}
