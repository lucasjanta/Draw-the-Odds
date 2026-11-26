# Draw the Odds — roadmap (depois da versão web)

Escrito em 16/11/2026. A ordem foi pensada para cada etapa já render algo publicável.

História, mecânicas temáticas (RAISE/TRUCO, fala-blefe) e o plano para portais e lojas: `Docs/HISTORIA.md` (24/11/2026).

## Onde o jogo está
- 3 chefes (Valete, Dama e Rei), um tutorial completo e dicas curtas para quem entra direto.
- Modo infinito (liberado ao vencer a Dama), 3 dificuldades e ranking global com 4 abas.
- Funciona no teclado + mouse e no toque (joystick, botões e cartas arrastáveis).
- Portais: GamePix pronto (`Docs/PORTAIS.md`); CrazyGames e Poki planejados.

---

## Etapa 1 — Publicar nos portais e ouvir os números (1–2 semanas)
**Objetivo:** descobrir com dados reais onde os jogadores desistem antes de investir em conteúdo.
- GamePix: enviar o `.gpx` e acompanhar o painel (sessões, tempo médio, % que passa do Valete).
- CrazyGames: integrar o SDK (`DTO_CRAZY` em `Portal.cs`) e enviar para o Basic Launch.
- Métrica que importa: **quanto tempo até a primeira vitória** e **quantos chegam à Dama**.
- Ajustar o balanceamento só com esses números (HP do Valete, frequência dos orbes, janela da fusão).

## Etapa 2 — Coringas do jogador ("relíquias")
O maior salto de profundidade. É o que transforma o jogo num roguelite, e é o que segura no celular.

**Como funciona:** depois de cada chefe (e a cada rodada do infinito), escolha 1 de 3 coringas. Eles duram a run inteira e ficam num cinto de até 5 no HUD.

**Ideias de coringas** (todos usam sistemas que já existem):
| Coringa | Efeito | Onde mexe |
|---|---|---|
| Carta Marcada (Marked Card) | 1x por chefe: a próxima carta vira o número exato | `CardProjectile` / `BossController.ReceiveCard` |
| Dado Viciado | A pista mostra também se o número é PAR ou ÍMPAR | `BossHUD` (histórico) |
| Mão Grande | Mão máxima +2 e mão inicial +1 | `Hand.MaxSize`, `GameConfig` |
| Coração de Ouro | ♥ cura +1 a mais; ♦ dá +1 carta a mais | `HitResult` |
| Espada Afiada | ♠ ×2 em vez de ×1,5 | `LuckyMath.SuitMultiplier` |
| Relógio Parado | Janela da fusão +5 s e mostra o resultado | `FusionController` |
| Ficha da Sorte | +1 redraw agora e +1 a cada chefe | `GameManager.RedrawsLeft` |
| Coringa Selvagem | Cartas de naipes diferentes nunca falham na fusão | `FusionRules` |
| Pé de Coelho | Dash recarrega 40% mais rápido | `PlayerController.dashCooldown` |
| Contador | Errar por 1 conta como crítico (sem trocar o número) | `LuckyMath` |

**Implementação sugerida:** `JokerData` (ScriptableObject: nome, ícone, descrição, raridade) + `JokerEffects` (um `switch` por tipo, chamado nos pontos acima) + `JokerPickPanel` (3 cartas grandes, toque/clique para escolher). Arte: 16 ícones 24×24 no Aseprite, no estilo dos orbes.

## Etapa 3 — Mais chefes (conteúdo)
O sistema de faces do dado (`BossData.dieFaces`) já permite criar chefes só com dados + sprite.
- **O Ás, "Ás na Manga" (Ace up the Sleeve)**: chefe final secreto depois do Rei (liberado por condição; ver `HISTORIA.md`), e também o "chefão" do infinito a cada 10 rodadas: número de 1 a 13, mas **dois números secretos**. O crítico só conta acertando os dois em sequência.
- **Valete Gêmeo**: dois Valetes pequenos, cada um com o seu número e metade da vida.
- **Dama de Copas**: se cura quando você erra por 5 ou mais (pune o chute).
- **Croupier**: embaralha a sua mão a cada 15 s (troca a carta selecionada).
- Ataques novos baratos: **roleta** (faixa giratória de dano), **cartas marcadas** (cartas no chão que explodem), **blackjack** (o chefe "compra" cartas e ataca com a soma).

Cada chefe novo precisa de: sprite por naipe (Aseprite), 6 faces de dado, uma regra especial curta e uma música (pode repetir).

## Etapa 4 — Android (Google Play)
O código de toque já está pronto, então a parte técnica é pequena. O trabalho maior é de loja e de monetização.

**Técnico**
- Build Android no Unity: IL2CPP, ARM64, API mínima 24, orientação **só paisagem**.
- `Portal.cs`: um `DTO_ANDROID` com o **AdMob** (ou o Unity LevelPlay) no lugar da GamePix: o interstitial e o recompensado já têm os pontos certos no jogo.
- Salvar: o `PlayerPrefs` já funciona. Para nuvem, o Google Play Games Saved Games (opcional).
- Ranking: o Supabase continua igual (o `platform` passa a ser `android`).
- Área segura (notch): ajustar o Canvas com `Screen.safeArea` (os botões de toque ficam na borda).
- Vibração curta no acerto crítico e no dano (`Handheld.Vibrate` ou um plugin com intensidade).
- Desempenho: testar num celular fraco. As Light2D e o rastro das cartas são os itens mais pesados; deixar um "modo leve" nas opções.

**Loja e dinheiro**
- Conta de desenvolvedor do Google Play (taxa única de US$ 25). Contas pessoais novas precisam de **teste fechado com 12 testadores por 14 dias** antes de publicar (regra vigente em 2025; conferir no Play Console antes de começar).
- Modelo recomendado: grátis com anúncios **+ uma compra única "Sem anúncios"** (US$ 2–3), que também libera um verso de carta exclusivo.
- Política de privacidade (obrigatória com anúncios) e formulário de classificação etária.
- Ícone, 4–8 capturas de tela em paisagem, vídeo curto e descrição em PT/EN.

**Diferencial para a versão mobile:** coringas (Etapa 2) + **desafio diário** (mesma semente para todos, ranking do dia). O diário é o que traz o jogador de volta todo dia.

## Etapa 5 — Retenção e polimento
- **Desafio diário** (semente do dia: mesmos chefes, regras e baralho para todos).
- **Conquistas** (crítico de primeira, vencer sem dano, sem redraw, rodada 10 no infinito) que liberam versos de carta e fundos.
- **Estatísticas** na tela de vitória (precisão, números adivinhados, melhor sequência).
- Música: mais uma trilha para o infinito; SFX dos coringas.

---

## Ordem recomendada
1. Portais (dados reais) → 2. Coringas → 3. 1–2 chefes novos → 4. Android → 5. Diário e conquistas.

Os coringas vêm antes do Android porque, sem eles, a versão de loja teria pouco a mais que a web gratuita.
