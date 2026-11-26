using UnityEngine;
using UnityEngine.EventSystems;

// Fundo invisível que cobre a tela inteira atrás dos outros controles de toque.
// Tocar no cenário (fora do joystick, botões e cartas) arremessa a carta selecionada naquela direção.
// Só existe com os controles de toque ligados: no PC o arremesso continua sendo o clique do mouse.
public class WorldTapCatcher : MonoBehaviour, IPointerDownHandler
{
    public static WorldTapCatcher Create(Transform parent)
    {
        var img = UIFactory.Image("WorldTap", parent, new Color(0f, 0f, 0f, 0f));
        img.raycastTarget = true;
        img.rectTransform.Stretch();
        return img.gameObject.AddComponent<WorldTapCatcher>();
    }

    // No "encostar" (não no soltar): arremesso imediato, sem atraso de clique.
    public void OnPointerDown(PointerEventData e)
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.CanAct || gm.Fusion.Active) return;
        var cards = gm.Player.GetComponent<PlayerCards>();
        if (cards != null) cards.ThrowAtScreen(e.position);
    }
}
