using System.Collections.Generic;
using UnityEngine;

// Troca de cores das roupas (texturas do aldeão) e retratos pequenos para a UI.
public static class VillagerPalette
{
    // Cores originais do aldeão (medidas no sprite) e qual delas é a "cor do meio" de cada parte.
    // O cabelo e as botas NÃO entram (ficam iguais em todas as roupas).
    private static readonly Color32[][] Groups =
    {
        new[] { new Color32(78, 98, 46, 255), new Color32(104, 128, 58, 255), new Color32(138, 160, 76, 255) },   // lenço
        new[] { new Color32(140, 122, 92, 255), new Color32(170, 152, 116, 255), new Color32(201, 184, 146, 255) }, // camisa
        new[] { new Color32(128, 77, 54, 255), new Color32(143, 86, 59, 255) },                                    // colete/túnica
        new[] { new Color32(70, 52, 38, 255), new Color32(90, 68, 48, 255) },                                      // calça
    };
    private static readonly int[] MidIndex = { 1, 1, 0, 1 };

    private static readonly Dictionary<Texture2D, Color32[]> originals = new Dictionary<Texture2D, Color32[]>();
    private static readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();
    private static bool hooked;

    private static int Key(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

    // Pixels originais (guardados na 1ª vez). No Editor a textura volta ao normal ao sair do Play.
    private static Color32[] Original(Texture2D tex)
    {
        if (tex == null || !tex.isReadable) return null;
        if (!originals.TryGetValue(tex, out var px))
        {
            px = tex.GetPixels32();
            originals[tex] = px;
            if (!hooked) { hooked = true; Application.quitting += RestoreAll; }
        }
        return px;
    }

    private static void RestoreAll()
    {
        foreach (var kv in originals)
        {
            if (kv.Key == null) continue;
            kv.Key.SetPixels32(kv.Value);
            kv.Key.Apply(false);
        }
    }

    // Mapa cor original → cor da roupa.
    private static Dictionary<int, Color32> BuildMap(int outfit)
    {
        var map = new Dictionary<int, Color32>();
        var o = Wardrobe.Outfits[Mathf.Clamp(outfit, 0, Wardrobe.Outfits.Length - 1)];
        Color32[] mids = { o.scarf, o.shirt, o.tunic, o.pants };
        for (int g = 0; g < Groups.Length; g++)
        {
            Color32 origMid = Groups[g][MidIndex[g]];
            foreach (var shade in Groups[g])
            {
                // A sombra mantém a proporção que tinha com a cor do meio
                byte Ch(byte target, byte s, byte m) => (byte)Mathf.Clamp(Mathf.RoundToInt(target * (s / (float)Mathf.Max(1, (int)m))), 0, 255);
                map[Key(shade)] = new Color32(Ch(mids[g].r, shade.r, origMid.r), Ch(mids[g].g, shade.g, origMid.g), Ch(mids[g].b, shade.b, origMid.b), 255);
            }
        }
        return map;
    }

    private static Color32[] Recolor(Color32[] src, Dictionary<int, Color32> map)
    {
        var dst = new Color32[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            var c = src[i];
            dst[i] = c.a > 0 && map.TryGetValue(Key(c), out var n) ? new Color32(n.r, n.g, n.b, c.a) : c;
        }
        return dst;
    }

    public static void ApplyTo(Texture2D tex, int outfit)
    {
        var px = Original(tex);
        if (px == null) return;
        tex.SetPixels32(outfit == 0 ? px : Recolor(px, BuildMap(outfit)));
        tex.Apply(false);
    }

    // Retrato (1º quadro) com chapéu e roupa: usado na taverna e no ranking. Fica em cache.
    public static Sprite Portrait(GameConfig config, int hat, int outfit)
    {
        string key = hat + "." + outfit;
        if (portraits.TryGetValue(key, out var cached) && cached != null) return cached;
        var sprite = config != null ? config.HatSprite(hat) : null;
        if (sprite == null) return null;
        var px = Original(sprite.texture);
        if (px == null) return sprite; // textura sem Read/Write: retrato sem a roupa
        Rect r = sprite.rect; // o Aseprite Importer monta a própria textura: rect = posição do quadro nela
        int x0 = (int)r.x, y0 = (int)r.y, w = (int)r.width, h = (int)r.height, texW = sprite.texture.width;
        var crop = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                crop[y * w + x] = px[(y0 + y) * texW + x0 + x];
        if (outfit != 0) crop = Recolor(crop, BuildMap(outfit));
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(crop);
        tex.Apply(false);
        var result = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16f);
        portraits[key] = result;
        return result;
    }
}
