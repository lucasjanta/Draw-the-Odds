using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// RAISE / TRUCO na tela:
//  - uma ficha dourada embaixo do painel do chefe, que SÓ aparece quando o RAISE está disponível
//    (1x por luta) e some depois de usada: pouca coisa na tela do celular;
//  - quando armado, a ficha vira um aviso piscando "x2" (a próxima carta vale a aposta);
//  - RE-RAISE do chefe: tempo parado e dois botões grandes no centro (CALL / FOLD).
public class RaiseUi : MonoBehaviour
{
    private static readonly Color Gold = new Color32(0xFF, 0xD4, 0x47, 0xFF);
    private const float Size = 40f;

    private GameManager gm;
    private RectTransform chip;
    private Image chipFace;
    private TextMeshProUGUI chipLabel, keyLabel;
    private GameObject modal;
    private TextMeshProUGUI modalTitle, modalBody, callLabel, foldLabel;
    private Action<bool> onAnswer;
    private float modalOpenTime;

    public static RaiseUi Create(Transform parent, GameManager gm)
    {
        var root = UIFactory.Rect("RaiseUi", parent).Stretch();
        var ui = root.gameObject.AddComponent<RaiseUi>();
        ui.gm = gm;

        // Ficha (botão redondo) dentro da área segura, embaixo do painel do chefe
        var safe = UIFactory.Rect("Safe", root).Stretch();
        SafeArea.Add(safe);
        var outline = UIFactory.Image("RaiseChip", safe, UIFactory.OutlineColor, Fx.Circle);
        outline.raycastTarget = true;
        outline.rectTransform.Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-34f, -96f), new Vector2(Size, Size));
        ui.chip = outline.rectTransform;
        ui.chipFace = UIFactory.Image("Face", outline.transform, Gold, Fx.Circle);
        ui.chipFace.rectTransform.Stretch(2f);
        // "Listras" de ficha de pôquer: anel interno escuro
        var inner = UIFactory.Image("Inner", outline.transform, new Color(0.55f, 0.32f, 0.05f, 1f), Fx.Circle);
        inner.rectTransform.Stretch(7f);
        inner.raycastTarget = false;
        var innerFace = UIFactory.Image("InnerFace", outline.transform, Gold, Fx.Circle);
        innerFace.rectTransform.Stretch(9f);
        innerFace.raycastTarget = false;
        ui.chipLabel = UIFactory.Text("Label", outline.transform, "", 7f, new Color32(0x3A, 0x1E, 0x04, 0xFF), TextAlignmentOptions.Center).NoLoc();
        ui.chipLabel.fontStyle = FontStyles.Bold;
        ui.chipLabel.rectTransform.Stretch();
        ui.keyLabel = UIFactory.Text("Key", outline.transform, "", 7f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center).NoLoc();
        ui.keyLabel.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(60f, 10f));
        var button = outline.gameObject.AddComponent<Button>();
        button.targetGraphic = ui.chipFace;
        button.onClick.AddListener(gm.TryRaise);
        outline.gameObject.AddComponent<ButtonJuice>();

        ui.BuildModal(root);
        return ui;
    }

    private void BuildModal(Transform root)
    {
        var dim = UIFactory.Image("ReRaise", root, new Color(0.08f, 0.01f, 0.02f, 0.75f));
        dim.raycastTarget = true;
        dim.rectTransform.Stretch();
        modal = dim.gameObject;

        modalTitle = UIFactory.Text("Title", dim.transform, "", 30f, new Color32(0xFF, 0x5A, 0x4A, 0xFF), TextAlignmentOptions.Center).NoLoc();
        modalTitle.Outline(2f);
        modalTitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 78f), new Vector2(600f, 40f));
        modalBody = UIFactory.Text("Body", dim.transform, "", 10f, Color.white, TextAlignmentOptions.Center).NoLoc();
        modalBody.lineSpacing = 6f;
        modalBody.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(560f, 56f));

        // Botões grandes e do mesmo tamanho (dedo acerta fácil)
        var size = new Vector2(170f, 40f);
        var call = UIFactory.Button("Call", dim.transform, "", size, () => Answer(true), 16f);
        call.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-96f, -40f), size);
        callLabel = call.transform.Find("Label").GetComponent<TextMeshProUGUI>().NoLoc();
        var fold = UIFactory.Button("Fold", dim.transform, "", size, () => Answer(false), 16f);
        fold.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(96f, -40f), size);
        foldLabel = fold.transform.Find("Label").GetComponent<TextMeshProUGUI>().NoLoc();
        modal.SetActive(false);
    }

    // RE-RAISE: abre a escolha (o GameManager já parou o tempo).
    public void Ask(Action<bool> onAnswer)
    {
        this.onAnswer = onAnswer;
        modalTitle.text = RaiseController.ReRaiseWord;
        int call = LuckyMath.RaiseMultiplier(2);
        modalBody.text = Loc.Pick(
            $"The boss doubles the bet!\n<color=#FFD447>{RaiseController.CallWord}</color>: x{call} damage if close, but you take 2 if you miss badly (boss gets faster)\n<color=#A0A0B8>{RaiseController.FoldWord}</color>: give up the bet",
            $"O chefe dobrou a aposta!\n<color=#FFD447>{RaiseController.CallWord}</color>: x{call} de dano se chegar perto, mas leva 2 se errar feio (o chefe acelera)\n<color=#A0A0B8>{RaiseController.FoldWord}</color>: desiste da aposta",
            $"¡El jefe dobla la apuesta!\n<color=#FFD447>{RaiseController.CallWord}</color>: x{call} de daño si te acercas, pero recibes 2 si fallas mucho (el jefe acelera)\n<color=#A0A0B8>{RaiseController.FoldWord}</color>: renuncias a la apuesta",
            $"Le boss double la mise !\n<color=#FFD447>{RaiseController.CallWord}</color> : x{call} dégâts si proche, mais tu prends 2 si tu rates de loin (le boss accélère)\n<color=#A0A0B8>{RaiseController.FoldWord}</color> : abandonner la mise",
            $"Der Boss verdoppelt!\n<color=#FFD447>{RaiseController.CallWord}</color>: x{call} Schaden wenn nah, aber 2 Schaden bei großem Fehlwurf (Boss wird schneller)\n<color=#A0A0B8>{RaiseController.FoldWord}</color>: Einsatz aufgeben",
            $"Il boss raddoppia la posta!\n<color=#FFD447>{RaiseController.CallWord}</color>: x{call} danni se vicino, ma ne subisci 2 se sbagli di molto (il boss accelera)\n<color=#A0A0B8>{RaiseController.FoldWord}</color>: rinunci alla puntata");
        callLabel.text = RaiseController.CallWord;
        foldLabel.text = RaiseController.FoldWord;
        modal.SetActive(true);
        transform.SetAsLastSibling();
        modalOpenTime = Time.unscaledTime;
    }

    private void Answer(bool call)
    {
        if (onAnswer == null || Time.unscaledTime < modalOpenTime + 0.3f) return;
        AudioManager.Ui(gm.Config.SfxUiClick);
        var cb = onAnswer;
        onAnswer = null;
        modal.SetActive(false);
        cb(call);
    }

    private void Update()
    {
        // Ficha só aparece durante a luta, sem fusão/pausa/diálogo, e com RAISE disponível ou armado
        bool fighting = gm.State == GameManager.GameState.Playing && !gm.Fusion.Active && !GameTime.Paused && !gm.InputLocked;
        bool show = fighting && (gm.Raise.Available || gm.Raise.Armed);
        if (chip.gameObject.activeSelf != show) chip.gameObject.SetActive(show);
        if (!show) return;
        transform.SetAsLastSibling();

        float t = Time.unscaledTime;
        if (gm.Raise.Armed)
        {
            // Armado: pisca e mostra o multiplicador
            chipLabel.text = $"x{LuckyMath.RaiseMultiplier(gm.Raise.Level)}";
            chipLabel.fontSize = 12f;
            chipFace.color = Color.Lerp(Gold, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 10f));
            keyLabel.text = Loc.Pick("NEXT CARD", "PRÓXIMA CARTA", "PRÓXIMA CARTA", "PROCHAINE CARTE", "NÄCHSTE KARTE", "PROSSIMA CARTA");
            chip.localScale = Vector3.one;
        }
        else
        {
            chipLabel.text = RaiseController.ButtonWord;
            chipLabel.fontSize = 7f;
            chipFace.color = Gold;
            keyLabel.text = VirtualInput.Active ? "" : $"[{KeyBindings.Name(GameKey.Raise)}]";
            float pulse = 1f + 0.06f * Mathf.Sin(t * 4f);
            chip.localScale = new Vector3(pulse, pulse, 1f);
        }
    }

    // Debug (ponte): responde o RE-RAISE sem clicar. "call" | "fold"
    public static string DebugAnswer(string arg)
    {
        var ui = FindAnyObjectByType<RaiseUi>();
        if (ui == null || ui.onAnswer == null) return "no question";
        ui.modalOpenTime = -1f;
        ui.Answer(arg == "call");
        return arg;
    }
}
