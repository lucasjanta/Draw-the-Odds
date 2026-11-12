using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Painel do ranking global (top 10). Dois usos:
//  - fim do jogo (vitória): mostra a run do jogador e, se ela entra no top 10, pede o nome e envia;
//  - menu principal (botão RANKING): só mostra a lista.
public class LeaderboardPanel : MonoBehaviour
{
    private static readonly float[] ColumnX = { 14f, 36f, 178f, 238f, 294f, 350f, 406f, 462f, 520f };
    private static readonly string[] Headers = { "#", "NAME", "SCORE", "TIME", "CARDS", "CRITS", "FUSES", "DMG", "DEATHS" };
    private const int Columns = 9;
    private static readonly Color Highlight = new Color32(0xFF, 0xD4, 0x47, 0xFF);

    private TextMeshProUGUI title, runLine, status;
    private TextMeshProUGUI[,] cells;
    private GameObject nameRow;
    private TMP_InputField nameInput;
    private Button submitButton;
    private LeaderboardClient.Entry run;     // run do jogador (null = só visualizar)
    private string highlightName;

    public static LeaderboardPanel Create(Transform parent, string closeLabel, Action onClose)
    {
        var panel = UIFactory.MenuPanel("Leaderboard", parent, new Vector2(600f, 296f));
        var p = panel.gameObject.AddComponent<LeaderboardPanel>();
        Transform t = panel.transform;

        p.title = UIFactory.Text("Title", t, "TOP 10", 16f, UIFactory.Gold, TextAlignmentOptions.Center);
        p.title.fontStyle = FontStyles.Bold;
        p.title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(580f, 20f));

