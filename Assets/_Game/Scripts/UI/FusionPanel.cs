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

    private GameManager gm;
    private CanvasGroup group;
    private RectTransform root;
    private TextMeshProUGUI title, opText, errorText, help;
    private Image timerFill, flash;
    private CardView slotA, slotB, result;
    private GameObject frameA, frameB;
    private float errorTimer;
    private Coroutine resolveRoutine;

    public static FusionPanel Create(Transform parent, GameManager gm)
    {
        var rt = UIFactory.Rect("FusionPanel", parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 56f), new Vector2(320f, 190f));
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

        p.slotA = MakeSlot(rt, gm, -SlotX, "Q", new Color32(0xFF, 0xA5, 0x2E, 0xFF), out p.frameA);
        p.slotB = MakeSlot(rt, gm, SlotX, "E", new Color32(0xFF, 0x6E, 0xC7, 0xFF), out p.frameB);

        p.opText = UIFactory.Text("Op", rt, "", 34f, Color.white, TextAlignmentOptions.Center);
        p.opText.fontStyle = FontStyles.Bold;
        p.opText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(60f, 40f));

        p.result = CardView.Create(rt, gm.Config, false);
        p.result.Rect.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(CardView.Width, CardView.Height));
        p.result.gameObject.SetActive(false);

        p.flash = UIFactory.Image("Flash", rt, new Color(1f, 1f, 1f, 0f));
        p.flash.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(150f, 150f));
        p.flash.sprite = Fx.Circle;

        p.errorText = UIFactory.Text("Error", rt, "", 11f, new Color32(0xFF, 0x5A, 0x5A, 0xFF), TextAlignmentOptions.Center);
        p.errorText.fontStyle = FontStyles.Bold;
        p.errorText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(300f, 14f));

        p.help = UIFactory.Text("Help", rt,
            "<color=#FFD447>SCROLL</color> pick   <color=#FFD447>Q</color> / <color=#FFD447>E</color> place   <color=#FFD447>LEFT CLICK</color> merge   <color=#FFD447>RIGHT CLICK</color> skip",
            10f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
        p.help.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(300f, 14f));

        gm.Fusion.OnStarted += p.HandleStarted;
        gm.Fusion.OnChanged += p.Refresh;
        gm.Hand.OnSelectionChanged += p.Refresh;
        gm.Fusion.OnRejected += p.ShowError;
        gm.Fusion.OnResolved += p.HandleResolved;
        gm.Fusion.OnEnded += p.HandleEnded;
        return p;
    }

    private static CardView MakeSlot(RectTransform parent, GameManager gm, float x, string key, Color keyColor, out GameObject frameObject)
    {
        var frame = UIFactory.Image("Frame " + key, parent, new Color(1f, 1f, 1f, 0.1f));
        frame.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -4f),
            new Vector2(CardView.Width, CardView.Height) * SlotScale + Vector2.one * 6f);
        var ghostKey = UIFactory.Text("Ghost", frame.transform, key, 30f, new Color(keyColor.r, keyColor.g, keyColor.b, 0.35f), TextAlignmentOptions.Center);
        ghostKey.fontStyle = FontStyles.Bold;
        ghostKey.rectTransform.Stretch();
        var label = UIFactory.Text("Key", frame.transform, key, 14f, keyColor, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(30f, 16f));
        frameObject = frame.gameObject;

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
        title.color = c;
        opText.text = FusionRules.Symbol(f.Type);
        opText.color = c;
        timerFill.color = c;

        ShowCard(slotA, f.SlotA >= 0 ? gm.Hand[f.SlotA] : (Card?)null);
        ShowCard(slotB, f.SlotB >= 0 ? gm.Hand[f.SlotB] : (Card?)null);
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
        if (f.AcceptsInput)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime * 8f);
            UIFactory.SetBar(timerFill.rectTransform, f.Unlimited ? 1f : f.TimeLeft / f.Duration);
        }
        else if (!f.Resolving)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime * 6f);
        }

        errorTimer -= Time.unscaledDeltaTime;
        errorText.gameObject.SetActive(errorTimer > 0f);
        Vector2 basePos = new Vector2(0f, 56f);
        root.anchoredPosition = basePos + (errorTimer > 1.15f ? Random.insideUnitCircle * 3f : Vector2.zero);
    }
}
