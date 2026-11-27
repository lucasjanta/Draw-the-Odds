-- Gera o aldeão com um chapéu escolhido (guarda-roupa da taverna, 27/11).
-- Base: o adventurer ORIGINAL + a mesma troca de paleta do villager.lua (que fez o aldeão de palha).
-- Uso (Aseprite CLI):
--   Aseprite.exe -b adventurer_original.aseprite --script-param hat=crown --script-param out=villager_crown.aseprite --script villager_hats.lua
-- Chapéus: none | straw | cap | feather | tophat | jester | crown
-- Todos os quadros: o chapéu é desenhado a partir da linha mais alta do cabelo (só na camada "head").
local spr = app.activeSprite
app.command.ChangePixelFormat{ format = "rgb" }
local pc = app.pixelColor
local HAT = app.params["hat"] or "straw"
local function key(r,g,b) return r*65536+g*256+b end
local map = {}
local function M(a,b) map[key(a[1],a[2],a[3])] = b end
-- (mesma paleta do villager.lua: o jogo recolore estas cores para as roupas)
M({143,50,50},{78,98,46}) M({171,67,67},{104,128,58}) M({217,87,99},{138,160,76})
M({89,86,82},{140,122,92}) M({107,105,112},{170,152,116}) M({132,126,135},{201,184,146})
M({203,219,252},{238,195,154}) M({155,173,183},{201,149,127}) M({165,174,234},{217,160,102}) M({133,144,159},{190,140,110})
M({158,100,81},{116,92,58}) M({171,118,94},{136,110,70}) M({143,94,71},{98,78,50})
M({45,56,53},{70,52,38}) M({58,69,66},{90,68,48})
local hair = { [key(128,77,54)]=true, [key(143,86,59)]=true, [key(112,70,52)]=true, [key(97,65,43)]=true }
local function C(r,g,b) return pc.rgba(r,g,b,255) end
local OUT = C(40,26,20)

local W, H = spr.width, spr.height
local targets = {}
local function walk(l) if l.isGroup then for _,c in ipairs(l.layers) do walk(c) end else table.insert(targets, l) end end
for _,l in ipairs(spr.layers) do if l.name == "Hero" then walk(l) end end

-- Cada chapéu recebe: img, put(x,y,c), top (linha do topo do cabelo), xmin/xmax (largura do cabelo), cx0/cx1 (copa)
local hats = {}

hats.straw = function(img, put, top, xmin, xmax, cx0, cx1)
  local STRAW, STRAW_D, STRAW_L, BAND = C(222,186,98), C(176,138,62), C(246,214,130), C(150,62,48)
  for x=cx0, cx1 do put(x, top-1, STRAW_L) put(x, top, STRAW) put(x, top+1, BAND) end
  for x=xmin-2, xmax+2 do put(x, top+2, ((x % 2) == 0) and STRAW or STRAW_D) end
  for x=cx0, cx1 do put(x, top-2, OUT) end
  put(cx0-1, top-1, OUT) put(cx1+1, top-1, OUT) put(cx0-1, top, OUT) put(cx1+1, top, OUT) put(cx0-1, top+1, OUT) put(cx1+1, top+1, OUT)
  put(xmin-3, top+2, OUT) put(xmax+3, top+2, OUT)
end

-- Boina (flat cap): copa baixa e larga + aba para a frente (o personagem olha para a direita)
hats.cap = function(img, put, top, xmin, xmax, cx0, cx1)
  local A, D, L = C(120,104,92), C(88,74,64), C(150,134,118)
  for x=xmin-1, xmax+1 do put(x, top, A) put(x, top+1, D) end
  for x=xmin, xmax-1 do put(x, top-1, L) end
  for x=xmax+1, xmax+3 do put(x, top+1, D) end
  for x=xmin, xmax-1 do put(x, top-2, OUT) end
  put(xmin-1, top-1, OUT) put(xmax, top-1, OUT) put(xmax+1, top-1, OUT)
  put(xmin-2, top, OUT) put(xmin-2, top+1, OUT) put(xmax+2, top, OUT) put(xmax+4, top+1, OUT)
  for x=xmax+2, xmax+3 do put(x, top, OUT) end
end

-- Gorro com pena: gorro verde e uma pena vermelha atrás
hats.feather = function(img, put, top, xmin, xmax, cx0, cx1)
  local G, GD, GL, F, FL = C(64,120,64), C(44,88,48), C(96,150,88), C(210,56,48), C(240,110,90)
  for x=xmin-1, xmax+1 do put(x, top, G) put(x, top+1, GD) end
  for x=xmin, xmax do put(x, top-1, GL) end
  for x=xmin, xmax do put(x, top-2, OUT) end
  put(xmin-1, top-1, OUT) put(xmax+1, top-1, OUT) put(xmin-2, top, OUT) put(xmin-2, top+1, OUT) put(xmax+2, top, OUT) put(xmax+2, top+1, OUT)
  -- pena: sobe na diagonal saindo da parte de trás
  local fx = xmin
  put(fx, top-3, F) put(fx-1, top-4, F) put(fx-1, top-5, FL) put(fx-2, top-6, FL) put(fx, top-4, OUT) put(fx-2, top-5, OUT) put(fx-1, top-6, OUT) put(fx-3, top-6, OUT)
end

