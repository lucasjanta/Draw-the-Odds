using TMPro;
using UnityEngine;

// Impede que um texto de UI saia da caixa dele na horizontal: se a linha mais larga não couber
// na largura do RectTransform, diminui a fonte só o necessário. Só recalcula quando o texto muda.
// (O UIFactory.Text adiciona este componente em todo texto sem quebra de linha.)
[RequireComponent(typeof(TextMeshProUGUI))]
public class TextFit : MonoBehaviour
{
    private TextMeshProUGUI tmp;
    private float baseSize;
    private string lastText;
    private float lastWidth;

    private void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
    }

    private void LateUpdate()
    {
        if (tmp == null || tmp.textWrappingMode != TextWrappingModes.NoWrap) return;
        float width = tmp.rectTransform.rect.width;
        bool sizeChangedOutside = !Mathf.Approximately(tmp.fontSize, fitSize);
        if (tmp.text == lastText && Mathf.Approximately(width, lastWidth) && !sizeChangedOutside) return;
        // Tamanho "de projeto": o valor que alguém definiu por último (fora do TextFit)
        if (lastText == null || sizeChangedOutside) baseSize = tmp.fontSize;
        lastText = tmp.text;
        lastWidth = width;
        Fit(width);
    }

    private float fitSize = -1f;

    private void Fit(float width)
    {
        if (width <= 1f || string.IsNullOrEmpty(tmp.text)) return;
        tmp.fontSize = baseSize;
        float preferred = tmp.GetPreferredValues(tmp.text, float.PositiveInfinity, float.PositiveInfinity).x;
        fitSize = preferred > width ? Mathf.Max(baseSize * 0.5f, Mathf.Floor(baseSize * width / preferred)) : baseSize;
        tmp.fontSize = fitSize;
    }
}
