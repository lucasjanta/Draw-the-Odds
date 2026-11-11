using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Painel da fusão (aparece ao pegar um poder): [Q] op [E] = resultado, com tempo restante.
public class FusionPanel : MonoBehaviour
{
    private const float SlotScale = 0.8f;

    private GameManager gm;
    private CanvasGroup group;
    private TextMeshProUGUI title, opText, eqText, errorText, help;
    private RectTransform timerFill;
    private CardView slotA, slotB, resultA, resultB;
    private float errorTimer;
    private Vector2 basePos;

    public static FusionPanel Create(Transform parent, GameManager gm)
    {
        var panel = UIFactory.Image("FusionPanel", parent, UIFactory.PanelColor);
        var rt = panel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -56f), new Vector2(236f, 122f));
        var p = panel.gameObject.AddComponent<FusionPanel>();
        p.gm = gm;
        p.basePos = rt.anchoredPosition;
        p.group = panel.gameObject.AddComponent<CanvasGroup>();
        p.group.alpha = 0f;

        p.title = UIFactory.Text("Title", rt, "", 12f, Color.white);
        p.title.fontStyle = FontStyles.Bold;
        p.title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(220f, 16f));

        UIFactory.Bar(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -21f), new Vector2(220f, 4f),
            Color.white, out p.timerFill, out var ghost);
        ghost.gameObject.SetActive(false);

        // Linha dos slots (centro em y = -62)
        p.slotA = MakeSlot(rt, gm, new Vector2(30f, -62f), "Q", new Color32(0xFF, 0xA5, 0x2E, 0xFF));
        p.opText = Label(rt, new Vector2(63f, -62f), 18f);
        p.slotB = MakeSlot(rt, gm, new Vector2(96f, -62f), "E", new Color32(0xFF, 0x6E, 0xC7, 0xFF));
        p.eqText = Label(rt, new Vector2(127f, -62f), 16f);
        p.eqText.text = "=";
        p.resultA = MakeSlot(rt, gm, new Vector2(160f, -62f), null, Color.white);
        p.resultB = MakeSlot(rt, gm, new Vector2(202f, -62f), null, Color.white);

        p.errorText = Label(rt, new Vector2(180f, -103f), 9f);
        p.errorText.color = new Color32(0xFF, 0x5A, 0x5A, 0xFF);

        p.help = UIFactory.Text("Help", rt, "", 8f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
        p.help.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(230f, 12f));

        gm.Fusion.OnStarted += p.Refresh;
        gm.Fusion.OnChanged += p.Refresh;
        gm.Hand.OnSelectionChanged += p.Refresh;
        gm.Fusion.OnRejected += p.ShowError;
        gm.Fusion.OnEnded += p.HandleEnded;
        return p;
    }

    private static TextMeshProUGUI Label(RectTransform parent, Vector2 pos, float size)
    {
        var t = UIFactory.Text("Label", parent, "", size, Color.white, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold;
        t.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), pos, new Vector2(60f, 20f));
        return t;
    }

    private static CardView MakeSlot(RectTransform parent, GameManager gm, Vector2 pos, string key, Color keyColor)
    {
        var frame = UIFactory.Image("SlotFrame", parent, new Color(1f, 1f, 1f, 0.12f));
        frame.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), pos,
            new Vector2(CardView.Width, CardView.Height) * SlotScale + Vector2.one * 4f);
        if (key != null)
        {
            var k = UIFactory.Text("Key", frame.transform, key, 10f, keyColor, TextAlignmentOptions.Center);
            k.fontStyle = FontStyles.Bold;
            k.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(30f, 12f));
            var hint = UIFactory.Text("Empty", frame.transform, key, 18f, new Color(keyColor.r, keyColor.g, keyColor.b, 0.35f), TextAlignmentOptions.Center);
            hint.rectTransform.Stretch();
        }
        var view = CardView.Create(frame.transform, gm.Config, false);
        view.Rect.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardView.Width, CardView.Height));
        view.targetScale = SlotScale;
        view.Rect.localScale = Vector3.one * SlotScale;
        view.gameObject.SetActive(false);
        return view;
    }

    private void Refresh()
    {
        var f = gm.Fusion;
        if (!f.Active) return;
        var hand = gm.Hand;
        Color c = FusionRules.GetColor(f.Type);
        title.text = $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{FusionRules.Symbol(f.Type)} {FusionRules.Title(f.Type)}</color>";
        opText.text = FusionRules.Symbol(f.Type);
        opText.color = c;
        timerFill.GetComponent<Image>().color = c;

        ShowCard(slotA, f.SlotA >= 0 ? hand[f.SlotA] : (Card?)null);
        ShowCard(slotB, f.SlotB >= 0 ? hand[f.SlotB] : (Card?)null);

        resultA.gameObject.SetActive(false);
        resultB.gameObject.SetActive(false);
        resultB.transform.parent.gameObject.SetActive(f.Type == PowerType.SwapSuit);

        bool canConfirm = false;
        if (f.TryGetPreview(out var o))
        {
            if (o.valid)
            {
                canConfirm = true;
                if (o.isSwap)
                {
                    ShowCard(resultA, o.swapA);
                    ShowCard(resultB, o.swapB);
                }
                else if (o.suitIsRandom)
                {
                    resultA.gameObject.SetActive(true);
                    resultA.SetMystery(o.value, o.suitA, o.suitB);
                }
                else ShowCard(resultA, new Card(o.suitA, o.value));
            }
            else ShowError(o.error);
        }

        help.text = canConfirm
            ? "<color=#FFD447>RIGHT CLICK TO FUSE!</color>"
            : "SCROLL pick  ·  Q / E put in slot";
    }

    private static void ShowCard(CardView view, Card? card)
    {
        view.gameObject.SetActive(card.HasValue);
        if (card.HasValue) { view.SetCard(card.Value); view.Punch(); }
    }

    private void ShowError(string message)
    {
        errorText.text = message;
        errorTimer = 1.2f;
    }

    private void HandleEnded(bool success)
    {
        if (success)
        {
            var player = gm.Player.transform.position;
            Fx.Popup("FUSED! " + gm.Hand.Selected, player + Vector3.up * 2.6f, FusionRules.GetColor(gm.Fusion.Type), 7f, 1.2f);
            Fx.Burst(player + Vector3.up, Color.white, 16, 8f, 0.25f);
        }
        else if (gm.State == GameManager.GameState.Playing)
        {
            Fx.Popup("TIME UP", gm.Player.transform.position + Vector3.up * 2.6f, new Color(1f, 0.5f, 0.5f), 6f, 1f);
        }
    }

    private void Update()
    {
        var f = gm.Fusion;
        group.alpha = Mathf.MoveTowards(group.alpha, f.Active ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        if (f.Active) UIFactory.SetBar(timerFill, f.TimeLeft / f.Duration);

        errorTimer -= Time.unscaledDeltaTime;
        errorText.gameObject.SetActive(errorTimer > 0f);
        // Tremidinha quando dá erro
        var rt = (RectTransform)transform;
        rt.anchoredPosition = basePos + (errorTimer > 0.9f ? Random.insideUnitCircle * 2f : Vector2.zero);
    }
}
