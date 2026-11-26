# Gera o ícone (256x256) e as capas da página do jogo nos portais, a partir dos sprites do jogo:
# 1360x850 (GamePix), 1920x1080 (paisagem), 800x1200 (retrato) e 800x800 (quadrada).
# Uso:  python Tools/make_store_art.py <pasta_com_frames_do_aventureiro> <pasta_de_saida>
# Os frames do aventureiro saem do Aseprite:
#   Aseprite.exe -b --frame-tag cast adventurer.aseprite --save-as adv_cast_{frame}.png
import math
import os
import random
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets", "_Game", "Art")
FONTS = os.path.join(ROOT, "Assets", "_Game", "Fonts")
ADV_DIR = sys.argv[1]
OUT = sys.argv[2]

GOLD = (255, 212, 71)
GOLD_DARK = (255, 168, 48)
BROWN = (122, 46, 14)
INK = (10, 6, 18)


def load(*parts):
    return Image.open(os.path.join(ART, *parts)).convert("RGBA")


def up(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)


def rot(img, deg):
    return img.rotate(deg, resample=Image.NEAREST, expand=True)


def paste(base, img, cx, cy):
    """Cola img com o centro em (cx, cy)."""
    base.alpha_composite(img, (int(cx - img.width / 2), int(cy - img.height / 2)))


def shadow(img, offset=(10, 14), alpha=150, blur=6):
    a = img.split()[3].point(lambda v: alpha if v > 0 else 0)
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sh.putalpha(a)
    pad = blur * 3
    canvas = Image.new("RGBA", (img.width + pad * 2, img.height + pad * 2), (0, 0, 0, 0))
    canvas.alpha_composite(sh, (pad, pad))
    return canvas.filter(ImageFilter.GaussianBlur(blur)), (offset[0] - pad, offset[1] - pad)


def paste_shadowed(base, img, cx, cy, offset=(10, 14), alpha=150, blur=6):
    sh, (ox, oy) = shadow(img, offset, alpha, blur)
    base.alpha_composite(sh, (int(cx - img.width / 2 + ox), int(cy - img.height / 2 + oy)))
    paste(base, img, cx, cy)


def outlined(mask, fill, outline, thickness, pixel=1):
    """Texto/forma (máscara L) com contorno em blocos de pixel."""
    w, h = mask.size
    pad = thickness + 2
    big = Image.new("L", (w + pad * 2, h + pad * 2), 0)
    big.paste(mask, (pad, pad))
    grown = big.filter(ImageFilter.MaxFilter(thickness * 2 + 1))
    out = Image.new("RGBA", big.size, (0, 0, 0, 0))
    out.paste(Image.new("RGBA", big.size, outline + (255,)), (0, 0), grown)
    if isinstance(fill, tuple):
        out.paste(Image.new("RGBA", big.size, fill + (255,)), (0, 0), big)
    else:  # gradiente vertical (imagem do tamanho de big)
        out.paste(fill, (0, 0), big)
    return out


def text_mask(text, font_path, size, scale):
    """Renderiza sem antialias no tamanho nativo e amplia (pixel art nítida)."""
    font = ImageFont.truetype(font_path, size)
    bbox = font.getbbox(text)
    w, h = bbox[2] - bbox[0] + 2, bbox[3] - bbox[1] + 2
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    d.fontmode = "1"
    d.text((-bbox[0] + 1, -bbox[1] + 1), text, font=font, fill=255)
    return m.resize((w * scale, h * scale), Image.NEAREST)


def gold_gradient(size):
    g = Image.new("RGBA", size)
    d = ImageDraw.Draw(g)
    for y in range(size[1]):
        t = y / max(1, size[1] - 1)
        t = min(1.0, max(0.0, (t - 0.45) / 0.55)) * 0.8
        c = tuple(int(GOLD[i] + (GOLD_DARK[i] - GOLD[i]) * t) for i in range(3))
        d.line([(0, y), (size[0], y)], fill=c + (255,))
    return g


