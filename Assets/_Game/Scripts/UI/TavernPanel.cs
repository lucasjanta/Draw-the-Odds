using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Taverna (menu principal): onde as fichas ganhas nas runs são gastas.
//  - CORINGAS: libera coringas novos (e evoluções) para o sorteio das runs;
//  - GUARDA-ROUPA: chapéus, roupas e versos de carta do aldeão (com prévia);
//  - BOATOS: o mural com os 3 boatos de cada nobre (desafios) e o botão REVER HISTÓRIA;
//  - CONQUISTAS: desafios que liberam itens do guarda-roupa.
// Tudo com toque/clique simples (sem arrastar), em cartões grandes para o celular.
public class TavernPanel : MonoBehaviour
{
    private enum Tab { Jokers, Wardrobe, Rumors, Trophies }
    private enum Closet { Hats, Outfits, Backs }

    private const float PanelW = 620f, PanelH = 336f;
    private static readonly Color Dim = new Color(1f, 1f, 1f, 0.45f);
    private static readonly Color Green = new Color32(0x3C, 0xC8, 0x6E, 0xFF);

    private GameConfig config;
    private RectTransform content;
    private TextMeshProUGUI chipsText, message;
    private readonly Image[] tabFrames = new Image[4];
    private Tab tab;
    private Closet closet;
    private float messageUntil;

