# Draw the Odds — portais web (GamePix, CrazyGames, Poki)

Atualizado em 16/11/2026.

## Como o código está organizado

- `Assets/_Game/Scripts/Core/Portal.cs` é a **única** classe do jogo que conversa com o portal: anúncio entre fases, momento bom/ruim, idioma, se pode acessar servidor externo e se o portal pausou o jogo.
- Cada portal entra com um símbolo de compilação (`DTO_GAMEPIX`; depois `DTO_POKI` e `DTO_CRAZY`). Sem nenhum símbolo (itch/Windows), tudo em `Portal` é "não faz nada".
- `PortalRunner.cs` silencia **todo** o som enquanto o portal pausa o jogo (anúncio ou aba escondida). É necessário porque a nossa música ignora a pausa do `AudioListener`.
- `Editor/PortalBuild.cs` tem os menus. O plugin da GamePix só compila com a chave ligada: os asmdefs dele têm `defineConstraints: DTO_GAMEPIX`, e a `.a` nativa só entra na build WebGL com a chave.

### Onde o jogo chama o portal

| Momento | Chamada |
|---|---|
| Chefe derrotado (fora do tutorial) | `Portal.HappyMoment()` |
| Player morreu | `Portal.SadMoment()` |
| Derrota → RECOMEÇAR | `Portal.Interstitial()` e depois recarrega |
| Entre chefes (tela coberta pela cortina de cartas) | `Portal.Interstitial()` |
| Vitória ou derrota → MAIN MENU | `Portal.Interstitial()` |
| Derrota → **► ANÚNCIO: REPETIR CHEFE** | `Portal.Rewarded()` (1x por chefe/rodada; só recompensa se o anúncio terminar) |
| Pausa pelo portal (anúncio / aba escondida) | `GameManager.Update` congela o input e o relógio da fusão; `PortalRunner` tira o som |

Trava de frequência (`Portal.cs`): o primeiro anúncio só sai depois de 90 s de jogo e, depois disso, no máximo 1 a cada 120 s. Não é um anúncio por tempo: ele só aparece nas pausas naturais listadas acima.

## GamePix

### Gerar a build
1. Com o Unity aberto, use o menu **Draw the Odds/Portal/GamePix**. Ele troca o alvo para WebGL, liga o `DTO_GAMEPIX` e o Unity recompila.
2. Use **Draw the Odds/Build GamePix**. O resultado sai em `Builds/GamePix/Janta_Games_Draw_the_Odds_1.0.gpx` (o nome exato está no Console). **Só esse `.gpx` é enviado.**
3. Volte com **Draw the Odds/Portal/itch (no SDK)** antes de gerar builds para o itch. O `Build WebGL` se recusa a rodar com a chave da GamePix ligada.

O build usa o modo *No override textures/sounds* do plugin, porque as texturas já estão sem compressão (pixel art). Também guarda e devolve as configurações do Player que o plugin muda (template, compressão, memória, stripping...).

### O que foi adaptado às guidelines
- **Anúncios** só nas pausas naturais. O jogo fica parado e mudo durante o anúncio, inclusive a música.
- **Ranking global** (Supabase) liberado na GamePix: as regras proíbem SDKs, anúncios e analytics de terceiros, não um servidor próprio do jogo. Na Poki ele precisará ficar desligado (`Portal.AllowExternalRequests`).
- **Idioma:** se o jogador nunca escolheu um, o jogo usa o idioma que a GamePix informa (português → PT; qualquer outro → inglês).
- **Sem botão de sair:** o menu já não tinha.
- **Tela de loading e pausa/retomada** quando a aba some: o plugin cuida disso sozinho.
- **Proporção:** a tela padrão vira 960×540 (16:9) só durante a build da GamePix.
- **Tamanho do iframe:** nos portais a Pixel Perfect Camera usa `StretchFill` (`PortalRunner.FitCamera`). Sem isso, em telas como 960×540 ela ficava em 1× e o mundo aparecia pequeno, com bordas vazias. **O itch tem o mesmo problema** no embed 960×600 (só some em tela cheia); vale aplicar lá também.
- **Build Profile:** os menus de portal desativam o Build Profile ativo do Unity 6. Senão, o símbolo e os ajustes iam para o profile, e o plugin, que desliga o profile antes de compilar, gerava a build **sem o SDK**.

