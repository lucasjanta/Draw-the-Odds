using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// "Juice" dos botões: cresce um pouco com o mouse em cima, afunda ao apertar e dá um pulinho ao soltar.
// Tudo em tempo real (funciona com o jogo pausado). UIFactory.Button adiciona sozinho.
public class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private const float HoverScale = 1.06f, PressScale = 0.94f;

    private Selectable selectable;
    private bool over, down;
    private float scale = 1f, velocity;

    private void Awake() => selectable = GetComponent<Selectable>();

    private void OnDisable()
    {
        over = down = false;
        scale = 1f;
        velocity = 0f;
        transform.localScale = Vector3.one;
    }

    public void OnPointerEnter(PointerEventData e) => over = true;
    public void OnPointerExit(PointerEventData e) { over = false; down = false; }
    public void OnPointerDown(PointerEventData e) => down = true;

    public void OnPointerUp(PointerEventData e)
    {
        if (down && over) velocity += 2.5f; // pulinho ao soltar
        down = false;
    }

    // Mola simples: a escala persegue o alvo e passa um pouquinho (fica "elástico").
    private void Update()
    {
        bool active = selectable == null || selectable.interactable;
        float target = !active ? 1f : down ? PressScale : over ? HoverScale : 1f;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        velocity += (target - scale) * 400f * dt;
        velocity *= Mathf.Exp(-22f * dt);
        scale += velocity * dt;
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
