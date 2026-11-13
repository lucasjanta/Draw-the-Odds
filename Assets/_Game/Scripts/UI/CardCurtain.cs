using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Transição entre chefes: várias cartas caem do alto e cobrem a tela inteira (Cover);
// a fase troca por baixo; depois as cartas caem embora girando (Reveal).
// Tudo em tempo real (não depende do Time.timeScale).
public class CardCurtain : MonoBehaviour
{
    private const int Cols = 13, Rows = 5;
    private const float CardW = 58f, CardH = 80f;

    private GameConfig config;
    private readonly List<RectTransform> cards = new List<RectTransform>();
    private readonly List<Vector2> targets = new List<Vector2>();

    public bool Covered { get; private set; }

    public static CardCurtain Create(Transform parent, GameConfig config)
    {
        var rt = UIFactory.Rect("CardCurtain", parent).Stretch();
        var c = rt.gameObject.AddComponent<CardCurtain>();
        c.config = config;
        return c;
    }

    // Cartas caem fileira por fileira (de baixo para cima), cada uma com um quique ao "pousar".
    public IEnumerator Cover()
    {
        Clear();
        transform.SetAsLastSibling();
        // Grade um pouco maior que a tela (640 × 360), cartas sobrepostas e levemente tortas
        float stepX = 640f / (Cols - 1), stepY = 360f / (Rows - 1);
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                var img = UIFactory.Image("Card", transform, Color.white, config.CardBack);
                var outline = img.gameObject.AddComponent<Outline>(); // contorno preto
                outline.effectColor = new Color(0.04f, 0.02f, 0.07f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
                var rt = img.rectTransform;
                rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW, CardH));
                rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-10f, 10f));
                Vector2 target = new Vector2(-320f + c * stepX + Random.Range(-6f, 6f), -180f + r * stepY + Random.Range(-6f, 6f));
                rt.anchoredPosition = target + Vector2.up * 460f;
                cards.Add(rt);
                targets.Add(target);
            }

        const float fall = 0.32f;
        float total = fall + (Cols - 1) * 0.018f + (Rows - 1) * 0.07f;
        for (float t = 0f; t <= total + Time.unscaledDeltaTime; t += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                int r = i / Cols, c = i % Cols;
                float delay = r * 0.07f + (c % 2 == 0 ? c : Cols - c) * 0.018f;
                float k = Mathf.Clamp01((t - delay) / fall);
                // Cai acelerando e dá um quique pequeno no fim
                float y = k < 0.8f ? 1f - (k / 0.8f) * (k / 0.8f) : -0.06f * Mathf.Sin((k - 0.8f) / 0.2f * Mathf.PI);
                cards[i].anchoredPosition = targets[i] + Vector2.up * 460f * y;
            }
            yield return null;
        }
        Covered = true;
        Fx.Shake(0.15f, 0.12f);
        yield return new WaitForSecondsRealtime(0.25f);
    }

    // As cartas caem para fora da tela girando, cada uma com um empurrão diferente.
    public IEnumerator Reveal()
    {
        var velocity = new Vector2[cards.Count];
        var spin = new float[cards.Count];
        var delay = new float[cards.Count];
        for (int i = 0; i < cards.Count; i++)
        {
            velocity[i] = new Vector2(Random.Range(-160f, 160f), Random.Range(80f, 260f));
            spin[i] = Random.Range(-420f, 420f);
            delay[i] = Random.Range(0f, 0.25f);
        }
        for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = 0; i < cards.Count; i++)
            {
                if (t < delay[i]) continue;
                velocity[i] += Vector2.down * 1600f * dt;
                cards[i].anchoredPosition += velocity[i] * dt;
                cards[i].localRotation *= Quaternion.Euler(0f, 0f, spin[i] * dt);
            }
            yield return null;
        }
        Clear();
    }

    private void Clear()
    {
        foreach (var c in cards) if (c != null) Destroy(c.gameObject);
        cards.Clear();
        targets.Clear();
        Covered = false;
    }
}
