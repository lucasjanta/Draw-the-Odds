using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Caixa de diálogo da história: escurece a tela, mostra quem fala e o texto aparecendo letra por letra.
// Clique / toque / Espaço / Enter: completa a frase (se ainda estiver escrevendo) ou vai para a próxima.
// O botão PULAR encerra a cena inteira. Tudo em tempo real (o jogo fica parado por baixo).
public class StoryDialog : MonoBehaviour
{
    private const float CharsPerSecond = 55f;
    private const float BoxWidth = 470f;

    private GameConfig config;
    private CanvasGroup group;
    private TextMeshProUGUI nameText, bodyText, arrow;
    private Image nameTag;
    private bool skipRequested;

    public bool Showing { get; private set; }

    public static StoryDialog Create(Transform parent, GameConfig config)
    {
        var root = UIFactory.Rect("StoryDialog", parent).Stretch();
        var d = root.gameObject.AddComponent<StoryDialog>();
        d.config = config;
        d.Build(root);
        return d;
    }

    private void Build(RectTransform root)
    {
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // Fundo escuro: também segura os cliques (não viram arremesso de carta)
        var dim = UIFactory.Image("Dim", root, new Color(0.02f, 0.01f, 0.05f, 0.6f));
        dim.raycastTarget = true;
        dim.rectTransform.Stretch();

        // Caixa (mesmo estilo dos menus: contorno preto + faixa dourada)
        var box = UIFactory.MenuPanel("Box", root, new Vector2(BoxWidth, 72f));
        box.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(BoxWidth, 72f));

        // Etiqueta com o nome de quem fala, "pendurada" no canto de cima da caixa
        nameTag = UIFactory.Image("NameTag", box.transform, UIFactory.OutlineColor);
        nameTag.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(12f, -4f), new Vector2(90f, 16f));
        var tagFace = UIFactory.Image("Face", nameTag.transform, new Color(UIFactory.PanelColor.r, UIFactory.PanelColor.g, UIFactory.PanelColor.b, 1f));
        tagFace.rectTransform.Stretch(2f);
        nameText = UIFactory.Text("Name", nameTag.transform, "", 9f, Color.white, TextAlignmentOptions.Center).NoLoc();
        nameText.fontStyle = FontStyles.Bold;
        nameText.rectTransform.Stretch();

        bodyText = UIFactory.Text("Body", box.transform, "", 10f, Color.white, TextAlignmentOptions.MidlineLeft).NoLoc();
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.lineSpacing = 6f;
        var textFit = bodyText.GetComponent<TextFit>();
        if (textFit != null) Destroy(textFit); // aqui o texto quebra linha; não deve encolher
        bodyText.rectTransform.Stretch();
        bodyText.rectTransform.offsetMin = new Vector2(16f, 8f);
        bodyText.rectTransform.offsetMax = new Vector2(-22f, -8f);

        arrow = UIFactory.Text("Arrow", box.transform, "►", 9f, UIFactory.Gold, TextAlignmentOptions.BottomRight).NoLoc();
        arrow.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 6f), new Vector2(14f, 12f));
        arrow.gameObject.AddComponent<Blink>();

        // PULAR (canto superior direito): encerra a cena
        var skip = UIFactory.Button("Skip", root, "SKIP", new Vector2(64f, 20f), () => skipRequested = true, 9f);
        skip.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(64f, 20f));
    }

    // Mostra as falas em sequência e termina quando o jogador passa da última (ou pula).
    public IEnumerator Play(List<StoryLine> lines)
    {
        if (lines == null || lines.Count == 0) yield break;
        Showing = true;
        skipRequested = false;
        group.blocksRaycasts = true;
        yield return Fade(1f, 0.2f);

        foreach (var line in lines)
        {
            if (skipRequested) break;
            ShowLine(line);
            AudioManager.Ui(config.SfxCardDraw);

            // Letra por letra (o jogo pausado congela a escrita)
            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;
            float shown = 0f;
            arrow.enabled = false;
            yield return null; // o clique que abriu/avançou não completa a frase nova
            while (shown < total && !skipRequested)
            {
                if (!GameTime.Paused) shown += Time.unscaledDeltaTime * CharsPerSecond;
                bodyText.maxVisibleCharacters = Mathf.FloorToInt(shown);
                if (Pressed()) break;
                yield return null;
            }
            bodyText.maxVisibleCharacters = 99999;
            arrow.enabled = true;
            yield return null;

            while (!skipRequested && !Pressed()) yield return null;
        }

        AudioManager.Ui(config.SfxUiClick);
        yield return Fade(0f, 0.2f);
        group.blocksRaycasts = false;
        Showing = false;
    }

    // Fecha na hora (quem chamou o Play foi interrompido: tutorial pulado, passo voltado...).
    public void Hide()
    {
        StopAllCoroutines();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        Showing = false;
    }

    private void ShowLine(StoryLine line)
    {
        bool narrator = string.IsNullOrEmpty(line.speaker);
        nameTag.gameObject.SetActive(!narrator);
        if (!narrator)
        {
            nameText.text = line.speaker;
            nameText.color = line.color;
            float width = nameText.GetPreferredValues(line.speaker).x + 16f;
            nameTag.rectTransform.sizeDelta = new Vector2(Mathf.Max(60f, width), 16f);
        }
        // Narrador: texto centralizado e em itálico; personagens: alinhado à esquerda
        bodyText.alignment = narrator ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
        bodyText.fontStyle = narrator ? FontStyles.Italic : FontStyles.Normal;
        bodyText.text = line.text;
        bodyText.maxVisibleCharacters = 0;
    }

    // Debug (ponte do Editor): avança uma fala ou pula a cena sem precisar clicar no jogo.
    private bool nextRequested;
    public void RequestNext() => nextRequested = true;
    public void RequestSkip() => skipRequested = true;

    // Avançar: clique/toque fora dos botões, Espaço ou Enter (nunca com o menu de pausa aberto).
    private bool Pressed()
    {
        if (nextRequested) { nextRequested = false; return true; }
        if (GameTime.Paused) return false;
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            return true;
        return VirtualInput.AnyPressThisFrame(out Vector2 at) && !VirtualInput.OverInteractive(at);
    }

    private IEnumerator Fade(float target, float duration)
    {
        float start = group.alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        group.alpha = target;
    }
}
