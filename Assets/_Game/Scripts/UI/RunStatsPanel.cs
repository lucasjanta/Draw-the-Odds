using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Status da run no menu de pausa (ao lado dos botões): chefe atual, tempo, cartas, críticos...
// e os coringas do cinto com o efeito de cada um.
public class RunStatsPanel : MonoBehaviour
{
    private const float W = 300f, H = 304f;
    private static readonly string[] Labels =
    {
        "TIME", "CARDS THROWN", "HITS", "CRITICALS", "DAMAGE DEALT", "FUSIONS", "RAISES WON", "DAMAGE TAKEN", "HEALED", "DEATHS", "CHIPS EARNED",
    };

    private TextMeshProUGUI header;
    private readonly TextMeshProUGUI[] values = new TextMeshProUGUI[Labels.Length];
    private RectTransform jokerArea;
    private TextMeshProUGUI noJokers;
    private readonly List<GameObject> jokerRows = new List<GameObject>();

    public static RunStatsPanel Create(Transform parent, Vector2 position)
    {
        var panel = UIFactory.MenuPanel("RunStats", parent, new Vector2(W, H));
        panel.rectTransform.anchoredPosition = position;
        var p = panel.gameObject.AddComponent<RunStatsPanel>();
        var t = panel.transform;

        var title = UIFactory.Text("Title", t, "RUN STATUS", 14f, UIFactory.Gold, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(W - 20f, 18f));

        p.header = UIFactory.Text("Header", t, "", 8f, Color.white, TextAlignmentOptions.Center).NoLoc();
        p.header.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(W - 16f, 22f));
        p.header.textWrappingMode = TextWrappingModes.Normal;

        // Duas colunas de números (rótulo à esquerda, valor à direita)
        for (int i = 0; i < Labels.Length; i++)
        {
            int col = i < 6 ? 0 : 1, row = i < 6 ? i : i - 6;
            float x = 10f + col * 145f, y = -52f - row * 13f;
            var label = UIFactory.Text("L" + i, t, Labels[i], 7f, new Color(0.75f, 0.72f, 0.85f), TextAlignmentOptions.MidlineLeft);
            label.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(98f, 12f));
            var v = UIFactory.Text("V" + i, t, "", 8f, Color.white, TextAlignmentOptions.MidlineRight).NoLoc();
            v.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + 98f, y), new Vector2(38f, 12f));
            p.values[i] = v;
        }

        var jokersTitle = UIFactory.Text("JokersTitle", t, "JOKERS", 10f, new Color32(0xB5, 0x7C, 0xFF, 0xFF), TextAlignmentOptions.MidlineLeft);
        jokersTitle.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -136f), new Vector2(200f, 14f));
        p.jokerArea = UIFactory.Rect("Jokers", t).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -152f), new Vector2(W - 20f, 145f));
        p.noJokers = UIFactory.Text("None", p.jokerArea, "NO JOKERS YET", 8f, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.TopLeft);
        p.noJokers.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(W - 20f, 12f));
        return p;
    }

    public void Refresh(GameManager gm)
    {
        var s = gm.Stats;
        string mode = gm.IsTutorial ? "TUTORIAL"
            : GameManager.EndlessMode ? Loc.Pick("ENDLESS", "INFINITO", "INFINITO", "INFINI", "ENDLOS", "INFINITO") + " · " +
                                        Loc.Pick("ROUND", "RODADA", "RONDA", "MANCHE", "RUNDE", "ROUND") + " " + gm.EndlessRound
            : gm.IsSecretBoss ? Loc.Pick("SECRET BOSS", "CHEFE SECRETO", "JEFE SECRETO", "BOSS SECRET", "GEHEIMER BOSS", "BOSS SEGRETO")
            : Loc.Pick("BOSS", "CHEFE", "JEFE", "BOSS", "BOSS", "BOSS") + $" {gm.BossIndex + 1}/{gm.Config.Bosses.Count}";
        string diff = gm.IsTutorial ? "" : "  ·  " + Loc.T(gm.CurrentDifficulty.DisplayName);
        string boss = gm.CurrentBossData != null ? Loc.T(gm.CurrentBossData.BannerName) : "";
        header.text = $"<color=#FFD447>{mode}</color>{diff}\n{boss}";

        int seconds = Mathf.FloorToInt(s.Elapsed);
        int accuracy = s.cardsThrown > 0 ? Mathf.RoundToInt(100f * s.hits / s.cardsThrown) : 0;
        string[] v =
        {
            $"{seconds / 60}:{seconds % 60:00}", s.cardsThrown.ToString(), $"{s.hits} <size=80%>({accuracy}%)</size>", s.criticals.ToString(),
            s.damageDealt.ToString(), s.fusions.ToString(), s.raisesWon.ToString(), s.damageTaken.ToString(), s.healed.ToString(),
            s.deaths.ToString(), "● " + s.chipsEarned,
        };
        for (int i = 0; i < values.Length; i++) values[i].text = v[i];

        foreach (var row in jokerRows) Destroy(row);
        jokerRows.Clear();
        noJokers.gameObject.SetActive(Jokers.Belt.Count == 0);
        for (int i = 0; i < Jokers.Belt.Count; i++)
        {
            var def = JokerCatalog.Get(Jokers.Belt[i]);
            var row = UIFactory.Rect("Joker" + i, jokerArea).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * 28f), new Vector2(W - 20f, 26f));
            JokerIcon.Create(row, def, 22f).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            var name = UIFactory.Text("Name", row, def.Name, 8f, def.color, TextAlignmentOptions.TopLeft).NoLoc();
            name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -1f), new Vector2(W - 50f, 11f));
            var desc = UIFactory.Text("Desc", row, def.Desc, 7f, Color.white, TextAlignmentOptions.TopLeft).NoLoc();
            desc.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -13f), new Vector2(W - 50f, 11f));
            jokerRows.Add(row.gameObject);
        }
    }
}
