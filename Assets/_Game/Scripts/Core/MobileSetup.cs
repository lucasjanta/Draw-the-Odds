using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Ajustes que só valem no celular (Android/iOS). Roda sozinho quando o jogo abre, antes da cena.
// - 60 FPS: no Android o padrão da Unity é 30, o que deixa o jogo "pesado" no toque.
// - Tela não apaga durante a partida (o jogador pode ficar parado pensando na fusão).
// - HDR desligado: o jogo não usa pós-processamento (bloom etc.), então o HDR só gastava memória
//   e banda de vídeo à toa. A imagem fica igual.
public static class MobileSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        if (!Application.isMobilePlatform) return;

        Screen.fullScreen = true; // esconde a barra de status/navegação (o jogo usa a tela toda)
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0; // no celular quem manda é o targetFrameRate
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            urp.supportsHDR = false;
    }
}
