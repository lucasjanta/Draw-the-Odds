using UnityEngine;

// Ícones pequenos em pixel art desenhados no código (sem precisar importar PNG).
// '#' = pixel branco; o contorno escuro em volta é gerado sozinho.
public static class PixelIcons
{
    // Duas setas curvas girando (= "comprar cartas novas" / REDRAW).
    private static readonly string[] RedrawRows =
    {
        ".....#####.....",
        "...#########...",
        "..###.....###..",
        ".##.........##.",
        ".##.......#####",
        "##.........###.",
        "##..........#..",
        "...............",
        "..#..........##",
        ".###.........##",
        "#####.......##.",
        ".##.........##.",
        "..###.....###..",
        "...#########...",
        ".....#####.....",
    };

    private static Sprite redraw;
    public static Sprite Redraw => redraw != null ? redraw : redraw = Build(RedrawRows);

    private static Sprite Build(string[] rows)
    {
        int h = rows.Length, w = rows[0].Length;
        int W = w + 2, H = h + 2; // 1 pixel de borda para o contorno
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[W * H];
        var white = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        var dark = new Color32(0x0A, 0x06, 0x12, 0xFF);

        bool Filled(int x, int y) // x, y no desenho (y = 0 em cima)
            => x >= 0 && y >= 0 && x < w && y < h && rows[y][x] == '#';

        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            int sx = x - 1, sy = (H - 1 - y) - 1; // textura começa embaixo
            Color32 c = default;
            if (Filled(sx, sy)) c = white;
            else
            {
                for (int dy = -1; dy <= 1 && c.a == 0; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (Filled(sx + dx, sy + dy)) { c = dark; break; }
            }
            pixels[y * W + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 16f);
    }
}
