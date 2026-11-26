using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Botão redondo de toque que avisa quando o dedo ENCOSTA e quando SOLTA (o Button normal do Unity
// só avisa no "clique" completo, tarde demais para pular). Usado no pulo, dash, redraw e pausa.
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IInteractiveUi
{
    public event Action OnDown;
    public event Action OnUp;

    private Image face;
    private Color normalColor;
    private bool held;
    private TextMeshProUGUI label;

    public static TouchButton Create(Transform parent, string name, string text, float size, Vector2 anchor, Vector2 position, Color color, float fontSize = 12f)
    {
        var outline = UIFactory.Image(name, parent, new Color(0f, 0f, 0f, 0.55f), Fx.Circle);
        outline.raycastTarget = true;
        outline.rectTransform.Place(anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
        var b = outline.gameObject.AddComponent<TouchButton>();
        b.face = UIFactory.Image("Face", outline.transform, color, Fx.Circle);
        b.face.rectTransform.Stretch(3f);
        b.normalColor = color;
        b.label = UIFactory.Text("Label", outline.transform, text, fontSize, Color.white, TextAlignmentOptions.Center);
        b.label.fontStyle = FontStyles.Bold;
        b.label.Outline(1f);
        b.label.rectTransform.Stretch();
        return b;
    }

    // Botão em forma de CARTA (verso do baralho) com um ícone no meio. Usado no REDRAW (nova mão):
    // dá para entender que ele compra cartas novas, sem depender de uma letra.
    public static TouchButton CreateCard(Transform parent, string name, Sprite cardBack, Sprite icon, float height, Vector2 anchor, Vector2 position)
    {
        float width = height * 58f / 80f; // proporção da carta (58×80)
        var root = UIFactory.Rect(name, parent);
        root.Place(anchor, new Vector2(0.5f, 0.5f), position, new Vector2(width, height));
        var hit = root.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f); // área de toque invisível
        hit.raycastTarget = true;
        var b = root.gameObject.AddComponent<TouchButton>();

        b.face = UIFactory.Image("Card", root, Color.white, cardBack);
        b.face.rectTransform.Stretch();
        b.face.preserveAspect = true;
        b.normalColor = Color.white;

        // Setas em roxo escuro: o verso da carta é quase branco (ícone branco sumia)
        var iconImage = UIFactory.Image("Icon", root, IconColor, icon);
        iconImage.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width * 0.78f, width * 0.78f));
        iconImage.preserveAspect = true;
        b.icon = iconImage;

        // Quantas trocas ainda restam (canto de baixo)
        b.label = UIFactory.Text("Label", root, "", 9f, Color.white, TextAlignmentOptions.Center);
        b.label.fontStyle = FontStyles.Bold;
        b.label.Outline(1f);
        b.label.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(width, 12f));
        return b;
    }

    private Image icon;
    private static readonly Color IconColor = new Color32(0x4A, 0x2E, 0x9E, 0xFF);

    public void SetLabel(string text) => label.text = text;

    // Cooldown (dash): mostra o botão apagado até carregar.
    public void SetReady(bool ready)
    {
        face.color = ready ? (held ? Pressed : normalColor) : new Color(normalColor.r * 0.4f, normalColor.g * 0.4f, normalColor.b * 0.4f, normalColor.a * 0.7f);
        if (icon != null) icon.color = ready ? IconColor : new Color(IconColor.r, IconColor.g, IconColor.b, 0.35f);
    }

    private Color Pressed => Color.Lerp(normalColor, Color.white, 0.45f);

    public void OnPointerDown(PointerEventData e)
    {
        held = true;
        face.color = Pressed;
        transform.localScale = Vector3.one * 0.92f;
        OnDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!held) return;
        held = false;
        face.color = normalColor;
        transform.localScale = Vector3.one;
        OnUp?.Invoke();
    }

    private void OnDisable()
    {
        if (held) OnPointerUp(null);
    }
}