-- Cartola: alta e preta com fita vermelha
hats.tophat = function(img, put, top, xmin, xmax, cx0, cx1)
  local K, KL, R = C(36,32,40), C(64,58,72), C(170,40,50)
  for y=top-6, top do for x=cx0, cx1 do put(x, y, K) end end
  for y=top-6, top-1 do put(cx0, y, KL) end
  for x=cx0, cx1 do put(x, top-1, R) end
  for x=xmin-2, xmax+2 do put(x, top+1, K) end
  for x=cx0, cx1 do put(x, top-7, OUT) end
  for y=top-6, top do put(cx0-1, y, OUT) put(cx1+1, y, OUT) end
  put(xmin-3, top+1, OUT) put(xmax+3, top+1, OUT)
end

-- Chapéu de bobo: faixa + duas pontas (roxo e amarelo) com guizos
hats.jester = function(img, put, top, xmin, xmax, cx0, cx1)
  local P, Y, B = C(130,60,170), C(236,196,64), C(255,236,140)
  for x=xmin-1, xmax+1 do put(x, top+1, (x % 2 == 0) and P or Y) end
  for x=xmin, xmax do put(x, top, P) end
  -- ponta de trás (roxa) e da frente (amarela)
  put(xmin, top-1, P) put(xmin-1, top-2, P) put(xmin-2, top-3, P) put(xmin-3, top-3, B)
  put(xmax, top-1, Y) put(xmax+1, top-2, Y) put(xmax+2, top-3, Y) put(xmax+3, top-3, B)
  put(xmin+1, top-1, OUT) put(xmin, top-2, OUT) put(xmin-1, top-3, OUT) put(xmin-3, top-4, OUT) put(xmin-4, top-3, OUT)
  put(xmax-1, top-1, OUT) put(xmax, top-2, OUT) put(xmax+1, top-3, OUT) put(xmax+3, top-4, OUT) put(xmax+4, top-3, OUT)
  for x=xmin+2, xmax-2 do put(x, top-1, OUT) end
  put(xmin-2, top+1, OUT) put(xmax+2, top+1, OUT)
end

-- Coroa: faixa dourada com 3 pontas e uma pedra vermelha
hats.crown = function(img, put, top, xmin, xmax, cx0, cx1)
  local G, GD, GL, R = C(240,196,64), C(190,140,40), C(255,236,140), C(210,40,50)
  local mid = math.floor((cx0 + cx1) / 2)
  for x=cx0, cx1 do put(x, top, G) put(x, top+1, GD) end
  put(cx0, top-1, GL) put(cx0, top-2, GL) put(mid, top-1, G) put(mid, top-2, G) put(mid, top-3, GL) put(cx1, top-1, G) put(cx1, top-2, G)
  put(mid, top, R)
  put(cx0, top-3, OUT) put(mid, top-4, OUT) put(cx1, top-3, OUT)
  put(cx0-1, top-2, OUT) put(cx0-1, top-1, OUT) put(cx0-1, top, OUT) put(cx0-1, top+1, OUT)
  put(cx1+1, top-2, OUT) put(cx1+1, top-1, OUT) put(cx1+1, top, OUT) put(cx1+1, top+1, OUT)
  for x=cx0+1, cx1-1 do if x ~= mid then put(x, top-1, OUT) end end
  put(mid-1, top-2, OUT) put(mid+1, top-2, OUT) put(mid-1, top-3, OUT) put(mid+1, top-3, OUT)
end

for _,layer in ipairs(targets) do
local isHead = layer.name == "head"
for _,cel in ipairs(layer.cels) do
  local img = Image(W, H, ColorMode.RGB)
  img:drawImage(cel.image, cel.position)
  -- 1) paleta
  for it in img:pixels() do
    local v = it()
    if pc.rgbaA(v) > 0 then
      local n = map[key(pc.rgbaR(v), pc.rgbaG(v), pc.rgbaB(v))]
      if n then it(pc.rgba(n[1],n[2],n[3],255)) end
    end
  end
  -- 2) chapéu
  local top = nil
  for y=0,H-1 do
    for x=0,W-1 do
      local v = img:getPixel(x,y)
      if pc.rgbaA(v) > 0 and hair[key(pc.rgbaR(v),pc.rgbaG(v),pc.rgbaB(v))] then top = y break end
    end
    if top then break end
  end
  if top and isHead and hats[HAT] then
    local xmin, xmax = W, -1
    for y=top, math.min(H-1, top+4) do
      for x=0,W-1 do
        local v = img:getPixel(x,y)
        if pc.rgbaA(v) > 0 and hair[key(pc.rgbaR(v),pc.rgbaG(v),pc.rgbaB(v))] then
          if x < xmin then xmin = x end
          if x > xmax then xmax = x end
        end
      end
    end
    if xmax >= xmin then
      local function put(x,y,c) if x>=0 and x<W and y>=0 and y<H then img:drawPixel(x,y,c) end end
      local cx0, cx1 = xmin + 1, xmax - 1
      if cx1 - cx0 < 4 then local c = math.floor((xmin + xmax) / 2) cx0, cx1 = c - 2, c + 2 end
      hats[HAT](img, put, top, xmin, xmax, cx0, cx1)
    end
  end
  cel.image = img
  cel.position = Point(0,0)
end
end
spr:saveAs(app.params["out"])
