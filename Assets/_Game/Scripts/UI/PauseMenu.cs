using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Menu de pausa (ESC ou P): Resume, Options, Skip Tutorial (só no tutorial) e Main Menu.
// Quem pausa/despausa de verdade é o GameManager; aqui é só a tela.
public class PauseMenu : MonoBehaviour
{
    private GameManager gm;
    private GameObject mainPanel;
    private Button skipTutorial;
    private OptionsPanel options;

    public bool OptionsOpen => options.gameObject.activeSelf;

    public static PauseMenu Create(Transform parent, GameManager gm)
    {
        // Fundo escurecendo o jogo inteiro (e bloqueando cliques)
        var dim = UIFactory.Image("PauseMenu", parent, new Color(0f, 0f, 0f, 0.75f));
        dim.raycastTarget = true;
        dim.rectTransform.Stretch();
        var menu = dim.gameObject.AddComponent<PauseMenu>();
        menu.gm = gm;

        var panel = UIFactory.MenuPanel("Main", dim.transform, new Vector2(220f, 182f));
        menu.mainPanel = panel.gameObject;

        var title = UIFactory.Text("Title", panel.transform, "PAUSED", 22f, UIFactory.Gold, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(200f, 28f));

        var size = new Vector2(160f, 24f);
        menu.AddButton(panel.transform, "RESUME", -46f, size, gm.ResumeGame);
        menu.AddButton(panel.transform, "OPTIONS", -76f, size, () => menu.ShowOptions(true));
        menu.skipTutorial = menu.AddButton(panel.transform, "SKIP TUTORIAL", -106f, size, gm.SkipTutorialFromPause);
        menu.AddButton(panel.transform, "MAIN MENU", -136f, size, gm.BackToMainMenu);

        menu.options = OptionsPanel.Create(dim.transform, () => menu.ShowOptions(false));
        dim.gameObject.SetActive(false);
        return menu;
    }

    private Button AddButton(Transform parent, string label, float y, Vector2 size, System.Action onClick)
    {
        var b = UIFactory.Button(label, parent, label, size, onClick, 12f);
        b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), size);
        return b;
    }

    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // por cima de tudo
        skipTutorial.interactable = gm.IsTutorial;
        skipTutorial.transform.Find("Label").GetComponent<TextMeshProUGUI>().alpha = gm.IsTutorial ? 1f : 0.35f;
        ShowOptions(false);
    }

    public void Close() => gameObject.SetActive(false);

    public void ShowOptions(bool show)
    {
        mainPanel.SetActive(!show);
        options.Show(show);
    }
}