def title(text, size_native, scale, outline_px):
    m = text_mask(text, os.path.join(FONTS, "alagard.ttf"), size_native, scale)
    pad = outline_px + 2
    grad = gold_gradient((m.width + pad * 2, m.height + pad * 2))
    t = outlined(m, grad, BROWN, outline_px)
    # segundo contorno escuro (destaca do fundo)
    a = t.split()[3]
    pad2 = scale + 2
    big = Image.new("L", (t.width + pad2 * 2, t.height + pad2 * 2), 0)
    big.paste(a, (pad2, pad2))
    ring = big.filter(ImageFilter.MaxFilter(scale * 2 + 1))
    out = Image.new("RGBA", big.size, (0, 0, 0, 0))
    out.paste(Image.new("RGBA", big.size, INK + (255,)), (0, 0), ring)
    out.alpha_composite(t, (pad2, pad2))
    return out


def small_text(text, scale, color, outline=INK, thickness=None):
    m = text_mask(text, os.path.join(FONTS, "Silkscreen-Regular.ttf"), 8, scale)
    return outlined(m, color, outline, thickness or scale)


def radial(size, center, radius, inner, outer):
    w, h = size
    img = Image.new("RGBA", size)
    px = img.load()
    for y in range(h):
        for x in range(w):
            t = min(1.0, math.hypot(x - center[0], y - center[1]) / radius)
            t = t * t * (3 - 2 * t)
            px[x, y] = tuple(int(inner[i] + (outer[i] - inner[i]) * t) for i in range(4))
    return img


SUITS = [
    ["....1....", "...111...", "..11111..", ".1111111.", "111111111", "111111111", ".11.1.11.", "....1....", "...111..."],
    [".11...11.", "1111.1111", "111111111", "111111111", ".1111111.", "..11111..", "...111...", "....1....", "........."],
    ["...111...", "..11111..", "..11111..", ".1.111.1.", "111111111", "111111111", ".11.1.11.", "....1....", "...111..."],
    ["....1....", "...111...", "..11111..", ".1111111.", "111111111", ".1111111.", "..11111..", "...111...", "....1...."],
]