### Enviar
1. Em my.gamepix.com → Games → adicionar jogo → enviar o `.gpx`.
2. Ícone e capa com o título "Draw the Odds" (`Docs/banner.png` serve de base). Descrição com tema, objetivo e controles.
3. O jogo hoje é **só desktop** (teclado + mouse). Se o formulário perguntar, marque desktop. Para valer no celular, falta o controle por toque (ver ideias).
4. "Allow Distribution": recomendo marcar (mais receita).

### Atenções
- O plugin nem sempre roda em `localhost` com o SDK de verdade: os anúncios só aparecem no ambiente da GamePix. Testar o fluxo no Editor (os anúncios simulados respondem na hora).
- Créditos de assets de terceiros (Adventurer, Boardgame Icons, sons, fontes) precisam permitir uso comercial com anúncios. **Conferir as licenças das músicas, do ToyZone e das fichas** antes de enviar (pendência antiga do tracking).

## CrazyGames (integrado em 17/11)
SDK oficial **5.39** (`Assets/CrazySDK`, baixado de `sdk.crazygames.com/UnityCrazySDK.unitypackage`, sem as cenas Demo). Só compila com `DTO_CRAZY` (asmdefs `CrazySDK`, `CrazySDK.Editor`, `CrazyGamesDefineManager` e o `.jslib` com `defineConstraints`). Template `Assets/WebGLTemplates/CrazyGames` (+ `user-select: none`).

### Gerar a build
1. **Draw the Odds/Portal/CrazyGames** (recompila).
2. **Draw the Odds/Build CrazyGames**: usa o builder oficial deles (release, Brotli, texturas ASTC e versões de memória limitada para celular, relatório que o portal lê). Leva ~20–25 min. Sai em `Builds/CrazyGamesRelease`: **envie todos os arquivos dessa pasta**.
3. **Draw the Odds/Portal/itch (no SDK)** para voltar.

Testar localmente: `python Tools/serve_webgl.py Builds/CrazyGamesRelease 8126` → `http://localhost:8126` (o SDK roda em modo local, com anúncios de demonstração). **Use `localhost`, não `127.0.0.1`:** o SiteLock da CrazyGames congela o jogo em qualquer outro endereço (código 3 no console). É proteção contra cópia, e no site deles roda normal.

### O que o jogo faz na CrazyGames
| Regra | Como ficou |
|---|---|
| `gameplayStart/Stop` | `PortalRunner` avisa a cada mudança: luta rolando (sem pausa e sem anúncio) = start; menu, pausa, transição, derrota e vitória = stop |
| Anúncio midgame | Entre chefes (cortina fechada) e no RECOMEÇAR. **Nunca** em botão de navegação (o MAIN MENU volta sem anúncio) |
| Recompensado | "► AD: RETRY THIS BOSS". Some se os anúncios estiverem desligados (Basic Launch), se houver adblock ou se o pedido falhar com `adsDisabled`: nada de botão morto. Recompensa só no `adFinished` |
| Som | Zerado quando o anúncio aparece (`adStarted`) e enquanto o jogador deixa o som desligado no botão do site da CrazyGames (`Settings.muteAudio` + `AddSettingsChangeListener`, em `Portal.PlatformMuted`) |
| `happytime` | Só na vitória final (momento raro) |
| Idioma | `SystemInfo.locale` → inglês, português, espanhol, francês, alemão ou italiano (o resto → inglês), só se o jogador nunca escolheu nas Opções |
| Compressão | Brotli **sem** "Decompression fallback": a CrazyGames recusa arquivos `.unityweb`. O `Build CrazyGames` desliga o fallback só durante a build (a do itch continua com ele) e apaga a pasta antiga antes |
| Controles de toque | Ligam também quando o SDK diz que o aparelho é `mobile`/`tablet` |
| Tecla Esc | O jogo não depende dela: **P** pausa (textos dizem "P/ESC") e o toque tem o botão II |
| Ranking global | Mantido (Supabase); as runs vão com `platform = crazygames` |
| Tela cheia | Nenhum botão próprio (a CrazyGames tem o dela) |

### Enviar (developer.crazygames.com)
- Submit a game → upload da pasta `Builds/CrazyGamesRelease` → testar no **QA tool** deles.
- Orientação: paisagem. Marcar que funciona em celular (tem controles de toque). **Data Module: "No"** (o jogo salva com PlayerPrefs/localStorage).
- Começa em **Basic Launch** (sem anúncios, 7–21 dias medindo tempo de jogo e retenção); a Full Launch libera os anúncios.
- Pendências para a Full Launch: salvar o progresso no Data Module (sincroniza entre aparelhos), ajustar com os números da Basic Launch.

