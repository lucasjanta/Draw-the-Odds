using System;
using System.Collections;
using UnityEngine;
#if DTO_GAMEPIX
using GamePix;
#endif
#if DTO_CRAZY
using CrazyGames;
#endif

// Ponte entre o jogo e o portal onde ele está publicado. O jogo SÓ conversa com esta classe;
// cada portal entra com um "#if" (símbolo definido na build, menus "Draw the Odds/Portal/..."):
//   DTO_GAMEPIX  → GamePix
//   DTO_CRAZY    → CrazyGames
//   (nenhum)     → itch.io / Windows: tudo aqui vira "não faz nada".
// Assim o resto do código nunca precisa saber em qual site está rodando.
public static class Portal
{
    public enum Kind { None, GamePix, CrazyGames }

#if DTO_GAMEPIX
    public static readonly Kind Current = Kind.GamePix;
#elif DTO_CRAZY
    public static readonly Kind Current = Kind.CrazyGames;
#else
    public static readonly Kind Current = Kind.None;
#endif

    // Nome que vai para o ranking (coluna "platform" no Supabase).
    public static string PlatformId => Current == Kind.GamePix ? "gamepix" : Current == Kind.CrazyGames ? "crazygames"
        : Application.platform == RuntimePlatform.Android ? "android" : "itch";

    // Intervalo mínimo entre dois anúncios (segundos reais). Não é um anúncio "por tempo":
    // ele só aparece nas pausas naturais (morreu → tentar de novo, troca de chefe), e esta trava
    // impede que mortes rápidas seguidas mostrem um anúncio atrás do outro.
    // (A CrazyGames ainda limita sozinha: no máximo 1 a cada 3 minutos.)
    private const float MinSecondsBetweenAds = 120f;
    // Primeiro anúncio só depois de um tempo de jogo (o jogador ainda está conhecendo o jogo).
    private const float FirstAdDelay = 90f;
    // Segurança: se o portal nunca responder, o jogo continua depois disso.
    private const float AdTimeout = 75f;

    private static float lastAdTime = float.NegativeInfinity;

    // Pedido de anúncio em andamento (o jogo fica parado e sem input).
    public static bool AdPlaying { get; private set; }
    // O anúncio já APARECEU na tela (a CrazyGames pede para tirar o som só neste momento).
    public static bool AdShowing { get; private set; }

    // O portal pausou o jogo (anúncio OU a aba ficou escondida).
    public static bool PausedByPortal
    {
        get
        {
#if DTO_GAMEPIX
            return AdPlaying || Gpx.IsGamePaused;
#else
            return AdPlaying;
#endif
        }
    }

    // O jogador tirou o som pelo botão do PRÓPRIO site (CrazyGames: Settings.muteAudio).
    // O SDK só avisa; quem obedece é o jogo (PortalRunner zera o AudioListener).
    public static bool PlatformMuted { get; private set; }

    // Som deve ficar zerado agora?
    public static bool ShouldMute => PlatformMuted || (Current == Kind.CrazyGames ? AdShowing : PausedByPortal);

    // ===================== Inicialização =====================
    public static bool Ready { get; private set; }
    public static event Action OnReady;

    // Chamado uma vez no começo (GameManager.Awake). A CrazyGames pede para não iniciar o SDK
    // dentro de um [RuntimeInitializeOnLoadMethod]; o Awake da cena serve.
    public static void Init()
    {
#if DTO_CRAZY
        if (Ready || !CrazySDK.IsAvailable) return;
        CrazySDK.Init(() =>
        {
            Ready = true;
            // Mute da plataforma: estado inicial + avisos quando o jogador muda no site
            PlatformMuted = CrazySDK.Game.Settings.muteAudio;
            CrazySDK.Game.AddSettingsChangeListener(settings => PlatformMuted = settings.muteAudio);
            OnReady?.Invoke();
        });
#else
        Ready = Current != Kind.None;
#endif
    }

#if DTO_CRAZY
    private static bool CrazyOk => Ready && CrazySDK.IsAvailable;
#endif

    // Pedidos para servidores de fora (o ranking global no Supabase). GamePix e CrazyGames permitem
    // (as regras deles proíbem SDKs, anúncios e analytics de terceiros, não um servidor próprio do jogo).
    // A Poki NÃO permite: quando ela entrar, devolver false para Kind.Poki.
    public static bool AllowExternalRequests => true;

    // Anúncio entre fases também ao voltar para o menu? A CrazyGames proíbe anúncio em botões de
    // navegação (menu, opções...); lá só há anúncio entre um chefe e outro e no "recomeçar".
    public static bool AdsOnNavigation => Current != Kind.CrazyGames;

    // ===================== Anúncio recompensado disponível? =====================
    // No itch não há anúncios: o botão de "continuar" nem aparece. Na CrazyGames o botão some se os
    // anúncios estiverem desligados (Basic Launch) ou houver adblock: nada de botão "morto".
    private static bool adsDisabled;
    public static bool RewardedAvailable
    {
        get
        {
            if (fakeAds) return true;
            if (Current == Kind.None || adsDisabled) return false;
#if DTO_CRAZY
            return CrazyOk && CrazySDK.Ad.AdblockStatus != AdblockStatus.Present;
#else
            return true;
#endif
        }
    }

    // Debug (ponte do Editor): finge que existe anúncio recompensado (sempre "assistido").
    // Ex.: call Portal.DebugFakeAds on
    // Na build web dá para testar o fluxo abrindo a página com "?fakeads=1" no endereço.
    // (Lido na primeira vez que é usado: o Unity não deixa ler a URL num inicializador estático.)
    private static bool? fakeAdsFlag;
    private static bool fakeAds => fakeAdsFlag ??= Application.absoluteURL != null && Application.absoluteURL.Contains("fakeads=1");
    public static string DebugFakeAds(string arg) { fakeAdsFlag = arg != "off"; return "fake ads " + fakeAds; }

