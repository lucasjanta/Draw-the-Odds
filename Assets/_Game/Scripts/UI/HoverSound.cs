using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Toca o som de "hover" quando o mouse passa por cima de um botão (UIFactory.Button adiciona sozinho).
public class HoverSound : MonoBehaviour, IPointerEnterHandler
{
    private Selectable selectable;

    private void Awake() => selectable = GetComponent<Selectable>();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable != null && !selectable.interactable) return;
        var gm = GameManager.Instance;
        if (gm != null) AudioManager.Ui(gm.Config.SfxUiHover, 0.6f);
    }
}
