# Draw the Odds: história + plano para virar um jogo completo

## Contexto
O jogo (Unity 6, `Game Dev\Unity\Games\Draw the Odds`) já tem bastante coisa: 3 chefes (Valete, Dama, Rei), tutorial com o Coringa, fusão de cartas, modo infinito, 3 dificuldades, ranking no Supabase, controle por toque, PT/EN, e as integrações com GamePix e CrazyGames (`Portal.cs`). O `Docs/ROADMAP.md` já prevê relíquias/coringas, novos chefes, Android, desafio diário e conquistas.
O que falta é um **motivo** para lutar (história) e o que separa uma "jam polida" de um "produto": progressão entre runs, identidade própria e as exigências de cada loja. Este plano encaixa a história no roadmap que já existe, sem refazer nada.

---

## 1. A história: "O Reino da Banca"

**Premissa (cabe num cartaz):**
> No Reino de Baralho, quem decide tudo (impostos, colheitas, quem vai pra forca) é a Família Real, num jogo de cartas que eles *sempre* ganham. Dizem que é sorte. Você, um aldeão (o **Sete de Paus**, a carta mais baixa do baralho), sabe que é **blefe**. Cada nobre esconde um número: o segredo da trapaça dele. Descubra, exponha e chegue ao trono.

**Por que encaixa na mecânica (tudo que já existe ganha um sentido):**
| Mecânica atual | Na história |
|---|---|
| Número secreto do chefe | O "podre" do nobre: o número com que ele marca as cartas |
| Pista ▲/▼ | **Boatos** do povo: "dizem que é mais alto..." |
| Acerto exato = crítico | **"Pego no blefe!"**: o nobre é exposto na frente da corte |
| Número muda depois do crítico | O nobre **muda a versão da história** |
| A Dama troca o número na metade da vida | Ela é a mestra da intriga e reescreve a mentira |
| O Rei troca a cada 25 s | **Propaganda real**: a "verdade oficial" muda o tempo todo |
| Fusão com o tempo parado | "Jogo de mão": o aldeão trapaceia de volta |
| ♥ cura | Ajuda do povo |
| Coringa do tutorial | O **Bobo da Corte**, o informante que te ensina a ler os blefes |

**Elenco:**
- **Bobo da Corte (Coringa):** é tutorial e narrador ao mesmo tempo. Sarcástico, sabe de tudo, e é quem dá os boatos.
- **Valete, o Coletor:** cobra impostos com dados viciados. É o chefe "honesto" (o número dele nunca mente).
- **Dama, a Intrigante:** espalha mentiras e troca o próprio número.
- **Rei, o Propagandista:** tudo nele é encenação.
- **O Ás, "Ace up the Sleeve / Ás na Manga" (chefe final secreto):** quem controla tudo por baixo dos panos. A Família Real era só a mão que ele jogava. Ele aparece depois do Rei **só se uma condição for cumprida** (ex.: destravar todos os boatos do Rei, ou vencer no Normal+ sem usar redraw). Senão, o jogo acaba no final "normal", com uma pista do Bobo ("o Rei nunca foi a carta mais alta..."). Mecânica: dois números secretos (já está no roadmap). Ao ser derrotado, ele abre a escolha dos finais.
  - Para não haver conflito de nome, a relíquia "Ás na Manga" do roadmap passa a se chamar **"Marked Card / Carta Marcada"**.
- **Final (depois do Ás):** você pode **pegar o trono** (e virar o próximo Rei, o que justifica o modo infinito: "o reino continua dando cartas") ou **rasgar o baralho** (final "livre"). Sai um final diferente por escolha, e custa só 2 telas.

**Vocabulário global: pôquer no inglês, truco como sabor local**
O truco quase não é conhecido no mercado de língua inglesa (é jogado no Brasil, Argentina, Uruguai, Espanha e Itália). O pôquer é universal, e o inglês já tem as expressões prontas: *bluff*, *raise*, *call*, *fold*, *all-in*, *poker face*, *call someone's bluff*. Por isso a mecânica é desenhada com termos de pôquer, e o `Loc` troca só o grito em PT/ES:

| Momento | EN (padrão) | PT-BR | ES |
|---|---|---|---|
| Aposta do jogador | **RAISE!** | **TRUCO!** | **¡TRUCO!** |
| Chefe aumenta | **RE-RAISE!** | **SEIS!** | **¡QUIERO RETRUCO!** |
| Aceitar / desistir | CALL / FOLD | DESCE / CORRO | QUIERO / NO QUIERO |
| Acerto exato (crítico) | **BLUFF CALLED!** | **PEGO NO BLEFE!** | **¡FAROL!** (blefe no truco argentino) |

