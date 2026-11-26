using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Painel da fusão, no meio da tela: [Q] op [E]. Sem preview: ao confirmar, as duas cartas
// se juntam no centro, piscam e revelam o resultado (naipes diferentes = o naipe "gira" até parar).
public class FusionPanel : MonoBehaviour
{
    private const float SlotScale = 1f;  // 58×80 já é grande; escala inteira mantém a pixel art nítida
    private const float SlotX = 72f;
    private const float PanelY = 62f;    // centro do painel acima do meio da tela (a mão fica embaixo)

    private GameManager gm;
    private CanvasGroup group;
    private RectTransform root;
    private TextMeshProUGUI title, opText, errorText, help, preview;
    private Image timerFill, flash;
    private CardView slotA, slotB, result;
    private GameObject frameA, frameB;
    private TextMeshProUGUI[] keyA, keyB;  // "Q"/"E" no teclado, "1"/"2" no toque
    private GameObject[] buttons;
    private float errorTimer;
    private Coroutine resolveRoutine;

    public static FusionPanel Create(Transform parent, GameManager gm)
    {
        var rt = UIFactory.Rect("FusionPanel", parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, PanelY), new Vector2(320f, 212f));
        var p = rt.gameObject.AddComponent<FusionPanel>();
        p.gm = gm;
        p.root = rt;
        p.group = rt.gameObject.AddComponent<CanvasGroup>();
        p.group.alpha = 0f;

        var back = UIFactory.Image("Back", rt, UIFactory.PanelColor);
        back.rectTransform.Stretch();