    public static TavernPanel Create(Transform parent, GameConfig config, Action onBack)
    {
        var panel = UIFactory.MenuPanel("Tavern", parent, new Vector2(PanelW, PanelH));
        var t = panel.gameObject.AddComponent<TavernPanel>();
        t.config = config;

        var title = UIFactory.Text("Title", panel.transform, "TAVERN", 20f, UIFactory.Gold, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        if (config.TitleFont != null) title.font = config.TitleFont;
        title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(200f, 26f));

        t.chipsText = UIFactory.Text("Chips", panel.transform, "", 14f, UIFactory.Gold, TextAlignmentOptions.Right).NoLoc();
        t.chipsText.Outline(1f);
        t.chipsText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -10f), new Vector2(220f, 22f));

        // Abas
        string[] tabs = { "JOKERS", "WARDROBE", "RUMORS", "TROPHIES" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            var b = UIFactory.Button("Tab" + i, panel.transform, tabs[i], new Vector2(136f, 22f), () => t.ShowTab((Tab)index), 10f);
            b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - 1.5f) * 144f, -40f), new Vector2(136f, 22f));
            var frame = UIFactory.Image("Selected", b.transform, UIFactory.Gold);
            frame.raycastTarget = false;
            frame.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(136f, 3f));
            t.tabFrames[i] = frame;
        }

        t.content = UIFactory.Rect("Content", panel.transform).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(PanelW - 20f, 226f));

        t.message = UIFactory.Text("Message", panel.transform, "", 9f, new Color32(0xFF, 0x6A, 0x5A, 0xFF), TextAlignmentOptions.Left).NoLoc();
        t.message.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 14f), new Vector2(300f, 16f));

        var back = UIFactory.Button("Back", panel.transform, "BACK", new Vector2(120f, 24f), onBack, 12f);
        back.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 10f), new Vector2(120f, 24f));

        ProgressSave.OnChanged += t.Refresh;
        Loc.OnChanged += t.Refresh;
        panel.gameObject.SetActive(false);
        return t;
    }

    private void OnDestroy()
    {
        ProgressSave.OnChanged -= Refresh;
        Loc.OnChanged -= Refresh;
    }

    public void Show(bool show)
    {
        gameObject.SetActive(show);
        if (show) { transform.SetAsLastSibling(); ShowTab(tab); }
    }

    private void ShowTab(Tab t)
    {
        tab = t;
        Refresh();
    }

    private void Refresh()
    {
        if (this == null || !gameObject.activeInHierarchy) return;
        chipsText.text = $"● {ProgressSave.Chips} " + Loc.Pick("CHIPS", "FICHAS", "FICHAS", "JETONS", "CHIPS", "FICHE");
        for (int i = 0; i < tabFrames.Length; i++) tabFrames[i].enabled = i == (int)tab;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        switch (tab)
        {
            case Tab.Jokers: BuildJokers(); break;
            case Tab.Wardrobe: BuildWardrobe(); break;
            case Tab.Rumors: BuildRumors(); break;
            default: BuildTrophies(); break;
        }
    }

    private void Update()
    {
        if (message.text.Length > 0 && Time.unscaledTime > messageUntil) message.text = "";
    }

    private void Say(string text, bool good = false)
    {
        message.text = text;
        message.color = good ? Green : new Color32(0xFF, 0x6A, 0x5A, 0xFF);
        messageUntil = Time.unscaledTime + 2.5f;
    }

    private static string NotEnough => Loc.Pick("Not enough chips! Win fights to earn more.", "Fichas insuficientes! Vença lutas para ganhar mais.",
        "¡No tienes fichas suficientes! Gana peleas para conseguir más.", "Pas assez de jetons ! Gagne des combats pour en avoir.",
        "Nicht genug Chips! Gewinne Kämpfe, um mehr zu bekommen.", "Fiche insufficienti! Vinci scontri per averne altre.");

    // Cartão genérico (moldura colorida + miolo), clicável.
    private RectTransform Card(string name, Vector2 pos, Vector2 size, Color frameColor, Action onClick)
    {
        var b = UIFactory.Button(name, content, "", size, onClick);
        var rt = b.GetComponent<RectTransform>();
        rt.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, size);
        var frame = UIFactory.Image("Frame", rt, frameColor);
        frame.rectTransform.Stretch();
        frame.raycastTarget = false;
        var face = UIFactory.Image("Face", rt, new Color(0.1f, 0.07f, 0.16f, 1f));
        face.rectTransform.Stretch(2f);
        face.raycastTarget = false;
        return rt;
    }

    private static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box, bool wrap = false)
    {
        var t = UIFactory.Text("Text", parent, text, size, color, TextAlignmentOptions.Top).NoLoc();
        if (wrap)
        {
            t.textWrappingMode = TextWrappingModes.Normal;
            t.lineSpacing = 3f;
            var fit = t.GetComponent<TextFit>();
            if (fit != null) Destroy(fit);
        }
        t.raycastTarget = false;
        t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, box);
        return t;
    }

    // ===================== Coringas =====================
    // 4 colunas × 5 linhas de cartões deitados: ícone, nome, efeito e preço.
    private void BuildJokers()
    {
        const float w = 146f, h = 42f, gapX = 150f, gapY = 45f;
        var all = JokerCatalog.All;
        for (int i = 0; i < all.Length; i++)
        {
            var def = all[i];
            int col = i % 4, row = i / 4;
            bool owned = ProgressSave.JokerOwned(def.id);
            bool canBuy = ProgressSave.Chips >= def.cost;
            var rt = Card("Joker" + i, new Vector2((col - 1.5f) * gapX, -row * gapY), new Vector2(w, h),
                          owned ? def.color : new Color(0.3f, 0.28f, 0.38f), () => BuyJoker(def));
            var icon = JokerIcon.Create(rt, def, 26f);
            icon.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(26f, 26f));
            if (!owned) icon.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.08f, 1f);
            var name = SmallText(rt, def.Name, 7f, owned ? def.color : Dim, new Vector2(35f, -4f), new Vector2(w - 70f, 10f), TextAlignmentOptions.TopLeft);
            name.fontStyle = FontStyles.Bold;
            var desc = SmallText(rt, def.Desc, 6f, owned ? Color.white : Dim, new Vector2(35f, -15f), new Vector2(w - 40f, 24f), TextAlignmentOptions.TopLeft);
            desc.textWrappingMode = TextWrappingModes.Normal;
            var fit = desc.GetComponent<TextFit>();
            if (fit != null) Destroy(fit);
            string status = owned ? $"<color=#3CC86E>{Loc.T("OWNED")}</color>"
                                  : $"<color={(canBuy ? "#FFD447" : "#8A8098")}>● {def.cost}</color>";
            SmallText(rt, status, 7f, Color.white, new Vector2(w - 36f, -4f), new Vector2(32f, 10f), TextAlignmentOptions.TopRight);
        }
    }

    private static TextMeshProUGUI SmallText(Transform parent, string text, float size, Color color, Vector2 topLeft, Vector2 box, TextAlignmentOptions align)
    {
        var t = UIFactory.Text("Text", parent, text, size, color, align).NoLoc();
        t.raycastTarget = false;
        t.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, box);
        return t;
    }

    private void BuyJoker(JokerDef def)
    {
        if (ProgressSave.JokerOwned(def.id)) { Say(def.Desc, true); return; }
        if (!ProgressSave.SpendChips(def.cost)) { Say(NotEnough); return; }
        ProgressSave.UnlockJoker(def.id);
        AudioManager.Sfx(config.SfxPowerUp, 0.9f, 0f);
        Say(Loc.Pick($"{def.Name} joins the deck!", $"{def.Name} entrou no baralho!", $"¡{def.Name} entra en la baraja!",
                     $"{def.Name} rejoint le paquet !", $"{def.Name} ist jetzt im Deck!", $"{def.Name} entra nel mazzo!"), true);
    }

    // ===================== Guarda-roupa =====================
    // Esquerda: o aldeão grande (chapéu + roupa) e o verso escolhido. Direita: sub-abas e itens.
    private void BuildWardrobe()
    {
        // Prévia
        var frame = UIFactory.Image("Preview", content, new Color(0.25f, 0.22f, 0.32f));
        frame.raycastTarget = false;
        frame.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-222f, -2f), new Vector2(150f, 220f));
        var face = UIFactory.Image("Face", frame.transform, new Color(0.1f, 0.07f, 0.16f, 1f));
        face.rectTransform.Stretch(2f);
        face.raycastTarget = false;
        var hero = UIFactory.Image("Villager", frame.transform, Color.white, VillagerPalette.Portrait(config, Wardrobe.SelectedHat, Wardrobe.SelectedOutfit));
        hero.preserveAspect = true;
        hero.raycastTarget = false;
        hero.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(150f, 111f));
        var back = UIFactory.Image("Back", frame.transform, CardBacks.Tint, config.CardBack);
        back.preserveAspect = true;
        back.raycastTarget = false;
        back.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(54f, 74f));
        SmallText(frame.transform, $"{Wardrobe.Hats[Wardrobe.SelectedHat].Name}\n{Wardrobe.Outfits[Wardrobe.SelectedOutfit].Name}", 6.5f, Color.white,
                  new Vector2(4f, -118f), new Vector2(142f, 20f), TextAlignmentOptions.Top);
        SmallText(frame.transform, Loc.T(CardBacks.All[Mathf.Clamp(ProgressSave.SelectedBack, 0, CardBacks.All.Length - 1)].name), 6.5f, UIFactory.Gold,
                  new Vector2(4f, -206f), new Vector2(142f, 10f), TextAlignmentOptions.Top);

        // Sub-abas
        string[] names = { "HATS", "OUTFITS", "CARD BACKS" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var b = UIFactory.Button("Closet" + i, content, names[i], new Vector2(122f, 18f), () => { closet = (Closet)index; Refresh(); }, 9f);
            b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-84f + i * 130f, -2f), new Vector2(122f, 18f));
            var label = b.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.color = i == (int)closet ? UIFactory.Gold : new Color(1f, 1f, 1f, 0.55f);
        }

        // Itens (7 por linha)
        int count = closet == Closet.Hats ? Wardrobe.Hats.Length : closet == Closet.Outfits ? Wardrobe.Outfits.Length : CardBacks.All.Length;
        const float w = 60f, h = 192f, gap = 64f;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            bool owned, equipped;
            string name;
            int cost, achievement;
            Sprite art;
            Color tint = Color.white;
            switch (closet)
            {
                case Closet.Hats:
                    var hat = Wardrobe.Hats[i];
                    owned = Wardrobe.HatOwned(i); equipped = owned && Wardrobe.SelectedHat == i;
                    name = hat.Name; cost = hat.cost; achievement = hat.achievement;
                    art = VillagerPalette.Portrait(config, i, Wardrobe.SelectedOutfit);
                    break;
                case Closet.Outfits:
                    var outfit = Wardrobe.Outfits[i];
                    owned = Wardrobe.OutfitOwned(i); equipped = owned && Wardrobe.SelectedOutfit == i;
                    name = outfit.Name; cost = outfit.cost; achievement = outfit.achievement;
                    art = VillagerPalette.Portrait(config, Wardrobe.SelectedHat, i);
                    break;
                default:
                    var cb = CardBacks.All[i];
                    owned = ProgressSave.BackOwned(i); equipped = owned && ProgressSave.SelectedBack == i;
                    name = Loc.T(cb.name); cost = cb.cost; achievement = cb.achievement;
                    art = config.CardBack; tint = cb.tint;
                    break;
            }
            var rt = Card("Item" + i, new Vector2(-150f + 36f + i * gap, -26f), new Vector2(w, h),
                          equipped ? UIFactory.Gold : new Color(0.3f, 0.28f, 0.38f), () => WardrobeClick(index));
            var img = UIFactory.Image("Art", rt, owned ? tint : new Color(tint.r * 0.35f, tint.g * 0.35f, tint.b * 0.4f, 1f), art);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), closet == Closet.Backs ? new Vector2(44f, 62f) : new Vector2(56f, 84f));
            var label = SmallText(rt, name, 6f, owned ? Color.white : Dim, new Vector2(3f, -100f), new Vector2(w - 6f, 30f), TextAlignmentOptions.Top);
            label.textWrappingMode = TextWrappingModes.Normal;
            var fit = label.GetComponent<TextFit>();
            if (fit != null) Destroy(fit);
            string status = equipped ? $"<color=#FFD447>{Loc.T("EQUIPPED")}</color>"
                          : owned ? $"<color=#3CC86E>{Loc.T("OWNED")}</color>"
                          : achievement >= 0 ? $"<color=#B57CFF>{Loc.T("TROPHIES")}</color>"
                          : $"<color={(ProgressSave.Chips >= cost ? "#FFD447" : "#8A8098")}>● {cost}</color>";
            var st = SmallText(rt, status, 6f, Color.white, new Vector2(3f, -h + 16f), new Vector2(w - 6f, 10f), TextAlignmentOptions.Top);
            st.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    // Clique num item: tem = veste · não tem = compra (fichas) · de conquista = diz o que fazer.
    private void WardrobeClick(int i)
    {
        bool owned;
        int cost, achievement;
        switch (closet)
        {
            case Closet.Hats: owned = Wardrobe.HatOwned(i); cost = Wardrobe.Hats[i].cost; achievement = Wardrobe.Hats[i].achievement; break;
            case Closet.Outfits: owned = Wardrobe.OutfitOwned(i); cost = Wardrobe.Outfits[i].cost; achievement = Wardrobe.Outfits[i].achievement; break;
            default: owned = ProgressSave.BackOwned(i); cost = CardBacks.All[i].cost; achievement = CardBacks.All[i].achievement; break;
        }
        if (!owned)
        {
            if (achievement >= 0) { Say(Loc.T("ACHIEVEMENT") + ": " + Achievements.Desc(achievement)); return; }
            if (!ProgressSave.SpendChips(cost)) { Say(NotEnough); return; }
            if (closet == Closet.Hats) ProgressSave.BuyHat(i);
            else if (closet == Closet.Outfits) ProgressSave.BuyOutfit(i);
            else ProgressSave.UnlockBack(i);
            AudioManager.Sfx(config.SfxPowerUp, 0.9f, 0f);
        }
        else AudioManager.Ui(config.SfxUiClick);
        if (closet == Closet.Hats) ProgressSave.SelectedHat = i;
        else if (closet == Closet.Outfits) ProgressSave.SelectedOutfit = i;
        else ProgressSave.SelectedBack = i;
    }

    // ===================== Conquistas =====================
    private void BuildTrophies()
    {
        for (int a = 0; a < Achievements.Count; a++)
        {
            bool done = Achievements.Has(a);
            var row = UIFactory.Image("Trophy" + a, content, done ? new Color(0.55f, 0.42f, 0.12f) : new Color(0.25f, 0.22f, 0.32f));
            row.raycastTarget = false;
            row.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -a * 37f), new Vector2(560f, 34f));
            var face = UIFactory.Image("Face", row.transform, new Color(0.1f, 0.07f, 0.16f, 1f));
            face.rectTransform.Stretch(1f);
            face.raycastTarget = false;
            SmallText(row.transform, done ? "<color=#FFD447>●</color>" : "<color=#8A8098>?</color>", 14f, Color.white, new Vector2(8f, -6f), new Vector2(20f, 20f), TextAlignmentOptions.Center);
            var name = SmallText(row.transform, Achievements.Name(a), 9f, done ? UIFactory.Gold : Color.white, new Vector2(34f, -4f), new Vector2(250f, 12f), TextAlignmentOptions.TopLeft);
            name.fontStyle = FontStyles.Bold;
            SmallText(row.transform, Achievements.Desc(a), 7f, done ? Color.white : Dim, new Vector2(34f, -19f), new Vector2(260f, 11f), TextAlignmentOptions.TopLeft);
            SmallText(row.transform, Achievements.RewardText(a), 7f, done ? new Color32(0x3C, 0xC8, 0x6E, 0xFF) : (Color)new Color32(0xB5, 0x7C, 0xFF, 0xFF),
                      new Vector2(300f, -12f), new Vector2(254f, 11f), TextAlignmentOptions.TopRight);
        }
    }

    // ===================== Mural de Boatos =====================
    private void BuildRumors()
    {
        string[] bosses = { "JACK", "QUEEN", "KING" };
        const float colW = 192f, gap = 198f;
        for (int b = 0; b < Rumors.Bosses; b++)
        {
            float x = (b - 1) * gap;
            int found = 0;
            for (int s = 0; s < Rumors.PerBoss; s++) if (ProgressSave.RumorFound(Rumors.Index(b, s))) found++;
            var head = Label(content, $"{Loc.T(bosses[b])}  <color=#FFD447>{found}/{Rumors.PerBoss}</color>", 11f, Color.white,
                             new Vector2(x, 0f), new Vector2(colW, 16f));
            if (config.TitleFont != null) head.font = config.TitleFont;
            for (int s = 0; s < Rumors.PerBoss; s++)
            {
                int index = Rumors.Index(b, s);
                bool ok = ProgressSave.RumorFound(index);
                var frame = UIFactory.Image("Rumor" + index, content, ok ? new Color(0.55f, 0.42f, 0.12f) : new Color(0.25f, 0.22f, 0.32f));
                frame.raycastTarget = false;
                frame.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -20f - s * 52f), new Vector2(colW, 48f));
                var face = UIFactory.Image("Face", frame.transform, new Color(0.1f, 0.07f, 0.16f, 1f));
                face.rectTransform.Stretch(1f);
                face.raycastTarget = false;
                string challenge = (ok ? "<color=#3CC86E>●</color> " : "<color=#8A8098>?</color> ") + Rumors.Challenge(index);
                string body = ok ? $"<size=7><color=#B8B0C8>{challenge}</color></size>\n\"{Rumors.Lore(index)}\""
                                 : $"{challenge}\n<size=7><color=#8A8098>+{Rumors.ChipReward} ●</color></size>";
                Label(frame.transform, body, 7.5f, ok ? Color.white : Dim, new Vector2(0f, -4f), new Vector2(colW - 10f, 42f), true);
            }
        }

        // Rodapé: o Ás e o botão de rever as cenas da história
        bool ace = ProgressSave.AceUnlocked;
        string aceText = ace
            ? Loc.Pick("<color=#4CFF9A>The ACE waits after the King...</color>", "<color=#4CFF9A>O ÁS espera depois do Rei...</color>",
                       "<color=#4CFF9A>El AS espera después del Rey...</color>", "<color=#4CFF9A>L'AS attend après le Roi...</color>",
                       "<color=#4CFF9A>Das ASS wartet nach dem König...</color>", "<color=#4CFF9A>L'ASSO aspetta dopo il Re...</color>")
            : Loc.Pick("The King's 3 rumors reveal a secret...", "Os 3 boatos do Rei revelam um segredo...", "Los 3 rumores del Rey revelan un secreto...",
                       "Les 3 rumeurs du Roi révèlent un secret...", "Die 3 Gerüchte des Königs enthüllen ein Geheimnis...", "Le 3 voci sul Re rivelano un segreto...");
        if (ProgressSave.EndingSeen(0) || ProgressSave.EndingSeen(1))
            aceText += $"   <color=#FFD447>{Loc.Pick("ENDINGS", "FINAIS", "FINALES", "FINS", "ENDEN", "FINALI")} {(ProgressSave.EndingSeen(0) ? 1 : 0) + (ProgressSave.EndingSeen(1) ? 1 : 0)}/2</color>";
        Label(content, aceText, 8f, Dim, new Vector2(-70f, -180f), new Vector2(440f, 14f));
        var replay = UIFactory.Button("Replay", content, "REPLAY STORY", new Vector2(130f, 20f), () =>
        {
            StoryLines.ResetAll();
            Say(Loc.Pick("The story scenes will play again in your next runs.", "As cenas da história vão aparecer de novo nas próximas runs.",
                         "Las escenas de la historia volverán a aparecer en tus próximas partidas.", "Les scènes de l'histoire rejoueront lors de tes prochaines parties.",
                         "Die Story-Szenen laufen in deinen nächsten Runs wieder.", "Le scene della storia torneranno nelle prossime partite."), true);
        }, 9f);
        replay.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(225f, -176f), new Vector2(130f, 20f));
    }

    // Debug (ponte): call TavernPanel.DebugTab 0|1|2|3 (guarda-roupa: "1 0" chapéus, "1 1" roupas, "1 2" versos)
    public static string DebugTab(string index)
    {
        var t = FindAnyObjectByType<TavernPanel>();
        if (t == null) return "no tavern";
        var parts = index.Split(' ');
        if (parts.Length > 1) t.closet = (Closet)int.Parse(parts[1]);
        t.ShowTab((Tab)int.Parse(parts[0]));
        return "tab " + index;
    }
}