**Mecânicas temáticas:**
1. **RAISE / TRUCO (aposta de risco):** um uso por luta. A próxima carta dá ×3 se ficar a até 2 do número, e você leva dano se errar por 6 ou mais. Às vezes o chefe responde com **RE-RAISE**: você escolhe **CALL** (×6 no acerto, e o ataque dele fica mais forte por alguns segundos) ou **FOLD** (cancela sem penalidade). Decisão rápida, grito na tela, ótimo para vídeo curto.
2. **Fala-blefe do chefe (*tell*):** o chefe provoca com um número ("My number is 9. Trust me."). Às vezes é verdade, às vezes é mentira, e a taxa muda por chefe (o Valete nunca mente, a Dama mente 70% das vezes). O jogador aprende a "ler" cada um. Também pode virar uma relíquia: "Poker Face / Detector de Mentira".
3. **Frases:** "the house always wins / a casa sempre ganha", "cards on the table / cartas na mesa", "show your hand / mostre a mão", "all-in".

(A ideia da vira/manilha foi descartada para não deixar o jogo difícil demais.)

**Mural de Boatos (os "furos do reinado"):**
- No menu tem uma taverna com um mural, e cada chefe tem 3 boatos para destravar ("o Rei nunca soube jogar truco", "a Dama já foi aldeã"...).
- Você destrava cumprindo desafios: crítico na primeira carta, vencer sem levar dano, usar TRUCO e ganhar...
- Isso junta conquistas (que já estão no roadmap, Etapa 5) com a lore, de graça, e dá um motivo para jogar de novo.

**Como contar (barato e que dá para pular):**
- Uma intro de **3 a 4 painéis** em pixel art (≤15 s, botão PULAR), no estilo da cortina de cartas (`CardCurtain`).
- **2 ou 3 falas** antes de cada chefe, numa caixa em forma de carta, e uma fala na morte dele. Tudo passa pelo `Loc` (PT/EN).
- Nada de texto no meio da luta, além dos gritos curtos (TRUCO!, BLEFE!).

---

## 2. Melhorias para ficar "completo"

### A. Primeiro minuto (o que os portais medem)
- O jogador precisa estar jogando em **menos de 10 s**: intro pulável, tutorial do Bobo encurtado (ou opcional depois da primeira run).
- Uma **primeira vitória rápida**: o Valete do Normal tem que cair em 1 a 2 minutos. Ajuste usando os dados da GamePix/CrazyGames (Etapa 1 do roadmap).

### B. Progressão entre runs (o maior ganho de retenção)
- Relíquias/coringas da Etapa 2 do roadmap, com nomes da história em EN/PT ("Marked Card / Carta Marcada", "Loaded Die / Dado Viciado", "Poker Face / Detector de Mentira").
- **Fichas** como moeda que fica entre as runs, gastas na taverna em: relíquias novas no sorteio, **versos de carta** e skins do aldeão. Você ganha mesmo perdendo, para a derrota não parecer tempo perdido.
- Um mapa curto do caminho até o trono (Vila → Quartel → Salão → Trono), com uma escolha de rota de vez em quando (evento, loja ou luta). Só se sobrar tempo; as relíquias sozinhas já seguram bem.

### C. Identidade (obrigatório antes de Play Store/Steam)
- **Trocar o player "Adventurer" (rvros)** por um aldeão próprio feito no Aseprite. A história pede isso, e é um asset muito usado (tem cara de "jogo genérico" na loja).
- **Conferir as licenças** de músicas, ToyZone e fichas para uso comercial com anúncios e IAP (é uma pendência antiga do tracking). Se alguma não liberar, trocar antes de qualquer loja.
- Uma **trilha própria** ou com licença clara, pelo menos para o menu e para o Rei.
- Um subtítulo para a loja: *Draw the Odds: Royal Bluff* / *O Blefe Real*.

### D. Por plataforma
**CrazyGames** (já integrado): usar o `DataModule` do SDK para salvar progresso (fichas, relíquias, boatos), que eles pedem para jogos com progressão. Mirar no Full Launch: mobile funcionando bem + tempo médio de sessão alto.

