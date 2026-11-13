using TMPro;
using UnityEngine;

// Contorno "pixel art" para textos de UI: 8 cópias escuras do texto, deslocadas em volta e
// desenhadas ATRÁS dele (irmãs anteriores no mesmo pai). O outline do TMP quase não aparece
// com a fonte pixel (o SDF dela tem pouca margem), por isso este truque.
// As cópias acompanham o texto original a cada frame (texto, tamanho, posição, escala, rotação).
public class PixelOutline : MonoBehaviour
{
    private static readonly Vector2[] Offsets =
    {
        new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1),
        new Vector2(-1, 0),                      new Vector2(1, 0),
        new Vector2(-1, 1),  new Vector2(0, 1),  new Vector2(1, 1),
    };

    private TextMeshProUGUI source;
    private RectTransform sourceRect;
    private TextMeshProUGUI[] copies;
    private float thickness;
    private string lastSource, plain = "";

    public static PixelOutline Add(TextMeshProUGUI text, float thickness, Color color)
    {
        var o = text.gameObject.AddComponent<PixelOutline>();
        o.source = text;
        o.sourceRect = text.rectTransform;
        o.thickness = thickness;
        o.copies = new TextMeshProUGUI[Offsets.Length];
        int index = text.transform.GetSiblingIndex();
        for (int i = 0; i < Offsets.Length; i++)
        {
            var c = UIFactory.Text(text.name + "Outline", text.transform.parent, text.text, text.fontSize, color, text.alignment);
            c.transform.SetSiblingIndex(index); // antes do original = desenhado atrás
            c.NoLoc(); // a cópia só repete o texto (já traduzido) do original
            o.copies[i] = c;
        }
        o.Sync();
        return o;
    }

    private void LateUpdate() => Sync();

    private void Sync()
    {
        for (int i = 0; i < copies.Length; i++)
        {
            var c = copies[i];
            if (c == null) continue;
            c.gameObject.SetActive(source.gameObject.activeInHierarchy && source.enabled);
            // Tira as tags de cor: o contorno é todo escuro mesmo com partes coloridas no texto
            if (lastSource != source.text)
            {
                lastSource = source.text;
                plain = System.Text.RegularExpressions.Regex.Replace(source.text, "<color=[^>]*>|</color>", "");
            }
            if (c.text != plain) c.text = plain;
            c.fontSize = source.fontSize;
            if (c.font != source.font) c.font = source.font;
            c.fontStyle = source.fontStyle;
            c.characterSpacing = source.characterSpacing;
            c.lineSpacing = source.lineSpacing;
            c.alignment = source.alignment;
            c.textWrappingMode = source.textWrappingMode;
            var a = c.color;
            a.a = source.color.a * source.alpha;
            c.color = a;

            var r = c.rectTransform;
            r.anchorMin = sourceRect.anchorMin;
            r.anchorMax = sourceRect.anchorMax;
            r.pivot = sourceRect.pivot;
            r.sizeDelta = sourceRect.sizeDelta;
            r.localScale = sourceRect.localScale;
            r.localRotation = sourceRect.localRotation;
            r.anchoredPosition = sourceRect.anchoredPosition + Offsets[i] * thickness;
        }
    }

    private void OnDestroy()
    {
        if (copies == null) return;
        foreach (var c in copies) if (c != null) Destroy(c.gameObject);
    }
}