        p.runLine = UIFactory.Text("Run", t, "", 9f, Color.white, TextAlignmentOptions.Center);
        p.runLine.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(580f, 14f));

        // Linha do nome: campo de texto + botão de enviar
        var row = UIFactory.Rect("NameRow", t).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(300f, 18f));
        p.nameRow = row.gameObject;
        p.nameInput = BuildInput(row);
        p.nameInput.GetComponent<RectTransform>().Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(190f, 18f));
        p.submitButton = UIFactory.Button("Submit", row, "SUBMIT", new Vector2(100f, 18f), p.SubmitClicked, 10f);
        p.submitButton.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(100f, 18f));

        p.status = UIFactory.Text("Status", t, "", 8f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
        p.status.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(580f, 12f));

        // Tabela: cabeçalho + 10 linhas
        p.cells = new TextMeshProUGUI[LeaderboardClient.Size + 1, Columns];
        for (int r = 0; r <= LeaderboardClient.Size; r++)
        {
            float y = -84f - r * 15f;
            for (int c = 0; c < Columns; c++)
            {
                Color color = r == 0 ? new Color(0.6f, 0.6f, 0.8f) : Color.white;
                var cell = UIFactory.Text($"Cell{r}_{c}", t, r == 0 ? Headers[c] : "", 8f, color);
                cell.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColumnX[c], y), new Vector2(140f, 12f));
                p.cells[r, c] = cell;
            }
        }

        var close = UIFactory.Button("Close", t, closeLabel, new Vector2(140f, 22f), onClose, 11f);
        close.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(140f, 22f));

        panel.gameObject.SetActive(false);
        return p;
    }

    // Campo de texto do TMP montado em código (fundo + área com máscara + placeholder + texto).
    private static TMP_InputField BuildInput(Transform parent)
    {
        var back = UIFactory.Image("NameInput", parent, new Color(0f, 0f, 0f, 0.6f));
        back.raycastTarget = true;
        var area = UIFactory.Rect("TextArea", back.transform).Stretch(3f);
        area.gameObject.AddComponent<RectMask2D>();
        var placeholder = UIFactory.Text("Placeholder", area, "TYPE YOUR NAME", 10f, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.MidlineLeft);
        placeholder.rectTransform.Stretch();
        var text = UIFactory.Text("Text", area, "", 10f, Color.white, TextAlignmentOptions.MidlineLeft);
        text.rectTransform.Stretch();

        var input = back.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = back;
        input.characterLimit = 14;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.caretColor = UIFactory.Gold;
        input.customCaretColor = true;
        input.caretWidth = 2;
        return input;
    }

    // run = null: só mostra o ranking.
    public void Show(LeaderboardClient.Entry playerRun)
    {
        run = playerRun;
        highlightName = LeaderboardClient.SavedName;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        nameRow.SetActive(false);
        runLine.text = run == null ? "" :
            $"YOUR RUN   <color=#FFD447>SCORE {run.score}</color>   TIME {FormatTime(run.time_sec)}   CARDS {run.cards_thrown}   " +
            $"CRITS {run.criticals}   FUSIONS {run.fusions}   DMG {run.damage_taken}   DEATHS {run.deaths}";
        status.text = "LOADING...";
        title.text = "TOP 10";
        FillRows();
        StartCoroutine(LeaderboardClient.FetchTop(AfterFetch));
    }

    public void Hide() => gameObject.SetActive(false);

    private void AfterFetch()
    {
        title.text = LeaderboardClient.Online ? "TOP 10 - GLOBAL" : "TOP 10 - LOCAL (OFFLINE)";
        FillRows();
        if (run == null) { status.text = LeaderboardClient.Entries.Count == 0 ? "NO RUNS YET. BE THE FIRST!" : ""; return; }

        int rank = LeaderboardClient.RankFor(run.score);
        if (rank > 0)
        {
            status.text = $"<color=#FFD447>YOU MADE THE TOP 10 (#{rank})!</color> TYPE YOUR NAME AND PRESS SUBMIT.";
            nameRow.SetActive(true);
            submitButton.interactable = true;
            nameInput.text = LeaderboardClient.SavedName;
            nameInput.ActivateInputField();
        }
        else status.text = "NOT IN THE TOP 10 THIS TIME. LESS TIME, FEWER CARDS, MORE CRITS!";
    }

    private void SubmitClicked()
    {
        if (run == null) return;
        string nick = LeaderboardClient.CleanName(nameInput.text);
        if (!LeaderboardClient.IsNameOk(nick)) { status.text = "<color=#FF5A5A>INVALID NAME (2-14 LETTERS/NUMBERS)</color>"; return; }
        submitButton.interactable = false;
        status.text = "SENDING...";
        StartCoroutine(LeaderboardClient.Submit(nick, run, (ok, error) =>
        {
            highlightName = nick;
            nameRow.SetActive(!ok && error == "INVALID NAME");
            submitButton.interactable = true;
            status.text = ok ? "<color=#3CC86E>SAVED!</color>" : $"<color=#FF5A5A>{error}</color>";
            title.text = LeaderboardClient.Online ? "TOP 10 - GLOBAL" : "TOP 10 - LOCAL (OFFLINE)";
            FillRows();
        }));
    }

    private void FillRows()
    {
        var list = LeaderboardClient.Entries;
        for (int r = 1; r <= LeaderboardClient.Size; r++)
        {
            var e = r - 1 < list.Count ? list[r - 1] : null;
            string[] values = e == null
                ? new[] { r.ToString(), "-", "", "", "", "", "", "", "" }
                : new[]
                {
                    r.ToString(), e.name, e.score.ToString(), FormatTime(e.time_sec), e.cards_thrown.ToString(),
                    e.criticals.ToString(), e.fusions.ToString(), e.damage_taken.ToString(), e.deaths.ToString(),
                };
            bool mine = e != null && !string.IsNullOrEmpty(highlightName) &&
                        string.Equals(e.name, highlightName, StringComparison.OrdinalIgnoreCase);
            for (int c = 0; c < Columns; c++)
            {
                cells[r, c].text = values[c];
                cells[r, c].color = mine ? Highlight : (e == null ? new Color(1f, 1f, 1f, 0.3f) : Color.white);
            }
        }
    }

    public static string FormatTime(int seconds) => $"{seconds / 60}:{seconds % 60:00}";
}
