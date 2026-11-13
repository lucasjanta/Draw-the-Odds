using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Transição entre chefes: várias cartas caem do alto e cobrem a tela inteira (Cover);
// a fase troca por baixo; depois as cartas caem embora girando (Reveal).
// Tudo em tempo real (não depende do Time.timeScale).
public class CardCurtain : MonoBehaviour
{
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

        // Grade com as cartas BEM sobrepostas (cada uma anda só ~65% do próprio tamanho), cobrindo a tela
        // inteira com sobra nas bordas. Antes o passo vertical (90) era maior que a carta (80) e
        // sobravam frestas mostrando o fundo. Usa o tamanho real do Canvas (tela larga mostra mais que 640).
        Vector2 screen = ((RectTransform)transform).rect.size;
        if (screen.x < 1f || screen.y < 1f) screen = new Vector2(640f, 360f);
        float stepX = CardW * 0.65f, stepY = CardH * 0.65f;
        int cols = Mathf.CeilToInt((screen.x + CardW) / stepX) + 1;
        int rows = Mathf.CeilToInt((screen.y + CardH) / stepY) + 1;
        Vector2 origin = new Vector2(-(cols - 1) * stepX / 2f, -(rows - 1) * stepY / 2f); // grade centralizada
        // Os atrasos foram espalhados para a animação durar o mesmo que antes (~0,8 s), com mais cartas.
        float rowDelay = 0.28f / (rows - 1), colDelay = 0.22f / (cols - 1);

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var img = UIFactory.Image("Card", transform, Color.white, config.CardBack);
                var outline = img.gameObject.AddComponent<Outline>(); // contorno preto
                outline.effectColor = new Color(0.04f, 0.02f, 0.07f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
                var rt = img.rectTransform;
                rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW, CardH));
                // Inclinação menor (±6°): com ±10° as quinas abriam buracos entre as cartas.
                rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));
                Vector2 target = origin + new Vector2(c * stepX + Random.Range(-4f, 4f), r * stepY + Random.Range(-4f, 4f));
                float drop = screen.y + CardH + 20f; // começa acima da tela
                rt.anchoredPosition = target + Vector2.up * drop;
                cards.Add(rt);
                targets.Add(target);
            }

        const float fall = 0.32f;
        float total = fall + (cols - 1) * colDelay + (rows - 1) * rowDelay;
        float fallHeight = screen.y + CardH + 20f;
        var landed = new bool[cards.Count];
        for (float t = 0f; t <= total + Time.unscaledDeltaTime; t += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                int r = i / cols, c = i % cols;
                float delay = r * rowDelay + (c % 2 == 0 ? c : cols - c) * colDelay;
                float k = Mathf.Clamp01((t - delay) / fall);
                // Cai acelerando e dá um quique pequeno no fim
                float y = k < 0.8f ? 1f - (k / 0.8f) * (k / 0.8f) : -0.06f * Mathf.Sin((k - 0.8f) / 0.2f * Mathf.PI);
                cards[i].anchoredPosition = targets[i] + Vector2.up * fallHeight * y;

                // Som da carta pousando. Com ~170 cartas, o SfxThrottled deixa tocar no máximo
                // 1 a cada 0,045 s: vira um "rrrrá" de baralho sendo espalhado na mesa.
                if (!landed[i] && k >= 0.8f)
                {
                    landed[i] = true;
                    AudioManager.SfxThrottled(config.SfxCurtainCard, 0.045f, 0.55f, 0.12f);
                }
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
        AudioManager.Sfx(config.SfxDash, 0.5f, 0.05f, 0.85f); // "fuuu" das cartas indo embora
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
