using TMPro;
using UnityEngine;

// Menu principal: Play (com tutorial), Skip Tutorial, How to Play e Options.
// (WebGL no navegador não tem "Quit": fechar a aba já faz isso.)
public class MainMenu : MonoBehaviour
{
    private GameObject mainPanel, howToPlay;
    private OptionsPanel options;
    private LeaderboardPanel ranking;
    private RectTransform titleRect;

    public static MainMenu Create(Transform parent, GameManager gm)
    {
        var back = UIFactory.Image("MainMenu", parent, new Color(0.04f, 0.02f, 0.09f, 1f));
        back.raycastTarget = true;
        back.rectTransform.Stretch();
        var menu = back.gameObject.AddComponent<MainMenu>();

        // ---------- Tela principal ----------
        var main = UIFactory.Rect("Main", back.transform).Stretch();
        menu.mainPanel = main.gameObject;

        var title = UIFactory.Text("Name", main, "DRAW THE ODDS", 44f, UIFactory.Gold, TextAlignmentOptions.Center);
        // Regular + contorno marrom: no Bold o W virava um bloco só.
        title.fontStyle = FontStyles.Normal;
        title.characterSpacing = 6f;
        title.Outline(2f, new Color32(0x7A, 0x2E, 0x0E, 0xFF));
        // Pivô no centro para o "pulsar" crescer a partir do meio
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -75f), new Vector2(600f, 54f));
        menu.titleRect = title.rectTransform;

        var sub = UIFactory.Text("Sub", main, "Every boss hides a secret number. Hit it with the right card.", 11f,
            new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
        sub.Outline(1f);
        sub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(600f, 16f));

        var size = new Vector2(180f, 26f);
        menu.AddButton(main, "PLAY", -142f, size, () => gm.StartGame(true), 16f);
        menu.AddButton(main, "SKIP TUTORIAL", -174f, size, () => gm.StartGame(false));
        menu.AddButton(main, "HOW TO PLAY", -206f, size, () => menu.ShowPage(menu.howToPlay));
        menu.AddButton(main, "RANKING", -238f, size, () => menu.ShowPage(menu.ranking.gameObject));
        menu.AddButton(main, "OPTIONS", -270f, size, () => menu.ShowPage(menu.options.gameObject));

        var credit = UIFactory.Text("Credit", main, "Official 2026 Unity Game Jam  ·  theme: LUCKY NUMBER", 8f,
            new Color(1f, 1f, 1f, 0.4f), TextAlignmentOptions.Center);
        credit.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(600f, 12f));

        // ---------- Subtelas ----------
        menu.howToPlay = BuildHowToPlay(back.transform, () => menu.ShowPage(null));
        menu.options = OptionsPanel.Create(back.transform, () => menu.ShowPage(null));
        menu.ranking = LeaderboardPanel.Create(back.transform, "BACK", () => menu.ShowPage(null));
        menu.ShowPage(null);
        back.gameObject.SetActive(false);
        return menu;
    }

    private void AddButton(Transform parent, string label, float y, Vector2 size, System.Action onClick, float fontSize = 13f)
    {
        var b = UIFactory.Button(label, parent, label, size, onClick, fontSize);
        b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), size);
    }

    // Mostra uma subtela (ou volta para a principal com null).
    private void ShowPage(GameObject page)
    {
        mainPanel.SetActive(page == null);
        howToPlay.SetActive(page == howToPlay);
        options.Show(page == options.gameObject);
        if (page == ranking.gameObject) ranking.Show(null); else ranking.Hide();
    }

    // Título pulsando devagar (escala) com um balanço leve.
    private void Update()
    {
        if (titleRect == null) return;
        float t = Time.unscaledTime;
        float pulse = 1f + 0.045f * Mathf.Sin(t * 2.6f);
        titleRect.localScale = new Vector3(pulse, pulse, 1f);
        titleRect.localRotation = Quaternion.Euler(0f, 0f, 1.2f * Mathf.Sin(t * 1.3f));
    }

    public void Show(bool show)
    {
        gameObject.SetActive(show);
        if (show) { transform.SetAsLastSibling(); ShowPage(null); }
    }

    // O texto de regras que antes ficava na tela de título.
    private static GameObject BuildHowToPlay(Transform parent, System.Action onBack)
    {
        var panel = UIFactory.MenuPanel("HowToPlay", parent, new Vector2(620f, 340f));

        var title = UIFactory.Text("Title", panel.transform, "HOW TO PLAY", 20f, UIFactory.Gold, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(600f, 26f));

        string suits =
            $"{SuitInfo.Colored(SuitInfo.Symbol(Suit.Clubs) + " CLUBS", Suit.Clubs)} normal damage     " +
            $"{SuitInfo.Colored(SuitInfo.Symbol(Suit.Spades) + " SPADES", Suit.Spades)} x1.5 damage\n" +
            $"{SuitInfo.Colored(SuitInfo.Symbol(Suit.Hearts) + " HEARTS", Suit.Hearts)} heal you (if close)     " +
            $"{SuitInfo.Colored(SuitInfo.Symbol(Suit.Diamonds) + " DIAMONDS", Suit.Diamonds)} damage + draw a card";

        string body =
            "<color=#FFD447>A / D</color> move   <color=#FFD447>SPACE</color> jump   <color=#FFD447>S + SPACE</color> drop down   <color=#FFD447>SHIFT</color> dash\n" +
            "<color=#FFD447>SCROLL</color> pick a card   <color=#FFD447>LEFT CLICK</color> throw it   <color=#FFD447>R</color> redraw hand (3x per run)   <color=#FFD447>ESC</color> pause\n\n" +
            "Each hit tells you <color=#FF8A3D>▲ HIGHER</color> or <color=#5A9BFF>▼ LOWER</color> (warm = close).\n" +
            "EXACT = critical + new number   ±2 = 2 dmg   ±5 = 1 dmg   further = 0\n\n" +
            suits + "\n\n" +
            "Grab a <color=#3CC86E>+</color> <color=#E84A5F>−</color> <color=#FFD447>×</color> orb: TIME STOPS!\n" +
            "<color=#FFD447>Q</color> / <color=#FFD447>E</color> put the selected card in a slot, <color=#FFD447>LEFT CLICK</color> merge, <color=#FFD447>RIGHT CLICK</color> skip.\n" +
            "The result is a surprise. Mixed suits give one of the two at random.";
        var text = UIFactory.Text("Body", panel.transform, body, 10f, Color.white, TextAlignmentOptions.Top);
        text.lineSpacing = 4f;
        text.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(600f, 240f));

        var back = UIFactory.Button("Back", panel.transform, "BACK", new Vector2(120f, 24f), onBack, 12f);
        back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(120f, 24f));
        return panel.gameObject;
    }
}
