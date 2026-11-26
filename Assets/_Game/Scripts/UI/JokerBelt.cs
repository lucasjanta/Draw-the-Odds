using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cinto de coringas: ícones pequenos logo abaixo do painel do player (canto superior esquerdo).
// Tocar/clicar num ícone mostra o nome e o efeito por alguns segundos (no celular não existe "hover").
public class JokerBelt : MonoBehaviour
{
    private const float Icon = 18f, Step = 21f;

    private GameManager gm;
    private RectTransform row;
    private TextMeshProUGUI info;
    private CanvasGroup infoGroup;
    private float infoUntil;
    private readonly List<GameObject> icons = new List<GameObject>();

    public static JokerBelt Create(Transform parent, GameManager gm)
    {
        var root = UIFactory.Rect("JokerBelt", parent).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -68f), new Vector2(300f, Icon));
        var belt = root.gameObject.AddComponent<JokerBelt>();
        belt.gm = gm;
        belt.row = root;

        var back = UIFactory.Image("Info", root, UIFactory.PanelColor);
        back.raycastTarget = false;
        back.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -Icon - 4f), new Vector2(240f, 30f));
        belt.infoGroup = back.gameObject.AddComponent<CanvasGroup>();
        belt.infoGroup.alpha = 0f;
        belt.infoGroup.blocksRaycasts = false;
        belt.info = UIFactory.Text("Text", back.transform, "", 8f, Color.white, TextAlignmentOptions.MidlineLeft).NoLoc();
        belt.info.textWrappingMode = TextWrappingModes.Normal;
        belt.info.rectTransform.Stretch(5f);

        Jokers.OnChanged += belt.Rebuild;
        belt.Rebuild();
        return belt;
    }

    private void OnDestroy() => Jokers.OnChanged -= Rebuild;

    private void Rebuild()
    {
        if (this == null) return;
        foreach (var g in icons) Destroy(g);
        icons.Clear();
        for (int i = 0; i < Jokers.Belt.Count; i++)
        {
            var def = JokerCatalog.Get(Jokers.Belt[i]);
            var button = UIFactory.Button("Slot" + i, row, "", new Vector2(Icon, Icon), () => ShowInfo(def), 6f);
            button.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            button.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * Step, 0f), new Vector2(Icon, Icon));
            JokerIcon.Create(button.transform, def, Icon).Stretch();
            icons.Add(button.gameObject);
        }
    }

    private void ShowInfo(JokerDef def)
    {
        info.text = $"<color=#{ColorUtility.ToHtmlStringRGB(def.color)}>{def.Name}</color>\n{def.Desc}";
        infoUntil = Time.unscaledTime + 2.8f;
    }

    private void Update()
    {
        // Só durante a luta (no menu e nas telas de escolha some)
        bool show = gm.State == GameManager.GameState.Playing || gm.State == GameManager.GameState.Transition;
        row.gameObject.SetActive(true);
        foreach (var g in icons) if (g.activeSelf != show) g.SetActive(show);
        infoGroup.alpha = Mathf.MoveTowards(infoGroup.alpha, show && Time.unscaledTime < infoUntil ? 1f : 0f, Time.unscaledDeltaTime * 5f);
    }
}
