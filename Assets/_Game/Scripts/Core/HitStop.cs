using System.Collections;
using UnityEngine;

// "Hit-stop": congela o jogo por uma fração de segundo num golpe forte (crítico).
// Só age com o tempo normal (não mexe na fusão, na pausa nem na câmera lenta do tutorial).
public class HitStop : MonoBehaviour
{
    private bool running;

    public void Play(float seconds)
    {
        if (running || GameTime.Paused || Time.timeScale < 0.99f) return;
        StartCoroutine(Run(seconds));
    }

    private IEnumerator Run(float seconds)
    {
        running = true;
        GameTime.SetScale(0.02f);
        yield return new WaitForSecondsRealtime(seconds);
        // Se outra coisa mudou o tempo nesse meio-tempo (fusão abriu, pausa), não desfaz.
        if (Mathf.Approximately(Time.timeScale, 0.02f) || GameTime.Paused) GameTime.SetScale(1f);
        running = false;
    }
}