    // ===================== Informações do jogador =====================
    // Idioma que o portal informa (null = o portal não diz nada → o jogo usa o padrão, inglês).
    public static Language? PreferredLanguage
    {
        get
        {
#if DTO_GAMEPIX
            return Loc.FromSystem(Gpx.CurrentLanguage) ?? Language.English;
#elif DTO_CRAZY
            if (!CrazyOk) return null;
            ReadSystemInfo();
            return Loc.FromCode(locale) ?? Language.English;
#else
            return null;
#endif
        }
    }

    // Celular/tablet segundo o portal (a CrazyGames pede para decidir os controles de toque assim,
    // e não só detectando toque).
    public static bool IsMobileDevice
    {
        get
        {
#if DTO_CRAZY
            if (!CrazyOk) return false;
            ReadSystemInfo();
            return deviceType == "mobile" || deviceType == "tablet";
#else
            return false;
#endif
        }
    }

#if DTO_CRAZY
    // Lido uma vez só: cada leitura do SystemInfo chama o JavaScript do SDK.
    private static string locale, deviceType;
    private static void ReadSystemInfo()
    {
        if (locale != null) return;
        var info = CrazySDK.User.SystemInfo;
        locale = info?.locale ?? "";
        deviceType = info?.device?.type ?? "";
    }
#endif

    // ===================== Eventos do jogo =====================
    // Jogando de verdade (luta rolando, sem pausa)? A CrazyGames quer saber quando começa e para.
    private static bool gameplayOn;

    public static void SetGameplay(bool playing)
    {
        if (playing == gameplayOn) return;
        gameplayOn = playing;
#if DTO_CRAZY
        if (!CrazyOk) { gameplayOn = false; return; }
        if (playing) CrazySDK.Game.GameplayStart();
        else CrazySDK.Game.GameplayStop();
#endif
    }

    // Momentos bons e ruins. big = momento raro (vitória final): a CrazyGames só quer esses.
    public static void HappyMoment(bool big = false)
    {
#if DTO_GAMEPIX
        Gpx.Events.HappyMoment();
#elif DTO_CRAZY
        if (big && CrazyOk) CrazySDK.Game.HappyTime();
#endif
    }

    public static void SadMoment()
    {
#if DTO_GAMEPIX
        Gpx.Events.SadMoment();
#endif
    }

    // ===================== Anúncios =====================
    // Anúncio entre fases. Use com "yield return Portal.Interstitial()" numa coroutine:
    // ela só termina quando o anúncio acaba (ou falha / foi pulado pela trava de tempo).
    public static IEnumerator Interstitial()
    {
        if (!CanShowAd()) yield break;
        lastAdTime = Time.realtimeSinceStartup;

        bool done = false;
        AdPlaying = true;
        PortalRunner.Ensure();
#if DTO_GAMEPIX
        AdShowing = true;
        Gpx.Ads.InterstitialAd(() => done = true, () => done = true);
#elif DTO_CRAZY
        CrazySDK.Ad.RequestAd(CrazyAdType.Midgame, () => AdShowing = true,
            error =>
            {
                if (error != null && (error.code == "adsDisabled" || error.code == "adblock")) adsDisabled = true;
                done = true;
            },
            () => done = true);
#else
        done = true;
#endif
        float giveUpAt = Time.realtimeSinceStartup + AdTimeout;
        while (!done && Time.realtimeSinceStartup < giveUpAt) yield return null;
        AdPlaying = AdShowing = false;
    }

    // Anúncio recompensado ("assista e continue"). onDone(true) SÓ se o anúncio foi visto até o fim;
    // falha, adblock ou desistência = false (e o jogador não ganha nada).
    public static IEnumerator Rewarded(Action<bool> onDone)
    {
        if (!RewardedAvailable || AdPlaying) { onDone?.Invoke(false); yield break; }
        bool done = false, success = false;
        AdPlaying = true;
        PortalRunner.Ensure();
        if (fakeAds) { done = true; success = true; }
#if DTO_GAMEPIX
        else
        {
            AdShowing = true;
            Gpx.Ads.RewardAd(() => { success = true; done = true; }, () => done = true);
        }
#elif DTO_CRAZY
        else
        {
            CrazySDK.Ad.RequestAd(CrazyAdType.Rewarded,
                () => AdShowing = true,
                error =>
                {
                    // Anúncios desligados (Basic Launch) ou adblock: esconde o botão daqui em diante.
                    if (error != null && (error.code == "adsDisabled" || error.code == "adblock")) adsDisabled = true;
                    done = true;
                },
                () => { success = true; done = true; });
        }
#else
        else done = true;
#endif
        float giveUpAt = Time.realtimeSinceStartup + AdTimeout;
        while (!done && Time.realtimeSinceStartup < giveUpAt) yield return null;
        AdPlaying = AdShowing = false;
        // Acabou de ver um anúncio: o próximo intervalo espera o tempo mínimo de novo.
        lastAdTime = Time.realtimeSinceStartup;
        onDone?.Invoke(success);
    }

    private static bool CanShowAd()
    {
        if (Current == Kind.None || AdPlaying) return false;
#if DTO_CRAZY
        if (!CrazyOk || adsDisabled) return false;
#endif
        float now = Time.realtimeSinceStartup;
        return now >= FirstAdDelay && now - lastAdTime >= MinSecondsBetweenAds;
    }
}
