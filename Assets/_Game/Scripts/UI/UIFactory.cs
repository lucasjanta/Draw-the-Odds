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

    // Ajusta uma barra feita com Bar(): 0..1 controlando a âncora direita.
    public static void SetBar(RectTransform fill, float value)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
    }
}
