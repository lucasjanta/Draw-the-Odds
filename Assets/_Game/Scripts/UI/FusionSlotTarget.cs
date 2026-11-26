using UnityEngine;
using UnityEngine.EventSystems;

// Um slot do painel da fusão. Recebe a carta arrastada (a HandView procura este componente onde a
// carta foi solta) e, num toque/clique, coloca a carta SELECIONADA nele (alternativa ao Q / E).
public class FusionSlotTarget : MonoBehaviour, IPointerClickHandler, IInteractiveUi
{
    public bool IsFirst { get; private set; }

    public static FusionSlotTarget Attach(GameObject go, bool isFirst)
    {
        var t = go.AddComponent<FusionSlotTarget>();
        t.IsFirst = isFirst;
        return t;
    }

    public void OnPointerClick(PointerEventData e)
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.Fusion.AcceptsInput) return;
        gm.Fusion.PutInSlot(IsFirst);
    }
}