## Poki (depois)
- `DTO_POKI`: `PokiSDK.init()`, `gameLoadingFinished()` no menu, `gameplayStart/Stop`, `commercialBreak()` nos mesmos pontos, `rewardedBreak()` para o "continuar".
- **Nenhuma requisição externa** (ranking só local, como na GamePix), e as fontes já estão embutidas (ok).
- A Poki é exigente com **mobile** e com **tempo até a diversão** (o tutorial atual é longo). É a última da fila: entra depois do controle por toque.

## Balanceamento para portal
**Aplicado em 17/11:** continuar com anúncio recompensado (item 1), JOGAR direto no Valete com dicas curtas (item 2) e o modo infinito liberado ao vencer a Dama (item 4, com "regras da casa" a partir da rodada 4). O item 3 (Valete mais fácil) espera os números reais dos portais.

Propostas originais:
O público de portal joga sessões curtas e desiste rápido. Pontos que mais pesam:
1. **Morrer recomeça do Valete.** Para portal, sugiro um **"CONTINUAR DESTE CHEFE" com anúncio recompensado** (🎬, opcional, ao lado de um "recomeçar" do mesmo tamanho). Todas as 3 plataformas aceitam esse uso e é o anúncio que mais rende.
2. **Tutorial obrigatório no PLAY:** inverter para o "jogar" ir direto ao Valete com dicas curtas na tela, deixando o tutorial completo como opção. As métricas de portal medem os primeiros 60 s.
3. **Valete mais fácil** (HP 12 → 10 ou mais orbes no começo), para a primeira vitória sair rápido. A dificuldade sobe a partir da Dama.
4. **Run curta = 3 chefes.** Para dar mais tempo de jogo por sessão sem fazer arte nova: um **modo infinito** (chefes repetem com naipe/número/HP maiores; o ranking passa a ser "quantos chefes você venceu").

## Ideias para expandir
- ~~**Controle por toque**~~ (feito em 17/11: joystick, botões, cartas arrastáveis, opção TOUCH AUTO/ON/OFF): direcional virtual + botões pular/dash, toque no chefe para arremessar a carta selecionada e cartas tocáveis na mão.
- **Modo infinito / diário** (semente do dia, todo mundo com o mesmo baralho; ranking diário).
- **Coringas para o jogador** (relíquias entre chefes, estilo roguelite): "Ás na manga" (1 crítico garantido), "dado viciado" (mostra se o número é par/ímpar), "mão de 8 cartas"... São 3 opções para escolher depois de cada chefe.
- **Mais chefes** usando o mesmo sistema de faces do dado (`BossData.dieFaces`): Ás negro, Dama de Copas que cura, Valete gêmeo (dois números).
- **Conquistas locais** (crítico de primeira, vencer sem dano, sem redraw) que liberam cosméticos de verso de carta.

## Testar localmente
- `Builds/WebGL/index.html` servido em `localhost` com **`?fakeads=1`**: o botão do anúncio recompensado aparece e "funciona" (sem anúncio de verdade).
- **`?devkeys=1`**: F1 abre uma fusão, F2 mata o player, F3 libera o infinito, F4 acerto crítico, F5 pacote de cartas.
- Opções → TOUCH → ON para testar os controles de toque com o mouse.

## Idiomas e teclas (17/11)
- **Idiomas:** inglês, português, espanhol, francês, alemão e italiano (`Core/Loc.cs`: tabela `R(...)` com 6 colunas + `Loc.Pick(en, pt, es, fr, de, it)` nas frases montadas). Só idiomas de alfabeto latino: a fonte Silkscreen não tem cirílico, turco, polonês nem japonês. Para eles, seria preciso uma fonte de fallback (ex.: uma pixel font com Latin Extended/Cyrillic).
- **Teclas configuráveis:** Opções → TECLAS (`Core/KeyBindings.cs` + `UI/KeyBindingsPanel.cs`). São 9 ações (esquerda, direita, descer, pular, dash, nova mão, espaços 1 e 2 da fusão, pausa), salvas no PlayerPrefs. As setas e o ESC sempre funcionam, e o mouse e as teclas 1-0 são fixos. Os textos (HUD, dicas, tutorial, Como Jogar, fusão) mostram a tecla escolhida. Controle (gamepad) também funciona: analógico/direcional, A pula, X/RB dá dash.
- **Toque:** o botão de nova mão agora é uma carta (verso do baralho) com setas curvas (`UI/PixelIcons.cs`) e mostra quantas trocas restam (x3).
