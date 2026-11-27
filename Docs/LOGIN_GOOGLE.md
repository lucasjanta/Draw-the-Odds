# Login com Google: plano (27/11/2026)

Status: **só planejamento**, nada implementado. O ranking continua funcionando sem login (nome digitado).

## Para que serve no Draw the Odds
- O nome no ranking passa a ser **da conta**: ninguém usa o seu nome, e "SEU RECORDE" funciona em qualquer aparelho.
- **Progresso na nuvem**: fichas, coringas liberados, guarda-roupa, conquistas e boatos (hoje ficam no `PlayerPrefs` de cada navegador ou celular).
- Base para conquistas/ranking do Google Play Games no futuro.

## Recomendação: Supabase Auth (o mesmo projeto do ranking)
O Supabase já é o backend do jogo e tem login com Google pronto. Em todas as plataformas o fluxo é o mesmo:

1. Pegar um **ID token do Google** (o jeito muda por plataforma, ver abaixo).
2. Trocar esse token por uma sessão do Supabase: `POST /auth/v1/token?grant_type=id_token` com `{ "provider": "google", "id_token": "..." }`. O cliente C# da comunidade tem o mesmo método: `SignInWithIdToken`. Dá para fazer só com `UnityWebRequest`, como o `LeaderboardClient` já faz.
3. Com a sessão (`access_token`), as funções do ranking passam a usar `auth.uid()` em vez do nome digitado. Também entra uma tabela `dto_profiles (user_id, nick, save jsonb)` para o save na nuvem, com RLS "cada um só lê e grava a própria linha".

### Android (o mais fácil)
- O Google lançou um plugin oficial e open source, o **Sign in with Google for Unity**. No Android ele usa o Credential Manager e devolve o ID token para o C#. Supabase, Firebase e Auth0 aparecem como backends suportados.
- **Atenção:** o anúncio fala em Unity 6.5, e o projeto está no 6000.4. É preciso testar se o plugin roda no 6.4 ou atualizar o Editor antes.
- O plugin antigo (`googlesamples/google-signin-unity`) foi descontinuado pelo Google para jogos novos desde fev/2025. Não usar.
- Configuração no Google Cloud Console: um OAuth Client do tipo **Android** (package `com.jantagames.drawtheodds` + SHA-1 da keystore de release) e outro do tipo **Web**, cujo client ID vai no Supabase.
- No painel do Supabase: Authentication → Providers → Google → colar os client IDs (separados por vírgula) e ligar "Skip nonce check" se o plugin não mandar nonce.

### Web (itch.io, portais)
É o caso mais chato: o jogo roda num **iframe** (`html-classic.itch.zone`), e o Google não deixa a tela de login abrir dentro de iframe.
- Caminho: um `.jslib` abre um **popup** com `https://ujkwiczikyolhlhachlk.supabase.co/auth/v1/authorize?provider=google&redirect_to=<callback.html>`. O `callback.html` fica hospedado ao lado do `index.html` do build, lê o `#access_token` e manda de volta com `window.opener.postMessage`. O jslib passa o token para a Unity com `SendMessage`.
- O popup só abre se for chamado direto no clique do jogador (bloqueador de popup).
- O app OAuth do Google precisa estar **publicado/verificado**. Em modo "testing", só os testadores aprovados conseguem logar (já aconteceu com outros jogos no itch).
- Portais: na **CrazyGames** usar a conta do próprio portal (o SDK tem `User`), não o Google. A **Poki** não permite login externo.

### Windows (.exe)
Abrir o navegador do sistema com o mesmo link de autorização e receber o retorno num `HttpListener` em `http://localhost:<porta>/callback` (loopback). Funciona, mas é o que menos vale a pena: dá para deixar por último.

## Alternativas avaliadas
| Opção | Prós | Contras |
|---|---|---|
| **Supabase Auth + Google (recomendada)** | Mesmo backend do ranking, grátis, um fluxo para tudo | Web exige popup + callback; app OAuth precisa de verificação |
| Unity Authentication (UGS) | SDK pronto na Unity, Google Play Games integrado | Outro backend: o ranking teria que migrar ou sincronizar |
| Firebase Auth | Muito documentado | Terceiro backend; o SDK Unity do Firebase não roda em WebGL |
| Contas dos portais (CrazyGames) | Zero configuração lá | Só vale dentro daquele portal |

## Ordem sugerida (quando for implementar)
1. Android primeiro (plugin oficial): login opcional no menu ("ENTRAR COM GOOGLE"), apelido salvo no perfil.
2. Funções do ranking v3 que aceitam o token (`auth.uid()`): sem token, segue o fluxo atual por nome.
3. Save na nuvem (fichas/guarda-roupa/conquistas): ao logar, juntar o save local com o da nuvem, ficando com o maior de cada coisa.
4. Web (popup + callback) e, por último, Windows (loopback).

## Fontes
- [Sign in with Google for Unity (Android Developers Blog)](https://developer.android.com/blog/posts/open-source-google-sign-in-unity)
- [Supabase: Login with Google](https://supabase.com/docs/guides/auth/social-login/auth-google.md)
- [Unity Authentication: Google sign-in](https://docs.unity.com/en-us/authentication/platform-signin/google.md)
- [Supabase + Unity, SignInWithIdToken (gist)](https://gist.github.com/wiverson/86ec69ddf13b137306842348eaec37a2)
- [Okta: Unity WebGL + OAuth com popup via jslib](https://developer.okta.com/blog/2021/02/26/unity-webgl-playfab-authorization)
- [itch.io: problema do iframe no callback OAuth](https://itch.io/t/2610444/help-needed-implementing-oauth-flow-callback-with-unity-webgl-game)
