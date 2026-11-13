using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Atalhos para montar UI em código (referência de tela: 640×360).
public static class UIFactory
{
    public static readonly Color PanelColor = new Color(0.07f, 0.05f, 0.14f, 0.95f);
    public static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.6f);

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Âncora + pivô no mesmo ponto (ex.: (0,1) = canto superior esquerdo).
    public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(this RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    public static Image Image(string name, Transform parent, Color color, Sprite sprite = null)
    {
        var img = Rect(name, parent).gameObject.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var tmp = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.richText = true;
        tmp.gameObject.AddComponent<TextFit>(); // nunca deixa a linha passar da largura da caixa
        tmp.gameObject.AddComponent<LocText>(); // tradução automática (inglês → português)
        return tmp;
    }

    // Texto que NÃO deve ser traduzido sozinho (nome digitado, nomes do ranking, textos que já
    // escolhem o idioma no código com Loc.Pick).
    public static TextMeshProUGUI NoLoc(this TextMeshProUGUI tmp)
    {
        var loc = tmp.GetComponent<LocText>();
        if (loc != null) Object.Destroy(loc);
        return tmp;
    }

    // Barra: fundo + "fantasma" (dano recente) + preenchimento. Retorna o preenchimento e o fantasma.
    public static void Bar(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color fillColor,
        out RectTransform fill, out RectTransform ghost)
    {
        var back = Image("BarBack", parent, BarBackColor);
        back.rectTransform.Place(anchor, pivot, pos, size);
        var g = Image("Ghost", back.transform, new Color(1f, 1f, 1f, 0.85f));
        ghost = g.rectTransform.Stretch(1f);
        var f = Image("Fill", back.transform, fillColor);
        fill = f.rectTransform.Stretch(1f);
    }

    // Contorno escuro no texto (pixels em volta, ver PixelOutline): separa as letras e destaca do fundo.
    public static TextMeshProUGUI Outline(this TextMeshProUGUI tmp, float pixels = 1f, Color32? color = null)
    {
        PixelOutline.Add(tmp, pixels, color ?? new Color32(0x00, 0x00, 0x00, 0xFF));
        return tmp;
    }

    // ===================== Menus =====================
    public static readonly Color Gold = new Color32(0xFF, 0xD4, 0x47, 0xFF);
    private static readonly Color ButtonNormal = new Color32(0x2A, 0x1F, 0x4D, 0xFF);
    private static readonly Color ButtonHover = new Color32(0x5A, 0x41, 0x9E, 0xFF);
    private static readonly Color ButtonPressed = new Color32(0x8A, 0x6A, 0x1E, 0xFF);

    public static readonly Color OutlineColor = new Color32(0x0A, 0x06, 0x12, 0xFF);

    // Botão pixel art: contorno preto de 2 px, face colorida com brilho em cima e sombra embaixo,
    // texto com contorno. Cresce no hover e "afunda" no clique (ButtonJuice). Toca o clique de UI.
    public static Button Button(string name, Transform parent, string label, Vector2 size, System.Action onClick, float fontSize = 14f)
    {
        var img = Image(name, parent, OutlineColor);   // a raiz é o contorno
        img.raycastTarget = true; // precisa receber o clique
        img.rectTransform.sizeDelta = size;

        var face = Image("Face", img.transform, Color.white); // a cor vem do Button (normal/hover/clique)
        face.rectTransform.Stretch(2f);
        var shine = Image("Shine", face.transform, new Color(1f, 1f, 1f, 0.16f));
        shine.rectTransform.anchorMin = new Vector2(0f, 1f);
        shine.rectTransform.anchorMax = new Vector2(1f, 1f);
        shine.rectTransform.pivot = new Vector2(0.5f, 1f);
        shine.rectTransform.sizeDelta = new Vector2(0f, 2f);
        var shade = Image("Shade", face.transform, new Color(0f, 0f, 0f, 0.3f));
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = new Vector2(1f, 0f);
        shade.rectTransform.pivot = new Vector2(0.5f, 0f);
        shade.rectTransform.sizeDelta = new Vector2(0f, 3f);

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = face;
        var colors = btn.colors;
        colors.normalColor = ButtonNormal;
        colors.highlightedColor = ButtonHover;
        colors.selectedColor = ButtonNormal; // não fica "aceso" depois do clique
        colors.pressedColor = ButtonPressed;
        colors.fadeDuration = 0.05f;
        btn.colors = colors;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(() =>
        {
            var gm = GameManager.Instance;
            if (gm != null) AudioManager.Ui(gm.Config.SfxUiClick);
            onClick?.Invoke();
        });

        img.gameObject.AddComponent<HoverSound>();
        img.gameObject.AddComponent<ButtonJuice>();

        var text = Text("Label", img.transform, label, fontSize, Color.white, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        text.Outline(1f);
        text.rectTransform.Stretch();
        return btn;
    }

    // Slider 0..1 (volume). Retorna o Slider; onChanged é chamado ao arrastar.
    public static Slider Slider(string name, Transform parent, Vector2 size, float value, System.Action<float> onChanged)
    {
        var root = Rect(name, parent);
        root.sizeDelta = size;

        var back = Image("Back", root, BarBackColor);
        back.raycastTarget = true; // clicar na trilha também move
        back.rectTransform.Stretch();

        var fillArea = Rect("FillArea", root).Stretch(1f);
        var fill = Image("Fill", fillArea, Gold);
        fill.rectTransform.Stretch();

        var handleArea = Rect("HandleArea", root).Stretch();
        var handle = Image("Handle", handleArea, Color.white);
        handle.raycastTarget = true;
        handle.rectTransform.sizeDelta = new Vector2(6f, 6f); // 6 px de largura, 3 px acima/abaixo da barra

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));
        return slider;
    }

    // Painel de menu centralizado: contorno preto de 2 px, fundo escuro e faixa dourada no topo.
    public static Image MenuPanel(string name, Transform parent, Vector2 size)
    {
        var panel = Image(name, parent, OutlineColor);
        panel.raycastTarget = true; // bloqueia cliques no que está atrás
        panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        var face = Image("Face", panel.transform, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 1f));
        face.rectTransform.Stretch(2f);
        var stripe = Image("Stripe", face.transform, Gold);
        stripe.rectTransform.anchorMin = new Vector2(0f, 1f);
        stripe.rectTransform.anchorMax = new Vector2(1f, 1f);
        stripe.rectTransform.pivot = new Vector2(0.5f, 1f);
        stripe.rectTransform.sizeDelta = new Vector2(0f, 2f);
        stripe.rectTransform.anchoredPosition = Vector2.zero;
        return panel;
    }

    // Ajusta uma barra feita com Bar(): 0..1 controlando a âncora direita.
    public static void SetBar(RectTransform fill, float value)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
    }
}
