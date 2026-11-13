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

    private static Sprite arrow, ring;

    // Seta de perigo em pixel art (aponta para BAIXO), gerada em código: miolo branco (tingível
    // pela cor do SpriteRenderer) + contorno preto de 1 px que não muda com o tingimento.
    // Para outras direções, gire o objeto em múltiplos de 90° (continua com pixels perfeitos).
    public static Sprite Arrow
    {
        get
        {
            if (arrow != null) return arrow;
            // Máscara do miolo (1 = cor). Linha 0 = topo. Haste de 3 px + ponta de 9 px.
            string[] mask =
            {
                "...111...",
                "...111...",
                "...111...",
                "111111111",
                ".1111111.",
                "..11111..",
                "...111...",
                "....1....",
            };
            arrow = MaskSprite(mask);
            return arrow;
        }
    }

    // Anel fino (onda de choque das fichas quebrando).
    public static Sprite Ring
    {
        get
        {
            if (ring != null) return ring;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float r = size / 2f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r));
                    tex.SetPixel(x, y, d <= r && d >= r - 2.2f ? Color.white : Color.clear);
                }
            tex.Apply();
            ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            return ring;
        }
    }

    private static Sprite orbBody, orbGlow;
    private static readonly Sprite[] powerSymbols = new Sprite[3];

    // Símbolo do orbe (+ − ×) em pixel art com contorno escuro. Tamanhos PARES (como o orbe de 26 px):
    // assim o centro do símbolo cai exatamente no centro do orbe, sem meio pixel de diferença.
    public static Sprite PowerSymbol(PowerType type)
    {
        int i = (int)type;
        if (powerSymbols[i] != null) return powerSymbols[i];
        string[] mask;
        if (type == PowerType.Add)
            mask = new[] { "...11...", "...11...", "...11...", "11111111", "11111111", "...11...", "...11...", "...11..." };
        else if (type == PowerType.Subtract)
            mask = new[] { "11111111", "11111111" };
        else
        {
            // × de 10×10: as duas diagonais com 3 px de espessura (simétricas)
            mask = new string[10];
            for (int y = 0; y < 10; y++)
            {
                var row = new char[10];
                for (int x = 0; x < 10; x++) row[x] = Mathf.Abs(x - y) <= 1 || Mathf.Abs(x + y - 9) <= 1 ? '1' : '.';
                mask[y] = new string(row);
            }
        }
        powerSymbols[i] = MaskSprite(mask);
        return powerSymbols[i];
    }

    // Orbe de poder: círculo de 24 px com sombra embaixo à direita (cinza = mais escuro depois de tingir)
    // e contorno escuro de 1 px. Tingido pela cor do SpriteRenderer.
    public static Sprite OrbBody
    {
        get
        {
            if (orbBody != null) return orbBody;
            const int d = 24, size = d + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float c = size / 2f, r = d / 2f;
            bool Inside(int x, int y) { float dx = x + 0.5f - c, dy = y + 0.5f - c; return dx * dx + dy * dy <= r * r; }
            var outline = new Color32(0x14, 0x0C, 0x1C, 0xFF);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color col = Color.clear;
                    if (Inside(x, y))
                    {
                        // Sombra: o que fica fora de um círculo deslocado para cima/esquerda
                        float sx = x + 0.5f - (c - r * 0.25f), sy = y + 0.5f - (c + r * 0.25f);
                        col = sx * sx + sy * sy > r * r * 0.92f ? new Color(0.62f, 0.62f, 0.62f, 1f) : Color.white;
                    }
                    else if (Inside(x - 1, y) || Inside(x + 1, y) || Inside(x, y - 1) || Inside(x, y + 1)) col = outline;
                    tex.SetPixel(x, y, col);
                }
            // O reflexo branco fica num sprite à parte (tingir multiplica a cor: aqui ele ficaria colorido).
            tex.Apply();
            orbBody = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            return orbBody;
        }
    }

    // Brilho em volta do orbe (e dos dados perigosos): degradê radial suave, forte no meio e
    // sumindo até a borda. Filtro bilinear de propósito (luz não precisa ser pixelada).
    public static Sprite OrbGlow
    {
        get
        {
            if (orbGlow != null) return orbGlow;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float k = Mathf.Clamp01(Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / c);
                    float a = 1f - k;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.9f * a * a)); // forte no meio, some suave na borda
                }
            tex.Apply();
            orbGlow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            return orbGlow;
        }
    }

    // Cria um sprite a partir de uma máscara de texto, com 1 px de contorno preto em volta (8 vizinhos).
    private static Sprite MaskSprite(string[] mask)
    {
        int h = mask.Length, w = mask[0].Length;
        int W = w + 2, H = h + 2;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        bool Fill(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && mask[h - 1 - y][x] == '1';
        var outline = new Color32(0x14, 0x0C, 0x1C, 0xFF);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int mx = x - 1, my = y - 1;
                Color c = Color.clear;
                if (Fill(mx, my)) c = Color.white;
                else
                {
                    for (int dy = -1; dy <= 1 && c.a == 0f; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (Fill(mx + dx, my + dy)) { c = outline; break; }
                }
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 16f);
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
        tmp.text = Loc.T(text); // popups do mundo já nascem traduzidos
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

    // Contorno pixel art para texto no mundo: 8 cópias escuras deslocadas em volta, desenhadas atrás
    // (o outline do TMP quase não aparece com a fonte pixel). As cópias são filhas: acompanham escala e giro.
    private static readonly Vector2[] OutlineOffsets =
    {
        new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1), new Vector2(-1, 0),
        new Vector2(1, 0), new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1),
    };

    public static void AddWorldOutline(TextMeshPro tmp, float pixels = 1f)
    {
        var dark = new Color32(0x14, 0x0C, 0x1C, 0xFF);
        // Remove as tags de cor (<color=...>) para a cópia ficar toda escura
        string plain = System.Text.RegularExpressions.Regex.Replace(tmp.text, "<color=[^>]*>|</color>", "");
        foreach (var o in OutlineOffsets)
        {
            var copy = WorldText(plain, tmp.transform.position, tmp.fontSize, dark, tmp.sortingOrder - 1, tmp.transform);
            copy.alignment = tmp.alignment;
            copy.font = tmp.font;
            copy.name = "Outline";
            copy.transform.localPosition = (Vector3)(o * pixels / 16f);
            copy.outlineWidth = 0f;
        }
    }

    // Texto que sobe e some (dano, "HIGHER", etc.), com contorno escuro.
    // slam = entra com impacto (grande → tamanho normal, inclinado, tremendo). flash = cor que pisca no começo.
    public static void Popup(string text, Vector3 pos, Color color, float fontSize = 7f, float duration = 1f, float rise = 1.6f,
        bool slam = false, Color? flash = null)
    {
        var tmp = WorldText(text, pos, fontSize, color, 200);
        AddWorldOutline(tmp, fontSize >= 10f ? 2f : 1f);
        tmp.gameObject.AddComponent<FloatingText>().Init(duration, rise, slam, flash);
    }

    // Congela o jogo por um instante (golpe forte).
    public static void HitStop(float seconds)
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (!cam.TryGetComponent(out HitStop stop)) stop = cam.gameObject.AddComponent<HitStop>();
        stop.Play(seconds);
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
