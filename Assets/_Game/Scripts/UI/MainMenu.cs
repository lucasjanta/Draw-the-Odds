using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Menu principal: Play (com tutorial), Skip Tutorial, How to Play e Options.
// (WebGL no navegador não tem "Quit": fechar a aba já faz isso.)
public class MainMenu : MonoBehaviour
{
    private GameObject mainPanel, howToPlay;
    private OptionsPanel options;
    private LeaderboardPanel ranking;
    private RectTransform titleRect;
    private RawImage pattern;
    private const int PatternTile = 128;   // textura do padrão: 4×4 células de 32 px
    private const float PatternScale = 3f; // cada pixel do padrão = 3×3 pixels da tela (naipes maiores)

    public static MainMenu Create(Transform parent, GameManager gm)
    {
        var back = UIFactory.Image("MainMenu", parent, new Color32(0x1C, 0x11, 0x30, 0xFF));
        back.raycastTarget = true;
        back.rectTransform.Stretch();
        var menu = back.gameObject.AddComponent<MainMenu>();

        // Fundo animado: naipes pretos em diagonal, deslizando devagar (textura repetida + uvRect).
        menu.pattern = new GameObject("SuitPattern", typeof(RectTransform)).AddComponent<RawImage>();
        menu.pattern.transform.SetParent(back.transform, false);
        menu.pattern.rectTransform.Stretch();
        menu.pattern.texture = SuitPatternTexture();
        menu.pattern.color = new Color(0f, 0f, 0f, 0.5f);
        menu.pattern.raycastTarget = false;

        // ---------- Tela principal ----------
        var main = UIFactory.Rect("Main", back.transform).Stretch();
        menu.mainPanel = main.gameObject;

        var title = UIFactory.Text("Name", main, "DRAW THE ODDS", 44f, UIFactory.Gold, TextAlignmentOptions.Center);
        // Regular + contorno marrom: no Bold o W virava um bloco só.
        title.fontStyle = FontStyles.Normal;
        if (gm.Config.TitleFont != null) { title.font = gm.Config.TitleFont; title.fontSize = 52f; title.characterSpacing = 2f; }
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
    // Debug (ponte do Editor): "howto" | "options" | "ranking" | "main"
    public static string DebugPage(string page)
    {
        var m = FindAnyObjectByType<MainMenu>();
        if (m == null) return "no menu";
        m.ShowPage(page == "howto" ? m.howToPlay : page == "options" ? m.options.gameObject : page == "ranking" ? m.ranking.gameObject : null);
        return "page " + page;
    }

    private void ShowPage(GameObject page)
    {
        mainPanel.SetActive(page == null);
        howToPlay.SetActive(page == howToPlay);
        options.Show(page == options.gameObject);
        if (page == ranking.gameObject) ranking.Show(null, GameManager.SelectedDifficulty); else ranking.Hide();
    }

    // Título pulsando devagar (escala) com um balanço leve.
    private void Update()
    {
        if (titleRect == null) return;
        float t = Time.unscaledTime;

        // Padrão andando na diagonal (para cima e à direita). Movimento CONTÍNUO: antes ele pulava
        // de texel em texel (10 pulos de 3 px por segundo = parecia travado). A textura usa filtro
        // Point, então mesmo com deslocamento "quebrado" o pixel art continua nítido; o movimento só
        // fica limitado pelos pixels da tela, que são bem menores.
        var size = pattern.rectTransform.rect.size;
        float offset = Mathf.Repeat(t * 10f, PatternTile) / PatternTile; // Repeat: evita números enormes com o tempo
        float tile = PatternTile * PatternScale;
        pattern.uvRect = new Rect(-offset, -offset, size.x / tile, size.y / tile);
        float pulse = 1f + 0.045f * Mathf.Sin(t * 2.6f);
        titleRect.localScale = new Vector3(pulse, pulse, 1f);
        titleRect.localRotation = Quaternion.Euler(0f, 0f, 1.2f * Mathf.Sin(t * 1.3f));
    }

    public void Show(bool show)
    {
        gameObject.SetActive(show);
        if (show) { transform.SetAsLastSibling(); ShowPage(null); }
    }

    // Textura do padrão: 4×4 células, cada uma com um naipe pixel art 9×9. O naipe depende de
    // (coluna + linha), então cada diagonal tem um naipe só: ♠ ♥ ♣ ♦ ♠ ♥ ...
    private static Texture2D SuitPatternTexture()
    {
        string[][] suits =
        {
            new[] { "....1....", "...111...", "..11111..", ".1111111.", "111111111", "111111111", ".11.1.11.", "....1....", "...111..." }, // ♠
            new[] { ".11...11.", "1111.1111", "111111111", "111111111", ".1111111.", "..11111..", "...111...", "....1....", "........." }, // ♥
            new[] { "...111...", "..11111..", "..11111..", ".1.111.1.", "111111111", "111111111", ".11.1.11.", "....1....", "...111..." }, // ♣
            new[] { "....1....", "...111...", "..11111..", ".1111111.", "111111111", ".1111111.", "..11111..", "...111...", "....1...." }, // ♦
        };
        const int cell = PatternTile / 4;
        var tex = new Texture2D(PatternTile, PatternTile, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
        };
        var pixels = new Color32[PatternTile * PatternTile];
        for (int cy = 0; cy < 4; cy++)
            for (int cx = 0; cx < 4; cx++)
            {
                string[] mask = suits[(cx + cy) % 4];
                int ox = cx * cell + (cell - 9) / 2, oy = cy * cell + (cell - 9) / 2;
                for (int y = 0; y < 9; y++)
                    for (int x = 0; x < 9; x++)
                        if (mask[8 - y][x] == '1') pixels[(oy + y) * PatternTile + ox + x] = new Color32(255, 255, 255, 255);
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // Regras (inglês ou português, com as teclas e naipes coloridos).
    private static string HowToPlayText()
    {
        string Y(string k) => $"<color=#FFD447>{k}</color>";
        string C(Suit s) => SuitInfo.Colored(SuitInfo.Symbol(s), s);
        string ops = "<color=#3CC86E>+</color> <color=#E84A5F>−</color> <color=#FFD447>×</color>";
        if (Loc.IsPt)
            return
                $"{Y("A/D")} anda  {Y("ESPAÇO")} pula  {Y("S+ESPAÇO")} desce  {Y("SHIFT")} dash\n" +
                $"{Y("RODINHA")} escolhe  {Y("CLIQUE")} arremessa  {Y("R")} nova mão  {Y("ESC")} pausa\n\n" +
                "Descubra o <color=#FFD447>NÚMERO SECRETO</color> de cada chefe.\n" +
                "<color=#FF8A3D>▲ MAIOR</color> / <color=#5A9BFF>▼ MENOR</color>  mais perto = mais dano\n" +
                "<color=#FFD447>EXATO = CRÍTICO</color>, depois um número novo\n\n" +
                $"{C(Suit.Clubs)} normal   {C(Suit.Spades)} x1.5   {C(Suit.Hearts)} cura   {C(Suit.Diamonds)} +1 carta\n\n" +
                $"Orbes {ops} param o tempo: {Y("Q/E")} coloca, {Y("CLIQUE")} funde";
        return
            $"{Y("A/D")} move  {Y("SPACE")} jump  {Y("S+SPACE")} drop  {Y("SHIFT")} dash\n" +
            $"{Y("SCROLL")} pick card  {Y("CLICK")} throw  {Y("R")} redraw  {Y("ESC")} pause\n\n" +
            "Find each boss's <color=#FFD447>SECRET NUMBER</color>.\n" +
            "<color=#FF8A3D>▲ HIGHER</color> / <color=#5A9BFF>▼ LOWER</color>  closer = more damage\n" +
            "<color=#FFD447>EXACT = CRITICAL</color>, then a new number\n\n" +
            $"{C(Suit.Clubs)} normal   {C(Suit.Spades)} x1.5   {C(Suit.Hearts)} heal   {C(Suit.Diamonds)} +1 card\n\n" +
            $"Orbs {ops} stop time: {Y("Q/E")} place, {Y("CLICK")} merge";
    }

    // O texto de regras que antes ficava na tela de título.
    private static GameObject BuildHowToPlay(Transform parent, System.Action onBack)
    {
        var panel = UIFactory.MenuPanel("HowToPlay", parent, new Vector2(620f, 340f));

        var title = UIFactory.Text("Title", panel.transform, "HOW TO PLAY", 20f, UIFactory.Gold, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(600f, 26f));

        var text = UIFactory.Text("Body", panel.transform, HowToPlayText(), 10f, Color.white, TextAlignmentOptions.Top).NoLoc();
        Loc.OnChanged += () => { if (text != null) text.text = HowToPlayText(); }; // trocou o idioma: remonta
        text.lineSpacing = 4f;
        text.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(600f, 240f));

        var back = UIFactory.Button("Back", panel.transform, "BACK", new Vector2(120f, 24f), onBack, 12f);
        back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(120f, 24f));
        return panel.gameObject;
    }
}
