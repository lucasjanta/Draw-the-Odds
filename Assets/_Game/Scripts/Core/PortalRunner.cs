using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Objeto invisível que vive o jogo inteiro (não some ao recarregar a cena) e cuida do SOM quando o
// portal pausa o jogo: anúncio na tela ou aba escondida (celular recebeu ligação, trocou de aba...).
// O portal já pausa o AudioListener, mas a nossa música ignora essa pausa (ignoreListenerPause,
// para continuar tocando no menu de pausa). Por isso aqui zeramos o volume geral enquanto durar.
public class PortalRunner : MonoBehaviour
{
    private static PortalRunner instance;
    private bool muted;

    // Cria sozinho quando o jogo abre. Vale para todas as builds: o ajuste da câmera (FitCamera)
    // também melhora o itch (embed 960×600) e o som mudo só age quando um portal pausa.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate() => Ensure();

    public static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("PortalRunner");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PortalRunner>();
        SceneManager.sceneLoaded += (_, _) => FitCamera();
        FitCamera();
    }

    // Nos portais o jogo roda num iframe de tamanho qualquer (ex.: 800×450, 960×540). A Pixel Perfect
    // Camera só amplia em números inteiros: em 960×540 ela fica em 1× e o mundo aparece pequeno, com
    // bordas vazias em volta. "StretchFill" desenha sempre os 640×360 do jogo e estica para preencher
    // a tela (mantendo a proporção). Pixels podem ficar levemente desiguais, mas o jogo ocupa a tela toda.
    private static void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var ppc = cam.GetComponent<PixelPerfectCamera>();
        if (ppc != null) ppc.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;
    }

    private void Update()
    {
        // Avisa o portal se o jogador está jogando de verdade (luta rolando, sem pausa, sem anúncio).
        var gm = GameManager.Instance;
        Portal.SetGameplay(gm != null && gm.State == GameManager.GameState.Playing && !GameTime.Paused && !Portal.AdPlaying);

        bool shouldMute = Portal.ShouldMute;
        if (shouldMute == muted) return;
        muted = shouldMute;
        AudioListener.volume = muted ? 0f : 1f;
    }
}
