using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Menu principal: Play (direto no Valete, com dicas curtas) + dificuldade, Tutorial, Endless (infinito,
// liberado ao vencer a Dama), How to Play, Ranking e Options.
// (WebGL no navegador não tem "Quit": fechar a aba já faz isso.)
public class MainMenu : MonoBehaviour
{
    private GameObject mainPanel, howToPlay;
    private GameManager gm;
    private Button endlessButton;
    private TextMeshProUGUI endlessLabel, difficultyLabel;
    private TextMeshProUGUI howToText;
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
        menu.gm = gm;

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
        menu.AddButton(main, "PLAY", -134f, size, gm.StartRun, 16f);
        // Dificuldade ao lado do JOGAR: ◄ NORMAL ► (clique troca; fica salva)
        var diff = menu.AddButton(main, "", -134f, new Vector2(104f, 26f), menu.CycleDifficulty, 10f);
        diff.GetComponent<RectTransform>().anchoredPosition = new Vector2(148f, -134f);
        menu.difficultyLabel = diff.transform.Find("Label").GetComponent<TextMeshProUGUI>().NoLoc();
        menu.AddButton(main, "TUTORIAL", -166f, size, () => gm.StartGame(true));
        menu.endlessButton = menu.AddButton(main, "ENDLESS", -198f, size, gm.StartEndless);
        menu.endlessLabel = menu.endlessButton.transform.Find("Label").GetComponent<TextMeshProUGUI>().NoLoc();
        // Bloqueado: roxo escuro (o cinza claro padrão do Unity deixava o texto ilegível)
        var colors = menu.endlessButton.colors;
        colors.disabledColor = new Color32(0x2A, 0x22, 0x40, 0xFF);
        menu.endlessButton.colors = colors;
        menu.AddButton(main, "HOW TO PLAY", -230f, size, () => menu.ShowPage(menu.howToPlay));
        menu.AddButton(main, "RANKING", -262f, size, () => menu.ShowPage(menu.ranking.gameObject));
        menu.AddButton(main, "OPTIONS", -294f, size, () => menu.ShowPage(menu.options.gameObject));
        Loc.OnChanged += () => { if (menu != null) menu.RefreshLabels(); };
        KeyBindings.OnChanged += () => { if (menu != null) menu.RefreshLabels(); }; // teclas novas aparecem no COMO JOGAR

        var credit = UIFactory.Text("Credit", main, "Official 2026 Unity Game Jam  ·  theme: LUCKY NUMBER", 8f,
            new Color(1f, 1f, 1f, 0.4f), TextAlignmentOptions.Center);
        credit.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(600f, 12f));

        // ---------- Subtelas ----------
        menu.howToPlay = BuildHowToPlay(back.transform, () => menu.ShowPage(null), out menu.howToText);
        menu.options = OptionsPanel.Create(back.transform, () => menu.ShowPage(null));
        menu.ranking = LeaderboardPanel.Create(back.transform, "BACK", () => menu.ShowPage(null));
        menu.ShowPage(null);
        back.gameObject.SetActive(false);
        return menu;
    }

    private Button AddButton(Transform parent, string label, float y, Vector2 size, System.Action onClick, float fontSize = 13f)
    {
        var b = UIFactory.Button(label, parent, label, size, onClick, fontSize);
        b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), size);
        return b;
    }

    private void CycleDifficulty()
    {
        GameManager.SelectedDifficulty = (Difficulty)(((int)GameManager.SelectedDifficulty + 1) % 3);
        RefreshLabels();
    }

    // Textos que mudam: dificuldade escolhida e se o infinito já foi liberado.
    private void RefreshLabels()
    {
        var d = GameManager.SelectedDifficulty;
        string color = d == Difficulty.Easy ? "#3CC86E" : d == Difficulty.Hardcore ? "#E84A5F" : "#FFD447";
        string name = Loc.T(d == Difficulty.Easy ? "EASY" : d == Difficulty.Hardcore ? "HARDCORE" : "NORMAL");
        difficultyLabel.text = $"◄ <color={color}>{name}</color> ►";

        bool unlocked = GameManager.EndlessUnlocked;
        endlessButton.interactable = unlocked;
        endlessLabel.text = unlocked ? Loc.T("ENDLESS")
                                     : "<size=9>" + Loc.Pick("ENDLESS - BEAT THE QUEEN", "INFINITO - VENÇA A DAMA", "INFINITO - VENCE A LA REINA",
                                                             "INFINI - BATS LA DAME", "ENDLOS - BESIEGE DIE DAME", "INFINITO - BATTI LA REGINA") + "</size>";
        endlessLabel.alpha = unlocked ? 1f : 0.45f;
        if (howToText != null) howToText.text = HowToPlayText();
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
        HandleBackButton();
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

    // Botão VOLTAR do Android (chega como a tecla Esc) e Esc no PC:
    // numa subpágina (como jogar, ranking, opções) volta para o menu; no menu principal do Android fecha o jogo.
    private void HandleBackButton()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null || KeyBindings.Busy || !keyboard.escapeKey.wasPressedThisFrame) return;
        if (!mainPanel.activeSelf) ShowPage(null);
        else if (Application.platform == RuntimePlatform.Android) Application.Quit();
    }

    public void Show(bool show)
    {
        gameObject.SetActive(show);
        if (show) { transform.SetAsLastSibling(); ShowPage(null); RefreshLabels(); }
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
        // Controles de toque: as 2 primeiras linhas falam do joystick e dos botões.
        bool touch = VirtualInput.Active;
        string N(GameKey k) => KeyBindings.Name(k); // teclas configuradas (Opções → CONTROLS)
        string W(string en, string pt, string es, string fr, string de, string it) => Loc.Pick(en, pt, es, fr, de, it);

        // Palavras dos controles
        string move = W("move", "anda", "mover", "bouger", "laufen", "muoviti");
        string jump = W("jump", "pula", "saltar", "sauter", "springen", "salta");
        string drop = W("drop", "desce", "bajar", "descendre", "runter", "scendi");
        string dash = W("dash", "dash", "dash", "dash", "Dash", "dash");
        string pickCard = W("pick card", "escolhe a carta", "elegir carta", "choisir", "Karte wählen", "scegli carta");
        string throwIt = W("throw", "arremessa", "lanzar", "lancer", "werfen", "lancia");
        string redraw = W("new hand", "nova mão", "nueva mano", "nouvelle main", "neue Hand", "nuova mano");
        string pause = W("pause", "pausa", "pausa", "pause", "Pause", "pausa");

        string controls = touch
            ? $"{Y("JOYSTICK")} {move}  {Y("▲")} {jump}  {Y(W("DOWN", "BAIXO", "ABAJO", "BAS", "RUNTER", "GIÙ") + " + ▲")} {drop}  {Y("»")} {dash}\n" +
              $"{Y(W("TAP", "TOQUE", "TOCA", "TOUCHE", "TIPPEN", "TOCCA"))} {pickCard}  {Y(W("DRAG UP", "ARRASTE P/ CIMA", "ARRASTRA ARRIBA", "GLISSE EN HAUT", "HOCHZIEHEN", "TRASCINA SU"))} {throwIt}\n" +
              $"{Y(W("CARD BUTTON", "BOTÃO DA CARTA", "BOTÓN CARTA", "BOUTON CARTE", "KARTENKNOPF", "PULSANTE CARTA"))} {redraw}  {Y("II")} {pause}\n\n"
            : $"{Y(N(GameKey.Left) + "/" + N(GameKey.Right))} {move}  {Y(N(GameKey.Jump))} {jump}  {Y(N(GameKey.Down) + "+" + N(GameKey.Jump))} {drop}  {Y(N(GameKey.Dash))} {dash}\n" +
              $"{Y(W("SCROLL", "RODINHA", "RUEDA", "MOLETTE", "MAUSRAD", "ROTELLA"))} {pickCard}  {Y(W("CLICK", "CLIQUE", "CLIC", "CLIC", "KLICK", "CLIC"))} {throwIt}  " +
              $"{Y(N(GameKey.Redraw))} {redraw}  {Y(N(GameKey.Pause) + "/ESC")} {pause}\n\n";

        string rules = W(
            "Find each boss's <color=#FFD447>SECRET NUMBER</color>.\n<color=#FF8A3D>▲ HIGHER</color> / <color=#5A9BFF>▼ LOWER</color>  closer = more damage\n<color=#FFD447>EXACT = CRITICAL</color>, then a new number",
            "Descubra o <color=#FFD447>NÚMERO SECRETO</color> de cada chefe.\n<color=#FF8A3D>▲ MAIOR</color> / <color=#5A9BFF>▼ MENOR</color>  mais perto = mais dano\n<color=#FFD447>EXATO = CRÍTICO</color>, depois um número novo",
            "Descubre el <color=#FFD447>NÚMERO SECRETO</color> de cada jefe.\n<color=#FF8A3D>▲ MAYOR</color> / <color=#5A9BFF>▼ MENOR</color>  más cerca = más daño\n<color=#FFD447>EXACTO = CRÍTICO</color>, luego un número nuevo",
            "Trouve le <color=#FFD447>NOMBRE SECRET</color> de chaque boss.\n<color=#FF8A3D>▲ PLUS</color> / <color=#5A9BFF>▼ MOINS</color>  plus près = plus de dégâts\n<color=#FFD447>EXACT = CRITIQUE</color>, puis un nouveau nombre",
            "Finde die <color=#FFD447>GEHEIME ZAHL</color> jedes Bosses.\n<color=#FF8A3D>▲ HÖHER</color> / <color=#5A9BFF>▼ TIEFER</color>  näher = mehr Schaden\n<color=#FFD447>GENAU = KRITISCH</color>, dann eine neue Zahl",
            "Trova il <color=#FFD447>NUMERO SEGRETO</color> di ogni boss.\n<color=#FF8A3D>▲ PIÙ ALTO</color> / <color=#5A9BFF>▼ PIÙ BASSO</color>  più vicino = più danni\n<color=#FFD447>ESATTO = CRITICO</color>, poi un nuovo numero");

        string suits = $"{C(Suit.Clubs)} normal   {C(Suit.Spades)} x1.5   {C(Suit.Hearts)} " +
                       W("heal", "cura", "cura", "soin", "Heilung", "cura") + $"   {C(Suit.Diamonds)} +1 " +
                       W("card", "carta", "carta", "carte", "Karte", "carta");

        string orbs = touch
            ? W($"Orbs {ops} stop time: {Y("DRAG")} 2 cards to the slots, {Y("MERGE")}",
                $"Orbes {ops} param o tempo: {Y("ARRASTE")} 2 cartas aos espaços, {Y("JUNTAR")}",
                $"Los orbes {ops} paran el tiempo: {Y("ARRASTRA")} 2 cartas a los huecos, {Y("FUSIONAR")}",
                $"Les orbes {ops} arrêtent le temps : {Y("GLISSE")} 2 cartes, {Y("FUSIONNER")}",
                $"Orbs {ops} stoppen die Zeit: {Y("ZIEH")} 2 Karten in die Plätze, {Y("FUSION")}",
                $"Le sfere {ops} fermano il tempo: {Y("TRASCINA")} 2 carte negli slot, {Y("FONDI")}")
            : W($"Orbs {ops} stop time: {Y(N(GameKey.SlotA) + "/" + N(GameKey.SlotB))} place, {Y("CLICK")} merge",
                $"Orbes {ops} param o tempo: {Y(N(GameKey.SlotA) + "/" + N(GameKey.SlotB))} coloca, {Y("CLIQUE")} funde",
                $"Los orbes {ops} paran el tiempo: {Y(N(GameKey.SlotA) + "/" + N(GameKey.SlotB))} coloca, {Y("CLIC")} fusiona",
                $"Les orbes {ops} arrêtent le temps : {Y(N(GameKey.SlotA) + "/" + N(GameKey.SlotB))} placer, {Y("CLIC")} fusionner",
                $"Orbs {ops} stoppen die Zeit: {Y(N(GameKey.SlotA) + "/" + N(GameKey.SlotB))} ablegen, {Y("KLICK")} verschmelzen",
                $"Le sfere {ops} fermano il tempo: {Y(N(GameKey.SlotA) + "/" + N(GameKey.SlotB))} metti, {Y("CLIC")} fondi");

        return controls + rules + "\n\n" + suits + "\n\n" + orbs;
    }

    // O texto de regras que antes ficava na tela de título.
    private static GameObject BuildHowToPlay(Transform parent, System.Action onBack, out TextMeshProUGUI bodyText)
    {
        var panel = UIFactory.MenuPanel("HowToPlay", parent, new Vector2(620f, 340f));

        var title = UIFactory.Text("Title", panel.transform, "HOW TO PLAY", 20f, UIFactory.Gold, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(600f, 26f));

        var text = UIFactory.Text("Body", panel.transform, HowToPlayText(), 10f, Color.white, TextAlignmentOptions.Top).NoLoc();
        Loc.OnChanged += () => { if (text != null) text.text = HowToPlayText(); }; // trocou o idioma: remonta
        text.lineSpacing = 4f;
        text.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(600f, 240f));
        bodyText = text;

        var back = UIFactory.Button("Back", panel.transform, "BACK", new Vector2(120f, 24f), onBack, 12f);
        back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(120f, 24f));
        return panel.gameObject;
    }
}