        p.title = UIFactory.Text("Title", rt, "", 18f, Color.white, TextAlignmentOptions.Center);
        p.title.fontStyle = FontStyles.Bold;
        p.title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(300f, 22f));

        UIFactory.Bar(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(280f, 4f),
            Color.white, out var timer, out var ghost);
        ghost.gameObject.SetActive(false);
        p.timerFill = timer.GetComponent<Image>();

        p.slotA = MakeSlot(rt, gm, -SlotX, "Q", new Color32(0xFF, 0xA5, 0x2E, 0xFF), out p.frameA, out p.keyA);
        p.slotB = MakeSlot(rt, gm, SlotX, "E", new Color32(0xFF, 0x6E, 0xC7, 0xFF), out p.frameB, out p.keyB);
        FusionSlotTarget.Attach(p.frameA, true);
        FusionSlotTarget.Attach(p.frameB, false);

        p.opText = UIFactory.Text("Op", rt, "", 34f, Color.white, TextAlignmentOptions.Center);
        p.opText.fontStyle = FontStyles.Bold;
        p.opText.Outline(2f); // contorno preto no + − ×
        p.opText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(60f, 40f));

        // EASY: resultado previsto embaixo do símbolo (ex.: "= 12 ♣")
        p.preview = UIFactory.Text("Preview", rt, "", 14f, Color.white, TextAlignmentOptions.Center);
        p.preview.fontStyle = FontStyles.Bold;
        p.preview.Outline(1f);
        p.preview.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(84f, 18f));

        p.result = CardView.Create(rt, gm.Config, false);
        p.result.Rect.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(CardView.Width, CardView.Height));
        p.result.gameObject.SetActive(false);

        p.flash = UIFactory.Image("Flash", rt, new Color(1f, 1f, 1f, 0f));
        p.flash.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(150f, 150f));
        p.flash.sprite = Fx.Circle;

        p.errorText = UIFactory.Text("Error", rt, "", 11f, new Color32(0xFF, 0x5A, 0x5A, 0xFF), TextAlignmentOptions.Center);
        p.errorText.fontStyle = FontStyles.Bold;
        p.errorText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(300f, 14f));

        p.help = UIFactory.Text("Help", rt, HelpText(), 8f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center).NoLoc();
        Loc.OnChanged += () => { if (p != null) p.help.text = HelpText(); };
        VirtualInput.OnModeChanged += () => { if (p != null) p.help.text = HelpText(); };
        KeyBindings.OnChanged += () => { if (p != null) p.help.text = HelpText(); };
        p.help.lineSpacing = 4f;
        p.help.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(300f, 24f));

        // Botões MERGE / SKIP (toque e mouse). No PC o clique em qualquer lugar continua valendo.
        var skip = UIFactory.Button("Skip", rt, "SKIP", new Vector2(90f, 20f), () => gm.Fusion.Skip(), 10f);
        skip.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-56f, 6f), new Vector2(90f, 20f));
        var merge = UIFactory.Button("Merge", rt, "MERGE", new Vector2(90f, 20f), () => gm.Fusion.TryConfirm(), 10f);
        merge.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(56f, 6f), new Vector2(90f, 20f));
        p.buttons = new[] { skip.gameObject, merge.gameObject };

        gm.Fusion.OnStarted += p.HandleStarted;
        gm.Fusion.OnChanged += p.Refresh;
        gm.Hand.OnSelectionChanged += p.Refresh;
        gm.Fusion.OnRejected += p.ShowError;
        gm.Fusion.OnResolved += p.HandleResolved;
        gm.Fusion.OnEnded += p.HandleEnded;
        return p;
    }

    private static string Y(string s) => $"<color=#FFD447>{s}</color>";

    private static string HelpText()
    {
        if (VirtualInput.Active)
            return Loc.Pick($"{Y("DRAG")} 2 cards into the slots\n(or tap a card, then a slot)",
                            $"{Y("ARRASTE")} 2 cartas para os espaços\n(ou toque na carta e depois no espaço)",
                            $"{Y("ARRASTRA")} 2 cartas a los huecos\n(o toca una carta y luego un hueco)",
                            $"{Y("GLISSE")} 2 cartes dans les emplacements\n(ou touche une carte, puis un emplacement)",
                            $"{Y("ZIEH")} 2 Karten in die Plätze\n(oder tippe eine Karte, dann einen Platz)",
                            $"{Y("TRASCINA")} 2 carte negli slot\n(o tocca una carta e poi uno slot)");
        string qe = Y(KeyBindings.Name(GameKey.SlotA)) + "/" + Y(KeyBindings.Name(GameKey.SlotB));
        return Loc.Pick(
            $"{Y("SCROLL")} pick  {qe} place (or drag)\n{Y("L-CLICK")} merge  {Y("R-CLICK")} skip",
            $"{Y("RODINHA")} escolhe  {qe} coloca (ou arraste)\n{Y("CLIQUE ESQ.")} funde  {Y("CLIQUE DIR.")} pula",
            $"{Y("RUEDA")} elige  {qe} coloca (o arrastra)\n{Y("CLIC IZQ.")} fusiona  {Y("CLIC DER.")} salta",
            $"{Y("MOLETTE")} choisir  {qe} placer (ou glisser)\n{Y("CLIC G.")} fusionner  {Y("CLIC D.")} passer",
            $"{Y("MAUSRAD")} wählen  {qe} ablegen (oder ziehen)\n{Y("LINKSKLICK")} verschmelzen  {Y("RECHTSKLICK")} überspringen",
            $"{Y("ROTELLA")} scegli  {qe} metti (o trascina)\n{Y("CLIC SX")} fondi  {Y("CLIC DX")} salta");
    }

    private static CardView MakeSlot(RectTransform parent, GameManager gm, float x, string key, Color keyColor, out GameObject frameObject, out TextMeshProUGUI[] keyTexts)
    {
        var frame = UIFactory.Image("Frame " + key, parent, new Color(1f, 1f, 1f, 0.1f));
        frame.raycastTarget = true; // recebe a carta arrastada / o toque
        frame.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -4f),
            new Vector2(CardView.Width, CardView.Height) * SlotScale + Vector2.one * 6f);
        var ghostKey = UIFactory.Text("Ghost", frame.transform, key, 30f, new Color(keyColor.r, keyColor.g, keyColor.b, 0.35f), TextAlignmentOptions.Center);
        ghostKey.fontStyle = FontStyles.Bold;
        ghostKey.rectTransform.Stretch();
        var label = UIFactory.Text("Key", frame.transform, key, 14f, keyColor, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        // Tecla/número EM CIMA do espaço (embaixo ficava encostado no texto de ajuda)
        label.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(30f, 16f));
        frameObject = frame.gameObject;
        keyTexts = new[] { ghostKey.NoLoc(), label.NoLoc() };

        var view = CardView.Create(parent, gm.Config, false);
        view.Rect.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -4f), new Vector2(CardView.Width, CardView.Height));
        view.targetScale = SlotScale;
        view.gameObject.SetActive(false);
        return view;
    }

    private void HandleStarted()
    {
        if (resolveRoutine != null) StopCoroutine(resolveRoutine);
        ResetLayout();
        Refresh();
    }

    private void ResetLayout()
    {
        slotA.Rect.anchoredPosition = new Vector2(-SlotX, -4f);
        slotB.Rect.anchoredPosition = new Vector2(SlotX, -4f);
        slotA.Rect.localRotation = slotB.Rect.localRotation = Quaternion.identity;
        slotA.targetScale = slotB.targetScale = SlotScale;
        frameA.SetActive(true);
        frameB.SetActive(true);
        opText.alpha = 1f;
        help.alpha = 1f;
        preview.alpha = 1f;
        foreach (var b in buttons) b.SetActive(true);
        result.gameObject.SetActive(false);
        flash.color = new Color(1f, 1f, 1f, 0f);
        errorTimer = 0f;
    }

    private void Refresh()
    {
        var f = gm.Fusion;
        if (!f.AcceptsInput) return;
        Color c = FusionRules.GetColor(f.Type);
        title.text = FusionRules.Title(f.Type);
        foreach (var t in keyA) t.text = VirtualInput.Active ? "1" : KeyBindings.Name(GameKey.SlotA);
        foreach (var t in keyB) t.text = VirtualInput.Active ? "2" : KeyBindings.Name(GameKey.SlotB);
        title.color = c;
        opText.text = FusionRules.Symbol(f.Type);
        opText.color = c;
        timerFill.color = c;

        ShowCard(slotA, f.SlotA >= 0 ? gm.Hand[f.SlotA] : (Card?)null);
        ShowCard(slotB, f.SlotB >= 0 ? gm.Hand[f.SlotB] : (Card?)null);

        preview.text = "";
        if (f.ShowsPreview && f.BothSlotsFilled)
        {
            if (f.TryPreview(out Card r, out string error))
            {
                // Número branco + naipe na cor dele clareada (♣ e ♠ são escuros demais no painel)
                Color suitColor = Color.Lerp(SuitInfo.GetColor(r.suit), Color.white, 0.35f);
                preview.text = $"= {r.value} <color=#{ColorUtility.ToHtmlStringRGB(suitColor)}>{SuitInfo.Symbol(r.suit)}</color>";
            }
            else
                preview.text = "<color=#FF5A5A>" + (error == "THAT MAKES ZERO!" ? "= 0" : error) + "</color>";
        }
    }

    private static void ShowCard(CardView view, Card? card)
    {
        view.gameObject.SetActive(card.HasValue);
        if (card.HasValue) { view.SetCard(card.Value); view.Punch(); }
    }

    private void ShowError(string message)
    {
        errorText.text = message;
        errorTimer = 1.4f;
    }

    private void HandleResolved(Card a, Card b, Card fused)
    {
        resolveRoutine = StartCoroutine(MergeAnimation(a, b, fused));
    }

    // A animação toda roda em tempo real (o jogo está parado).
    private IEnumerator MergeAnimation(Card a, Card b, Card fused)
    {
        slotA.SetCard(a); slotB.SetCard(b);
        slotA.gameObject.SetActive(true); slotB.gameObject.SetActive(true);
        frameA.SetActive(false); frameB.SetActive(false);

        // 1) As duas cartas vão para o centro
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.35f);
            slotA.Rect.anchoredPosition = new Vector2(Mathf.Lerp(-SlotX, -16f, k), -4f);
            slotB.Rect.anchoredPosition = new Vector2(Mathf.Lerp(SlotX, 16f, k), -4f);
            slotA.Rect.localRotation = Quaternion.Euler(0f, 0f, 10f * k);
            slotB.Rect.localRotation = Quaternion.Euler(0f, 0f, -10f * k);
            opText.alpha = 1f - k;
            help.alpha = 1f - k;
            foreach (var button in buttons) button.SetActive(false);
            preview.alpha = 1f - k;
            yield return null;
        }

        // 2) Tremem e crescem
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.35f;
            Vector2 shake = Random.insideUnitCircle * 4f * k;
            slotA.Rect.anchoredPosition = new Vector2(-16f, -4f) + shake;
            slotB.Rect.anchoredPosition = new Vector2(16f, -4f) - shake;
            slotA.targetScale = slotB.targetScale = Mathf.Lerp(SlotScale, SlotScale + 0.25f, k);
            yield return null;
        }

        // 3) Flash e o resultado aparece
        slotA.gameObject.SetActive(false);
        slotB.gameObject.SetActive(false);
        result.gameObject.SetActive(true);
        result.SetCard(fused);
        Fx.Shake(0.25f, 0.2f);
        AudioManager.Sfx(gm.Config.SfxFusion); // som da carta recebida: na hora em que ela aparece

        bool mixed = a.suit != b.suit;
        float spinUntil = mixed ? 0.9f : 0f;
        float suitTimer = 0f;
        bool showA = true;
        for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
        {
            flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - t / 0.35f));
            flash.rectTransform.localScale = Vector3.one * (0.6f + t * 2f);
            float pop = t < 0.15f ? Mathf.Lerp(0.2f, 2.1f, t / 0.15f) : Mathf.Lerp(2.1f, 1.8f, (t - 0.15f) / 0.2f);
            result.targetScale = pop;

            // Naipes diferentes: o naipe alterna cada vez mais devagar até parar no sorteado.
            if (t < spinUntil)
            {
                suitTimer -= Time.unscaledDeltaTime;
                if (suitTimer <= 0f)
                {
                    showA = !showA;
                    result.SetCard(new Card(showA ? a.suit : b.suit, fused.value));
                    suitTimer = Mathf.Lerp(0.05f, 0.22f, t / spinUntil);
                }
            }
            else if (mixed && spinUntil > 0f)
            {
                result.SetCard(fused);
                result.Punch();
                spinUntil = 0f;
            }
            yield return null;
        }

        // 4) Some e o tempo volta
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.25f;
            yield return null;
        }
        resolveRoutine = null;
        gm.Fusion.FinishResolve();
    }

    private void HandleEnded(bool success)
    {
        if (resolveRoutine != null) { StopCoroutine(resolveRoutine); resolveRoutine = null; }
        var player = gm.Player.transform.position;
        if (success)
        {
            Fx.Popup("MERGED! " + gm.Hand.Selected, player + Vector3.up * 2.6f, FusionRules.GetColor(gm.Fusion.Type), 7f, 1.2f);
            Fx.Burst(player + Vector3.up, Color.white, 16, 8f, 0.25f);
        }
        else if (gm.State == GameManager.GameState.Playing)
        {
            // Sobrou tempo = o jogador pulou o poder (botão direito)
            string msg = gm.Fusion.TimeLeft > 0f ? "SKIPPED" : "TIME UP";
            Fx.Popup(msg, player + Vector3.up * 2.6f, new Color(1f, 0.5f, 0.5f), 6f, 1f);
        }
    }

    private void Update()
    {
        var f = gm.Fusion;
        // Invisível = não bloqueia toques/cliques no meio da tela.
        group.blocksRaycasts = group.interactable = f.AcceptsInput;
        if (f.AcceptsInput)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime * 8f);
            bool noClock = f.Unlimited || f.NoTimer;
            UIFactory.SetBar(timerFill.rectTransform, noClock ? 1f : f.TimeLeft / f.Duration);
            timerFill.transform.parent.gameObject.SetActive(!noClock); // EASY: sem relógio, sem barra
        }
        else if (!f.Resolving)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime * 6f);
        }

        errorTimer -= Time.unscaledDeltaTime;
        errorText.gameObject.SetActive(errorTimer > 0f);
        Vector2 basePos = new Vector2(0f, PanelY);
        root.anchoredPosition = basePos + (errorTimer > 1.15f ? Random.insideUnitCircle * 3f : Vector2.zero);
    }
}
