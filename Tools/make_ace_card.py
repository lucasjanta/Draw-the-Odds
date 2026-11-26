# Gera a carta do Ás (chefe secreto) no mesmo estilo das cartas 58×80 dos chefes.
import sys
from PIL import Image
D, OUT = sys.argv[1], sys.argv[2]
king = Image.open(f"{D}/bosses_spades_2.png").convert("RGBA")
im = king.copy()
W, H = im.size
DARK, GOLD, GOLD_D, RED, WHITE, GREY = (30,30,40,255), (247,190,57,255), (176,120,30,255), (222,40,0,255), (255,255,255,255), (148,148,156,255)
px = im.load()
# miolo escuro com moldura dourada dupla
for y in range(8, 73):
    for x in range(8, 50):
        px[x, y] = DARK
for x in range(9, 49):
    px[x, 9] = GOLD; px[x, 71] = GOLD
for y in range(9, 72):
    px[9, y] = GOLD; px[48, y] = GOLD
for x in range(11, 47, 2):
    px[x, 11] = GOLD_D; px[x, 69] = GOLD_D
# espada (naipe) grande, 25 px de largura
spade = [
"............#............",
"...........###...........",
"..........#####..........",
".........#######.........",
"........#########........",
".......###########.......",
"......#############......",
".....###############.....",
"....#################....",
"...###################...",
"..#####################..",
".#######################.",
"#########################",
"#########################",
"#########################",
"#########################",
".#######################.",
"..#########.#.#########..",
"...#######..#..#######...",
".....###....#....###.....",
"...........###...........",
"..........#####..........",
".........#######.........",
]
ox, oy = 29 - 12, 40 - len(spade) // 2
for j, row in enumerate(spade):
    for i, c in enumerate(row):
        if c == "#":
            # contorno escuro dourado nas bordas, miolo dourado claro
            edge = any((0 <= j+dj < len(spade) and 0 <= i+di < len(row) and spade[j+dj][i+di] == ".") or not (0 <= j+dj < len(spade) and 0 <= i+di < len(row))
                       for dj, di in ((1,0),(-1,0),(0,1),(0,-1)))
            px[ox + i, oy + j] = GOLD_D if edge else GOLD
# rubi no meio da espada
for dx, dy in ((0,0),(1,0),(-1,0),(0,1),(0,-1)):
    px[29 + dx, 41 + dy] = RED
px[28, 40] = WHITE
# estrelinhas
for (x, y) in ((14, 16), (43, 18), (15, 63), (42, 62), (13, 40), (45, 40)):
    px[x, y] = GOLD; px[x+1, y] = GOLD_D; px[x-1, y] = GOLD_D; px[x, y+1] = GOLD_D; px[x, y-1] = GOLD_D
# letra "A" no canto (troca o K)
for y in range(8, 15):
    for x in range(1, 8):
        px[x, y] = WHITE
A = ["..#..", ".#.#.", "#...#", "#...#", "#####", "#...#", "#...#"]
for j, row in enumerate(A):
    for i, c in enumerate(row):
        if c == "#": px[2 + i, 8 + j] = DARK
# canto de baixo = canto de cima girado 180°
corner = im.crop((0, 8, 8, 27)).rotate(180)
im.paste(corner, (W - 8, H - 8 - 19))
im.save(OUT)