def suit_pattern(size, cell, scale, color):
    """Padrão de naipes em diagonal (o mesmo do menu do jogo)."""
    w, h = size
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for cy in range(-1, h // cell + 2):
        for cx in range(-1, w // cell + 2):
            mask = SUITS[(cx + cy) % 4]
            ox = cx * cell + (cell - 9 * scale) // 2 + (cy % 2) * cell // 2
            oy = cy * cell + (cell - 9 * scale) // 2
            for y in range(9):
                for x in range(9):
                    if mask[y][x] == "1":
                        d.rectangle([ox + x * scale, oy + y * scale, ox + x * scale + scale - 1, oy + y * scale + scale - 1], fill=color)
    return img


def triangle(up_dir, size, color):
    img = Image.new("RGBA", (size + 8, size + 8), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = size
    pts = [(4, s + 4), (s + 4, s + 4), (s / 2 + 4, 4)] if up_dir else [(4, 4), (s + 4, 4), (s / 2 + 4, s + 4)]
    d.polygon(pts, fill=INK)
    inner = [(p[0] + (1 if i != 2 else 0) * (3 if p[0] < s / 2 + 4 else -3), p[1] + (-3 if up_dir else 3) * (1 if i != 2 else -1)) for i, p in enumerate(pts)]
    d.polygon(inner, fill=color)
    return img


def card_with_number(suit, number, k):
    """Carta da mão (fundo ui_<naipe>) com o número nos cantos, como no jogo."""
    card = load("Cards", "Deck", f"ui_{suit}_0.png")
    ink = (200, 30, 40) if suit in ("hearts", "diamonds") else (25, 25, 35)
    m = text_mask(str(number), os.path.join(FONTS, "Silkscreen-Regular.ttf"), 8, 2)
    num = Image.new("RGBA", m.size, ink + (255,))
    num.putalpha(m)
    card.alpha_composite(num, (3, 1))
    card.alpha_composite(num.rotate(180), (card.width - num.width - 3, card.height - num.height - 1))
    return up(card, k)


def trail_of(img, start, end, steps, max_alpha=110):
    """Fantasmas da carta voando (rastro)."""
    layer = Image.new("RGBA", (1, 1))
    ghosts = []
    for i in range(steps):
        t = i / steps
        a = int(max_alpha * (t ** 1.6))
        g = img.copy()
        g.putalpha(g.split()[3].point(lambda v, a=a: min(v, a)))
        ghosts.append((g, start[0] + (end[0] - start[0]) * t, start[1] + (end[1] - start[1]) * t))
    return ghosts


# ======================================================================================
def make_icon():
    S = 256
    img = radial((S, S), (S / 2, S * 0.42), S * 0.75, (92, 40, 140, 255), (16, 8, 30, 255))
    img.alpha_composite(suit_pattern((S, S), 32, 2, (0, 0, 0, 70)))
    glow = radial((S, S), (S / 2, S * 0.45), S * 0.38, (255, 200, 90, 120), (255, 200, 90, 0))
    img.alpha_composite(glow)

    jack = up(load("Cards", "Deck", "bosses_spades_0.png"), 2)
    queen = up(load("Cards", "Deck", "bosses_hearts_1.png"), 2)
    king = up(load("Cards", "Deck", "bosses_diamonds_2.png"), 2)
    paste_shadowed(img, rot(jack, 18), 74, 124, (6, 8), 140, 4)
    paste_shadowed(img, rot(king, -18), 182, 124, (6, 8), 140, 4)
    paste_shadowed(img, queen, 128, 112, (6, 10), 170, 5)

    # borda escura (vinheta) por baixo dos objetos da frente
    img.alpha_composite(radial((S, S), (S / 2, S / 2), S * 0.72, (0, 0, 0, 0), (0, 0, 0, 150)))

    # "?" grande: o número secreto
    q = title("?", 16, 6, 4)
    paste(img, q, 128, 192)

    # dado e fichas
    die = rot(up(load("Cards", "DIE_06.png"), 1), 14)
    paste_shadowed(img, die, 40, 210, (3, 4), 140, 3)
    paste_shadowed(img, up(load("Chips", "chip_red.png"), 2), 220, 214, (3, 4), 140, 3)
    paste_shadowed(img, up(load("Chips", "chip_blue.png"), 2), 206, 232, (3, 4), 140, 3)

    img.convert("RGB").save(os.path.join(OUT, "icon_256.png"))


# ======================================================================================
def make_cover():
    W, H = 1360, 850
    random.seed(7)

    # Fundo: castelo do jogo ampliado 5x, escurecido e puxado para o roxo
    bg = up(load("Backgrounds", "castle_bg.png"), 5)
    bg = bg.crop(((bg.width - W) // 2, bg.height - H, (bg.width - W) // 2 + W, bg.height))
    tint = Image.new("RGBA", (W, H), (70, 40, 120, 255))
    bg = ImageChops.multiply(bg, tint).point(lambda v: min(255, int(v * 1.45)))
    img = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    img.alpha_composite(bg)
    img.alpha_composite(suit_pattern((W, H), 64, 3, (0, 0, 0, 45)))

    # Luz atrás dos chefes
    img.alpha_composite(radial((W, H), (W * 0.70, H * 0.55), 520, (255, 190, 90, 120), (255, 190, 90, 0)))
    # Chão escuro embaixo
    floor = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fd = ImageDraw.Draw(floor)
    for y in range(H):
        t = max(0.0, (y - H * 0.70) / (H * 0.30))
        fd.line([(0, y), (W, y)], fill=(12, 6, 24, int(230 * min(1, t) ** 1.2)))
    img.alpha_composite(floor)

    # ---- Chefes em leque (lado direito)
    k = 4
    jack = up(load("Cards", "Deck", "bosses_spades_0.png"), k)
    queen = up(load("Cards", "Deck", "bosses_hearts_1.png"), k)
    king = up(load("Cards", "Deck", "bosses_diamonds_2.png"), k)
    cx, cy = 985, 500
    paste_shadowed(img, rot(jack, 14), cx - 200, cy + 28, (16, 22), 150, 10)
    paste_shadowed(img, rot(king, -14), cx + 200, cy + 28, (16, 22), 150, 10)
    paste_shadowed(img, queen, cx, cy - 10, (18, 26), 170, 12)

    # Pista ▲ / ▼ e CRITICAL! (como os textos que aparecem no jogo)
    hi = small_text("HIGHER", 4, (255, 138, 61))
    paste(img, triangle(True, 28, (255, 138, 61)), cx - 290, 300)
    paste(img, hi, cx - 195, 300)
    crit = small_text("CRITICAL!", 5, GOLD, INK, 5)
    paste(img, rot(crit, 7), cx + 175, 292)

    # ---- Dados e fichas caindo (ataques dos chefes), com rastro
    for (x, y, face, ang) in [(130, 330, 5, 20), (1260, 120, 3, -15), (1290, 720, 6, 30)]:
        die = up(load("Cards", f"DIE_0{face}.png"), 2)
        for i in range(5):
            g = rot(die, ang)
            g.putalpha(g.split()[3].point(lambda v, i=i: min(v, 20 + i * 18)))
            paste(img, g, x - 30 + i * 6, y - 60 + i * 12)
        paste_shadowed(img, rot(die, ang), x, y, (8, 10), 140, 5)
    for (x, y, c) in [(700, 760, "chip_red"), (1180, 790, "chip_black"), (560, 330, "chip_blue"), (1330, 420, "chip_white")]:
        paste_shadowed(img, up(load("Chips", f"{c}.png"), 4), x, y, (6, 8), 150, 4)

    # ---- Aventureiro arremessando (lado esquerdo) + carta voando com rastro
    adv = up(Image.open(os.path.join(ADV_DIR, "adv_cast_3.png")).convert("RGBA"), 11)
    adv_x, adv_y = 330, 540
    back = up(load("Cards", "Deck", "card_back_0.png"), 2)
    start, end = (adv_x + 230, adv_y - 20), (cx - 120, cy - 60)
    for g, x, y in trail_of(rot(back, -30), start, end, 7, 120):
        paste(img, g, x, y)
    paste_shadowed(img, rot(back, -30), end[0], end[1], (8, 10), 140, 5)
    # faíscas na ponta
    sp = ImageDraw.Draw(img)
    for i in range(14):
        a = random.uniform(0, math.tau)
        r = random.uniform(30, 70)
        x, y = end[0] + math.cos(a) * r, end[1] + math.sin(a) * r
        s = random.choice([4, 6, 8])
        sp.rectangle([x, y, x + s, y + s], fill=random.choice([GOLD, (255, 255, 255), (255, 138, 61)]))
    paste_shadowed(img, adv, adv_x, adv_y, (14, 10), 160, 8)

    # Mão de cartas embaixo à esquerda (o jogo é sobre escolher a carta certa)
    hand = [("clubs", 7), ("spades", 12), ("hearts", 4), ("diamonds", 9)]
    for i, (s, n) in enumerate(hand):
        c = rot(card_with_number(s, n, 2), 12 - i * 8)
        paste_shadowed(img, c, 90 + i * 66, 815 - abs(i - 1.5) * 10, (6, 8), 160, 5)

    # Vinheta (antes dos textos, para o título não escurecer)
    img.alpha_composite(radial((W, H), (W / 2, H / 2), 900, (0, 0, 0, 0), (0, 0, 0, 170)))

    # ---- Título e subtítulo
    t = title("Draw the Odds", 16, 7, 4)
    paste(img, t, W / 2, 112)
    sub = small_text("GUESS THE BOSS'S SECRET NUMBER", 4, (255, 255, 255))
    paste(img, sub, W / 2, 205)

    img.convert("RGB").save(os.path.join(OUT, "cover_1360x850.png"))

# ======================================================================================
# Capas em outros tamanhos: mesma arte da capa 1360x850, mas cada formato tem a sua composição
# (posições e escalas em pixels de cada tamanho). Escalas de sprite são inteiras = pixel art nítida.
LAYOUTS = {
    "cover_1920x1080.png": dict(
        W=1920, H=1080, title=(9, 150), sub=(5, 262), boss=(5, 1385, 650, 270), glow=(1370, 640, 700),
        hint=(1000, 395), crit=(1620, 380), adv=(14, 470, 700), hand=(3, 125, 1048, 95, 14),
        dice=[(170, 430, 5, 20), (1790, 150, 3, -15), (1830, 960, 6, 30)],
        chips=[(980, 990, "chip_red"), (1650, 1020, "chip_black"), (800, 420, "chip_blue"), (1880, 560, "chip_white")],
        chip_k=5, die_k=3, back_k=3, pattern=(80, 4)),
    "cover_800x1200.png": dict(
        W=800, H=1200, title=(6, 115), sub=(3, 192), boss=(3, 430, 560, 165), glow=(430, 540, 460),
        hint=(230, 330), crit=(600, 345), adv=(10, 250, 925), hand=(2, 470, 1150, 66, 10),
        dice=[(80, 470, 5, 20), (740, 790, 3, -15), (700, 1010, 6, 30)],
        chips=[(110, 1120, "chip_red"), (640, 900, "chip_black"), (90, 700, "chip_blue"), (735, 420, "chip_white")],
        chip_k=3, die_k=2, back_k=2, pattern=(56, 3)),
    "cover_800x800.png": dict(
        W=800, H=800, title=(6, 92), sub=(3, 165), boss=(3, 545, 445, 135), glow=(545, 440, 420),
        hint=(330, 255), crit=(650, 262), adv=(8, 200, 560), hand=(2, 70, 790, 62, 10),
        dice=[(60, 300, 5, 20), (745, 680, 3, -15)],
        chips=[(470, 735, "chip_red"), (620, 745, "chip_black"), (90, 450, "chip_blue")],
        chip_k=3, die_k=2, back_k=2, pattern=(56, 3)),
}


def make_cover_sized(name, L):
    W, H = L["W"], L["H"]
    random.seed(7)

    # Fundo: castelo ampliado até cobrir a tela, escurecido e puxado para o roxo
    raw = load("Backgrounds", "castle_bg.png")
    k = max(math.ceil(W / raw.width), math.ceil(H / raw.height))
    bg = up(raw, k)
    bg = bg.crop(((bg.width - W) // 2, bg.height - H, (bg.width - W) // 2 + W, bg.height))
    tint = Image.new("RGBA", (W, H), (70, 40, 120, 255))
    bg = ImageChops.multiply(bg, tint).point(lambda v: min(255, int(v * 1.45)))
    img = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    img.alpha_composite(bg)
    img.alpha_composite(suit_pattern((W, H), L["pattern"][0], L["pattern"][1], (0, 0, 0, 45)))

    gx, gy, gr = L["glow"]
    img.alpha_composite(radial((W, H), (gx, gy), gr, (255, 190, 90, 120), (255, 190, 90, 0)))
    floor = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fd = ImageDraw.Draw(floor)
    for y in range(H):
        t = max(0.0, (y - H * 0.70) / (H * 0.30))
        fd.line([(0, y), (W, y)], fill=(12, 6, 24, int(230 * min(1, t) ** 1.2)))
    img.alpha_composite(floor)

    u = L["boss"][0] / 4  # 1.0 = tamanho da capa original
    sh = lambda v: max(2, int(v * u))

    # ---- Chefes em leque
    bk, cx, cy, spread = L["boss"]
    jack = up(load("Cards", "Deck", "bosses_spades_0.png"), bk)
    queen = up(load("Cards", "Deck", "bosses_hearts_1.png"), bk)
    king = up(load("Cards", "Deck", "bosses_diamonds_2.png"), bk)
    paste_shadowed(img, rot(jack, 14), cx - spread, cy + spread * 0.14, (sh(16), sh(22)), 150, sh(10))
    paste_shadowed(img, rot(king, -14), cx + spread, cy + spread * 0.14, (sh(16), sh(22)), 150, sh(10))
    paste_shadowed(img, queen, cx, cy - spread * 0.05, (sh(18), sh(26)), 170, sh(12))

    hs = max(2, round(4 * u))
    hx, hy = L["hint"]
    hi = small_text("HIGHER", hs, (255, 138, 61))
    tri = triangle(True, int(7 * hs), (255, 138, 61))
    gap = 2 * hs
    left = hx - (tri.width + gap + hi.width) / 2
    paste(img, tri, left + tri.width / 2, hy)
    paste(img, hi, left + tri.width + gap + hi.width / 2, hy)
    crit = small_text("CRITICAL!", hs + 1, GOLD, INK, hs + 1)
    paste(img, rot(crit, 7), *L["crit"])

    # ---- Dados e fichas caindo, com rastro
    dk = L["die_k"]
    for (x, y, face, ang) in L["dice"]:
        die = up(load("Cards", f"DIE_0{face}.png"), dk)
        for i in range(5):
            g = rot(die, ang)
            g.putalpha(g.split()[3].point(lambda v, i=i: min(v, 20 + i * 18)))
            paste(img, g, x - 15 * dk + i * 3 * dk, y - 30 * dk + i * 6 * dk)
        paste_shadowed(img, rot(die, ang), x, y, (4 * dk, 5 * dk), 140, 5)
    for (x, y, c) in L["chips"]:
        paste_shadowed(img, up(load("Chips", f"{c}.png"), L["chip_k"]), x, y, (sh(6), sh(8)), 150, 4)

    # ---- Aventureiro + carta voando até o chefe da esquerda
    ak, adv_x, adv_y = L["adv"]
    adv = up(Image.open(os.path.join(ADV_DIR, "adv_cast_3.png")).convert("RGBA"), ak)
    back = up(load("Cards", "Deck", "card_back_0.png"), L["back_k"])
    start = (adv_x + ak * 21, adv_y - ak * 2)
    end = (cx - spread * 0.6, cy - spread * 0.3)
    ang = -30
    for g, x, y in trail_of(rot(back, ang), start, end, 7, 120):
        paste(img, g, x, y)
    paste_shadowed(img, rot(back, ang), end[0], end[1], (sh(8), sh(10)), 140, 5)
    sp = ImageDraw.Draw(img)
    for i in range(14):
        a = random.uniform(0, math.tau)
        r = random.uniform(30, 70) * u
        x, y = end[0] + math.cos(a) * r, end[1] + math.sin(a) * r
        s = random.choice([4, 6, 8]) * max(1, round(u))
        sp.rectangle([x, y, x + s, y + s], fill=random.choice([GOLD, (255, 255, 255), (255, 138, 61)]))
    paste_shadowed(img, adv, adv_x, adv_y, (sh(14), sh(10)), 160, sh(8))

    # ---- Mão de cartas
    ck, x0, y0, step, tilt = L["hand"]
    hand = [("clubs", 7), ("spades", 12), ("hearts", 4), ("diamonds", 9)]
    for i, (s, n) in enumerate(hand):
        c = rot(card_with_number(s, n, ck), 12 - i * 8)
        paste_shadowed(img, c, x0 + i * step, y0 - abs(i - 1.5) * tilt, (sh(6), sh(8)), 160, 5)

    img.alpha_composite(radial((W, H), (W / 2, H / 2), math.hypot(W, H) * 0.56, (0, 0, 0, 0), (0, 0, 0, 170)))

    # ---- Título e subtítulo
    ts, ty = L["title"]
    paste(img, title("Draw the Odds", 16, ts, max(3, ts // 2)), W / 2, ty)
    ss, sy = L["sub"]
    paste(img, small_text("GUESS THE BOSS'S SECRET NUMBER", ss, (255, 255, 255)), W / 2, sy)

    img.convert("RGB").save(os.path.join(OUT, name))


os.makedirs(OUT, exist_ok=True)
make_icon()
make_cover()
for name, layout in LAYOUTS.items():
    make_cover_sized(name, layout)
print("ok")
