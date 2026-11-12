-- Exporta cada quadro do .ase das cartas como PNG, com o nome da tag + índice dentro da tag.
-- Ex.: bosses_spades_0.png (J), bosses_spades_1.png (Q), bosses_spades_2.png (K), ui_hearts_0.png, card_back_0.png
-- Uso (o Unity chama isto pelo menu "Draw the Odds/Import Card Art"):
--   Aseprite.exe --batch --script-param file=<arquivo.ase> --script-param out=<pasta> --script export_cards.lua
local spr = app.open(app.params.file)
local out = app.params.out
local count = 0
for _, tag in ipairs(spr.tags) do
  local i = 0
  for f = tag.fromFrame.frameNumber, tag.toFrame.frameNumber do
    local img = Image(spr.spec)
    img:drawSprite(spr, f)
    img:saveAs(out .. "/" .. tag.name .. "_" .. i .. ".png")
    i = i + 1
    count = count + 1
  end
end
print("exported " .. count)
