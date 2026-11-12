using UnityEngine;

// Controle central do tempo do jogo: câmera lenta (escala) + pausa do menu.
// Enquanto o jogo está pausado, pedidos de mudar a escala (fusão, tutorial) ficam guardados
// e só valem quando a pausa termina. Assim o menu de pausa nunca é "despausado" sem querer.
public static class GameTime
{
    private const float DefaultFixedDelta = 0.02f;

    public static bool Paused { get; private set; }
    private static float scaleBeforePause = 1f;

    public static void SetScale(float scale)
    {
        if (Paused)
        {
            scaleBeforePause = scale; // aplica quando sair da pausa
            return;
        }
        Apply(scale);
    }

    public static void SetPaused(bool paused)
    {
        if (paused == Paused) return;
        if (paused)
        {
            scaleBeforePause = Time.timeScale;
            Apply(0f);
            Paused = true;
        }
        else
        {
            Paused = false;
            Apply(scaleBeforePause);
        }
        AudioListener.pause = paused; // pausa os SFX (música e UI ignoram, ver AudioManager)
    }

    // Recarregar a cena / voltar ao menu: zera tudo.
    public static void Reset()
    {
        Paused = false;
        AudioListener.pause = false;
        Apply(1f);
    }

    private static void Apply(float scale)
    {
        Time.timeScale = scale;
        // fixedDeltaTime acompanha a câmera lenta (física suave); com o tempo parado não precisa mudar.
        if (scale > 0f) Time.fixedDeltaTime = DefaultFixedDelta * scale;
    }
}
