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
    private Image portrait;          // retrato (carta) de quem fala, em cima da caixa à esquerda
    private string shownPortrait = "";
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

        // Retrato: a carta de quem fala (Coringa, Valete, Dama, Rei, Ás), "pendurada" em cima da caixa
        portrait = UIFactory.Image("Portrait", root, Color.white);
        portrait.preserveAspect = true;
        portrait.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-BoxWidth / 2f + 40f, 108f), new Vector2(58f, 80f));
        portrait.gameObject.SetActive(false);

        // Etiqueta com o nome de quem fala, "pendurada" no canto de cima da caixa
        nameTag = UIFactory.Image("NameTag", box.transform, UIFactory.OutlineColor);
        nameTag.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(76f, -4f), new Vector2(90f, 16f)); // à direita do retrato
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
        var skip = UIFactory.Button("Skip", box.transform, "SKIP", new Vector2(64f, 18f), () => skipRequested = true, 9f);
        skip.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-8f, 2f), new Vector2(64f, 18f));
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
        shownPortrait = "";
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
        ShowPortrait(line.portrait);
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

    // Sprite do retrato pela chave da fala (o naipe dos nobres é o do chefe atual).
    private Sprite PortraitSprite(string key)
    {
        if (string.IsNullOrEmpty(key) || config == null) return null;
        var gm = GameManager.Instance;
        Suit suit = gm != null ? gm.CurrentBossSuit : Suit.Spades;
        // "jack_2" = Valete de um naipe fixo (gêmeos, Valete de Espadas)
        if (key.StartsWith("jack_") && int.TryParse(key.Substring(5), out int s))
            return config.FindBoss("boss0")?.SpriteFor((Suit)s);
        switch (key)
        {
            case "joker": return config.TutorialBoss.Sprite;
            case "jack": return config.FindBoss("boss0")?.SpriteFor(suit);
            case "queen": return config.FindBoss("boss1")?.SpriteFor(suit);
            case "king": return config.FindBoss("boss2")?.SpriteFor(suit);
            case "ace": return config.SecretBoss.Sprite;
        }
        return null;
    }

    private void ShowPortrait(string key)
    {
        var sprite = PortraitSprite(key);
        portrait.gameObject.SetActive(sprite != null);
        if (sprite == null) { shownPortrait = ""; return; }
        portrait.sprite = sprite;
        if (key != shownPortrait) StartCoroutine(PortraitPop()); // quem fala mudou: a carta "pula"
        shownPortrait = key;
    }

    private IEnumerator PortraitPop()
    {
        var rt = portrait.rectTransform;
        for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.22f;
            float s = 1f + 0.25f * Mathf.Sin(k * Mathf.PI);
            rt.localScale = new Vector3(s, s, 1f);
            rt.localRotation = Quaternion.Euler(0f, 0f, 8f * (1f - k) * Mathf.Sin(k * 20f));
            yield return null;
        }
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
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
