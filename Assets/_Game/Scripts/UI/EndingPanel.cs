using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Depois do Ás: a escolha do final. 0 = PEGAR O TRONO · 1 = RASGAR O BARALHO.
// Dois cartões grandes lado a lado (funciona igual no toque e no mouse).
public class EndingPanel : MonoBehaviour
{
    private Action<int> onPick;
    private float openTime;

    public static EndingPanel Create(Transform parent)
    {
        var dim = UIFactory.Image("EndingPanel", parent, new Color(0.03f, 0.02f, 0.06f, 0.94f));
        dim.raycastTarget = true;
        dim.rectTransform.Stretch();
        var p = dim.gameObject.AddComponent<EndingPanel>();

        var title = UIFactory.Text("Title", dim.transform, "", 22f, UIFactory.Gold, TextAlignmentOptions.Center).NoLoc();
        title.Outline(2f, new Color32(0x7A, 0x2E, 0x0E, 0xFF));
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 30f));
        p.title = title;

        p.throne = p.Choice(dim.transform, 0, -128f, UIFactory.Gold);
        p.free = p.Choice(dim.transform, 1, 128f, new Color32(0x4C, 0xFF, 0x9A, 0xFF));
        dim.gameObject.SetActive(false);
        return p;
    }

    private TextMeshProUGUI title;
    private (TextMeshProUGUI name, TextMeshProUGUI body) throne, free;

    private (TextMeshProUGUI, TextMeshProUGUI) Choice(Transform parent, int ending, float x, Color color)
    {
        var size = new Vector2(220f, 150f);
        var button = UIFactory.Button("Ending" + ending, parent, "", size, () => Pick(ending));
        var rt = button.GetComponent<RectTransform>();
        rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -10f), size);
        var frame = UIFactory.Image("Frame", rt, color);
        frame.rectTransform.Stretch();
        frame.raycastTarget = false;
        var face = UIFactory.Image("Face", rt, new Color(0.1f, 0.07f, 0.16f, 1f));
        face.rectTransform.Stretch(3f);
        face.raycastTarget = false;
        var name = UIFactory.Text("Name", rt, "", 15f, color, TextAlignmentOptions.Center).NoLoc();
        name.Outline(1f);
        name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(205f, 22f));
        var body = UIFactory.Text("Body", rt, "", 9f, Color.white, TextAlignmentOptions.Top).NoLoc();
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 5f;
        var fit = body.GetComponent<TextFit>();
        if (fit != null) Destroy(fit);
        body.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(196f, 90f));
        return (name, body);
    }

    public void Open(Action<int> onPick)
    {
        this.onPick = onPick;
        title.text = Loc.Pick("THE TABLE IS YOURS", "A MESA É SUA", "LA MESA ES TUYA", "LA TABLE EST À TOI", "DER TISCH GEHÖRT DIR", "IL TAVOLO È TUO");
        throne.name.text = Loc.Pick("TAKE THE THRONE", "PEGAR O TRONO", "TOMAR EL TRONO", "PRENDRE LE TRÔNE", "DEN THRON NEHMEN", "PRENDERE IL TRONO");
        throne.body.text = Loc.Pick("Become the new house.\nThe kingdom keeps dealing.", "Vire a nova casa.\nO reino continua dando cartas.",
            "Conviértete en la nueva casa.\nEl reino sigue repartiendo.", "Deviens la nouvelle maison.\nLe royaume continue de distribuer.",
            "Werde die neue Bank.\nDas Königreich gibt weiter.", "Diventa il nuovo banco.\nIl regno continua a distribuire.");
        free.name.text = Loc.Pick("TEAR THE DECK", "RASGAR O BARALHO", "ROMPER LA BARAJA", "DÉCHIRER LE PAQUET", "DAS DECK ZERREISSEN", "STRAPPARE IL MAZZO");
        free.body.text = Loc.Pick("No more secret numbers.\nNobody rules by luck.", "Chega de números secretos.\nNinguém manda pela sorte.",
            "Se acabaron los números secretos.\nNadie manda por suerte.", "Fini les nombres secrets.\nPersonne ne règne par la chance.",
            "Keine geheimen Zahlen mehr.\nNiemand herrscht durch Glück.", "Basta numeri segreti.\nNessuno comanda per fortuna.");
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        openTime = Time.unscaledTime;
    }

    public void Close() => gameObject.SetActive(false);

    private void Pick(int ending)
    {
        if (Time.unscaledTime < openTime + 0.5f) return;
        onPick?.Invoke(ending);
    }

    // Debug (ponte): call EndingPanel.DebugPick 0|1
    public static string DebugPick(string ending)
    {
        var p = FindAnyObjectByType<EndingPanel>();
        if (p == null || !p.gameObject.activeSelf) return "closed";
        p.openTime = -1f;
        p.Pick(int.Parse(ending));
        return "ending " + ending;
    }
}
