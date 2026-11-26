using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Joystick "flutuante": a área invisível cobre a parte esquerda da tela; onde o dedo encosta, a base
// aparece e o pino segue o dedo. Escreve em VirtualInput.Move.
// Saída "digital" (como o teclado): passou de 30% para o lado = anda na velocidade cheia.
// Para baixo forte (+ pulo) = descer da plataforma, igual a S + Espaço.
public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInteractiveUi
{
    private const float Radius = 34f;        // raio do movimento do pino (px do Canvas de referência)
    private const float DeadZone = 0.3f;
    private const float DownThreshold = 0.6f;

    private RectTransform area, baseRect, knob;
    private CanvasGroup visual;
    private int pointerId = int.MinValue;
    private Vector2 restPosition;

    public static TouchJoystick Create(Transform parent)
    {
        // Área de toque: metade de baixo da tela, do lado esquerdo (fora da mão de cartas no meio).
        var zone = UIFactory.Image("JoystickZone", parent, new Color(0f, 0f, 0f, 0f));
        zone.raycastTarget = true;
        var zr = zone.rectTransform;
        zr.anchorMin = new Vector2(0f, 0f);
        zr.anchorMax = new Vector2(0.21f, 0.62f); // 0,21 × 640 = 134 px: não encosta na mão de 10 cartas (começa em ~138)
        zr.offsetMin = zr.offsetMax = Vector2.zero;

        var j = zone.gameObject.AddComponent<TouchJoystick>();
        j.area = zr;

        var baseImg = UIFactory.Image("Base", zr, new Color(1f, 1f, 1f, 0.16f), Fx.Circle);
        j.baseRect = baseImg.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(64f, 62f), new Vector2(Radius * 2f + 18f, Radius * 2f + 18f));
        var ring = UIFactory.Image("Ring", j.baseRect, new Color(1f, 1f, 1f, 0.25f), Fx.Circle);
        ring.rectTransform.Stretch(4f);
        var knobImg = UIFactory.Image("Knob", j.baseRect, new Color(1f, 1f, 1f, 0.7f), Fx.Circle);
        j.knob = knobImg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
        j.visual = j.baseRect.gameObject.AddComponent<CanvasGroup>();
        j.visual.alpha = 0.45f;
        j.restPosition = j.baseRect.anchoredPosition;
        return j;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (pointerId != int.MinValue) return; // já tem um dedo nele
        pointerId = e.pointerId;
        // A base vai para onde o dedo encostou.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var local);
        baseRect.anchoredPosition = local - area.rect.min; // âncora no canto inferior esquerdo da área
        visual.alpha = 1f;
        UpdateStick(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId == pointerId) UpdateStick(e);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != pointerId) return;
        Release();
    }

    private void UpdateStick(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, e.position, e.pressEventCamera, out var local);
        Vector2 offset = Vector2.ClampMagnitude(local, Radius);
        knob.anchoredPosition = offset;
        Vector2 v = offset / Radius;
        float x = Mathf.Abs(v.x) > DeadZone ? Mathf.Sign(v.x) : 0f;
        float y = v.y < -DownThreshold ? -1f : v.y > DownThreshold ? 1f : 0f;
        VirtualInput.Move = new Vector2(x, y);
    }

    private void Release()
    {
        pointerId = int.MinValue;
        knob.anchoredPosition = Vector2.zero;
        baseRect.anchoredPosition = restPosition;
        visual.alpha = 0.45f;
        VirtualInput.Move = Vector2.zero;
    }

    // Escondido/desligado no meio de um toque: solta o joystick.
    private void OnDisable()
    {
        if (pointerId != int.MinValue) Release();
    }
}