**Poki:** é a mais seletiva (você manda pelo formulário e eles testam antes de aceitar). Exige o build mais leve possível (o carregamento inicial pesa muito) e tudo funcionando no celular. O ranking externo fica desligado (`Portal.AllowExternalRequests`, já previsto) e o progresso vai no save local/do SDK. Mandar só depois das relíquias e da história, para causar uma boa primeira impressão.

**Play Store (Etapa 4 do roadmap) + extras:**
- Google Play Games: conquistas (= boatos), ranking e save na nuvem.
- Vibração no crítico/TRUCO, `Screen.safeArea`, "modo leve" para celular fraco.
- Monetização: anúncio recompensado (repetir chefe, dobrar as fichas da run) + compra única "Sem anúncios" com um verso de carta exclusivo. Interstitial só nas pausas que já existem.
- Teste fechado (12 testadores por 14 dias em conta pessoal nova): começar cedo, dá para rodar junto com o desenvolvimento.
- Política de privacidade (Supabase + anúncios) e classificação etária. **Atenção ao tema "aposta":** é só carta e dado, sem dinheiro real nem loot box. Declarar no questionário IARC que não há "simulação de jogo de azar" para não cair em 18+. Por isso evitar fichas compráveis com dinheiro de verdade.

**Steam (depois):** precisa de mais conteúdo (5 a 8 chefes, os 2 finais, ~3 a 5 h de conteúdo com relíquias), suporte a controle/Steam Deck, conquistas Steam e uma demo para o Next Fest. Abrir a página da loja cedo para juntar wishlists.

### E. Acessibilidade e alcance
- Hoje a pista "quente/frio" é por cor. Adicionar a distância em texto/ícone (ex.: 🔥 perto, ❄ longe) e um modo para daltônicos.
- Opção de mira automática/assistida no toque.
- **Espanhol** completo (a CrazyGames já detecta `es`, e a LatAm é forte em portais e na Play Store), e o truco ressoa por lá. Depois francês/alemão se os dados pedirem.

### F. Retenção (Etapa 5 do roadmap)
- Desafio diário com semente do dia (a "Mão do Dia"), ranking do dia, e uma fala do Bobo nova por dia (um boato aleatório).
- Estatísticas na vitória: precisão, blefes pegos, TRUCOs ganhos. É bom para print/compartilhar.

---

## 3. Ordem recomendada (ajuste no roadmap atual)
1. **Portais + dados** (como já está no roadmap). Em paralelo: **conferir as licenças**.
2. **Camada de história leve:** intro, falas dos chefes, Bobo narrador, novo aldeão. Barato e muda a percepção do jogo.
3. **Relíquias + fichas + taverna** (Etapa 2), já com nomes da história.
4. **RAISE/TRUCO e fala-blefe do chefe:** as mecânicas temáticas que diferenciam o jogo.
5. **O Ás (chefe secreto) + os finais** (Etapa 3).
6. **Mural de Boatos/conquistas + diário** → **Android**.
7. Poki (com a versão mais completa) → Steam (com mais chefes).

## 4. Onde cada coisa entra no código (para quando for implementar)
- Falas e intro: um novo `StoryDirector` (no estilo de `TutorialDirector.cs`), com os textos em `Loc.cs` e a transição usando `CardCurtain.cs`.
- Fala-blefe: `BossData.cs` (taxa de mentira por chefe) + `BossController.cs` (quando provoca) + `BossHUD.cs` (balão de fala).
- RAISE/TRUCO: `LuckyMath.cs` (multiplicador), `PlayerCards.cs`/`GameUI.cs` (botão), `TouchControls.cs` (botão de toque), gritos por idioma em `Loc.cs`.
- Ás secreto: condição de liberação em `GameManager.cs` + um novo `BossData` com dois números.
- Fichas, boatos e save: um novo `ProgressSave` (PlayerPrefs; `Portal.cs` escolhe o save do SDK na CrazyGames/Poki).
- Balanceamento continua em `Data/GameConfig.asset`.

## Verificação (quando for implementado)
- Jogar no Editor: a intro pula, as falas aparecem em PT e EN, o RAISE aplica ×3/×6/dano certo e o grito aparece no idioma certo (vale logar no `LuckyMath`).
- Build da CrazyGames em `localhost:8126` (`Tools/serve_webgl.py`): o save sobrevive a recarregar a página, os anúncios só aparecem nas pausas.
- Build Android num celular fraco: FPS, safe area, vibração.
