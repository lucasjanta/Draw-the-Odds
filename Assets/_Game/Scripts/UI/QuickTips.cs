using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Dicas curtas no topo da tela para quem entra direto na luta (JOGAR sem o tutorial).
// Cada dica aparece UMA vez por jogador (fica salvo no navegador). Quem termina o tutorial completo
// não vê nenhuma (MarkAllSeen). As dicas esperam na fila e aparecem uma de cada vez.
public class QuickTips : MonoBehaviour
{
    private const string PrefPrefix = "dto_tip_";
    private const float ShowSeconds = 4.2f;
    private static readonly string[] AllIds = { "move", "throw", "hint", "crit", "orb", "fusion", "suits", "redraw" };

    private GameManager gm;
    private CanvasGroup group;
    private TextMeshProUGUI text;
    private readonly Queue<string> queue = new Queue<string>();
    private readonly HashSet<string> shownThisSession = new HashSet<string>();
    private bool running;

    public static QuickTips Create(Transform parent, GameManager gm)
    {
        var back = UIFactory.Image("QuickTip", parent, new Color(0.05f, 0.03f, 0.12f, 0.9f));
        // Abaixo dos painéis de HUD, centralizada (o botão de pausa do toque fica acima).
        back.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(300f, 30f));
        var stripe = UIFactory.Image("Stripe", back.transform, UIFactory.Gold);
        stripe.rectTransform.anchorMin = new Vector2(0f, 1f);
        stripe.rectTransform.anchorMax = new Vector2(1f, 1f);
        stripe.rectTransform.pivot = new Vector2(0.5f, 1f);
        stripe.rectTransform.sizeDelta = new Vector2(0f, 2f);

        var tips = back.gameObject.AddComponent<QuickTips>();
        tips.gm = gm;
        tips.group = back.gameObject.AddComponent<CanvasGroup>();
        tips.group.alpha = 0f;
        tips.group.blocksRaycasts = false;
        tips.text = UIFactory.Text("Text", back.transform, "", 9f, Color.white, TextAlignmentOptions.Center).NoLoc();
        tips.text.lineSpacing = 3f;
        tips.text.rectTransform.Stretch(4f);
        return tips;
    }

    public static bool Seen(string id) => PlayerPrefs.GetInt(PrefPrefix + id, 0) == 1;

    // Terminou o tutorial: já aprendeu tudo.
    public static void MarkAllSeen()
    {
        foreach (var id in AllIds) PlayerPrefs.SetInt(PrefPrefix + id, 1);
        PlayerPrefs.Save();
    }

    // Pede uma dica (ignora se já foi vista, se é o tutorial ou se já está na fila).
    public void Request(string id)
    {
        if (gm.IsTutorial || Seen(id) || shownThisSession.Contains(id) || queue.Contains(id)) return;
        queue.Enqueue(id);
        if (!running) StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        running = true;
        while (queue.Count > 0)
        {
            // Só durante a luta, sem pausa nem fusão aberta (a fusão tem a própria ajuda... exceto a dica dela).
            while (gm.State != GameManager.GameState.Playing || GameTime.Paused) yield return null;
            string id = queue.Dequeue();
            shownThisSession.Add(id);
            PlayerPrefs.SetInt(PrefPrefix + id, 1);
            text.text = TextFor(id);
            var rt = (RectTransform)transform;
            float h = text.GetPreferredValues(text.text, rt.sizeDelta.x - 8f, 0f).y + 12f;
            float height = Mathf.Max(24f, h);
            // Com a fusão aberta o painel ocupa o meio/topo: a dica vai para baixo dele (acima da mão).
            if (gm.Fusion.Active) rt.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(300f, height));
            else rt.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(300f, height));
            transform.SetAsLastSibling();

            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { group.alpha = t / 0.25f; yield return null; }
            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(ShowSeconds);
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / 0.35f; yield return null; }
            group.alpha = 0f;
            yield return new WaitForSecondsRealtime(0.4f);
        }
        PlayerPrefs.Save();
        running = false;
    }

    private static string Y(string s) => $"<color=#FFD447>{s}</color>";

    private static string TextFor(string id)
    {
        bool touch = VirtualInput.Active;
        string C(Suit s) => SuitInfo.Colored(SuitInfo.Symbol(s), s);
        string N(GameKey k) => KeyBindings.Name(k); // tecla configurada (Opções → CONTROLS)
        string lr = Y(N(GameKey.Left) + "/" + N(GameKey.Right)), jump = Y(N(GameKey.Jump)), dash = Y(N(GameKey.Dash));
        string drop = Y(N(GameKey.Down) + "+" + N(GameKey.Jump));
        switch (id)
        {
            case "move":
                return touch
                    ? Loc.Pick($"{Y("JOYSTICK")} move   {Y("▲")} jump   {Y("»")} dash", $"{Y("JOYSTICK")} anda   {Y("▲")} pula   {Y("»")} dash",
                               $"{Y("JOYSTICK")} mover   {Y("▲")} saltar   {Y("»")} dash", $"{Y("JOYSTICK")} bouger   {Y("▲")} sauter   {Y("»")} dash",
                               $"{Y("JOYSTICK")} laufen   {Y("▲")} springen   {Y("»")} Dash", $"{Y("JOYSTICK")} muoviti   {Y("▲")} salta   {Y("»")} dash")
                    : Loc.Pick($"{lr} move   {jump} jump   {dash} dash   {drop} drop",
                               $"{lr} anda   {jump} pula   {dash} dash   {drop} desce",
                               $"{lr} mover   {jump} saltar   {dash} dash   {drop} bajar",
                               $"{lr} bouger   {jump} sauter   {dash} dash   {drop} descendre",
                               $"{lr} laufen   {jump} springen   {dash} Dash   {drop} runter",
                               $"{lr} muoviti   {jump} salta   {dash} dash   {drop} scendi");
            case "throw":
                return touch
                    ? Loc.Pick($"Find the boss's {Y("SECRET NUMBER")}!\n{Y("DRAG")} a card up (or tap the screen) to throw it",
                               $"Descubra o {Y("NÚMERO SECRETO")} do chefe!\n{Y("ARRASTE")} uma carta para cima (ou toque na tela) para arremessar",
                               $"¡Descubre el {Y("NÚMERO SECRETO")} del jefe!\n{Y("ARRASTRA")} una carta hacia arriba (o toca la pantalla) para lanzarla",
                               $"Trouve le {Y("NOMBRE SECRET")} du boss !\n{Y("GLISSE")} une carte vers le haut (ou touche l'écran) pour la lancer",
                               $"Finde die {Y("GEHEIME ZAHL")} des Bosses!\n{Y("ZIEH")} eine Karte nach oben (oder tippe), um sie zu werfen",
                               $"Trova il {Y("NUMERO SEGRETO")} del boss!\n{Y("TRASCINA")} una carta in alto (o tocca lo schermo) per lanciarla")
                    : Loc.Pick($"Find the boss's {Y("SECRET NUMBER")}!\n{Y("SCROLL")} pick a card   {Y("CLICK")} throw it",
                               $"Descubra o {Y("NÚMERO SECRETO")} do chefe!\n{Y("RODINHA")} escolhe a carta   {Y("CLIQUE")} arremessa",
                               $"¡Descubre el {Y("NÚMERO SECRETO")} del jefe!\n{Y("RUEDA")} elige carta   {Y("CLIC")} la lanza",
                               $"Trouve le {Y("NOMBRE SECRET")} du boss !\n{Y("MOLETTE")} choisir   {Y("CLIC")} lancer",
                               $"Finde die {Y("GEHEIME ZAHL")} des Bosses!\n{Y("MAUSRAD")} Karte wählen   {Y("KLICK")} werfen",
                               $"Trova il {Y("NUMERO SEGRETO")} del boss!\n{Y("ROTELLA")} scegli carta   {Y("CLIC")} lancia");
            case "hint":
                return Loc.Pick("<color=#FF8A3D>▲ HIGHER</color> / <color=#5A9BFF>▼ LOWER</color>: the closer, the more damage",
                                "<color=#FF8A3D>▲ MAIOR</color> / <color=#5A9BFF>▼ MENOR</color>: quanto mais perto, mais dano",
                                "<color=#FF8A3D>▲ MAYOR</color> / <color=#5A9BFF>▼ MENOR</color>: cuanto más cerca, más daño",
                                "<color=#FF8A3D>▲ PLUS</color> / <color=#5A9BFF>▼ MOINS</color> : plus c'est proche, plus ça fait mal",
                                "<color=#FF8A3D>▲ HÖHER</color> / <color=#5A9BFF>▼ TIEFER</color>: je näher, desto mehr Schaden",
                                "<color=#FF8A3D>▲ PIÙ ALTO</color> / <color=#5A9BFF>▼ PIÙ BASSO</color>: più sei vicino, più danni fai");
            case "crit":
                return Loc.Pick($"{Y("EXACT = CRITICAL!")} The boss picks a new number",
                                $"{Y("EXATO = CRÍTICO!")} O chefe escolhe um número novo",
                                $"{Y("¡EXACTO = CRÍTICO!")} El jefe elige un número nuevo",
                                $"{Y("EXACT = CRITIQUE !")} Le boss choisit un nouveau nombre",
                                $"{Y("GENAU = KRITISCH!")} Der Boss wählt eine neue Zahl",
                                $"{Y("ESATTO = CRITICO!")} Il boss sceglie un nuovo numero");
            case "orb":
            {
                string ops = $"<color=#3CC86E>+</color> <color=#E84A5F>−</color> {Y("×")}";
                return Loc.Pick($"Grab the orbs {ops}: time stops and you merge 2 cards",
                                $"Pegue os orbes {ops}: o tempo para e você junta 2 cartas",
                                $"Coge los orbes {ops}: el tiempo se detiene y fusionas 2 cartas",
                                $"Prends les orbes {ops} : le temps s'arrête et tu fusionnes 2 cartes",
                                $"Sammle die Orbs {ops}: die Zeit stoppt und du verschmilzt 2 Karten",
                                $"Prendi le sfere {ops}: il tempo si ferma e unisci 2 carte");
            }
            case "fusion":
                return touch
                    ? Loc.Pick($"{Y("DRAG")} 2 cards into the slots, then {Y("MERGE")}", $"{Y("ARRASTE")} 2 cartas para os espaços e aperte {Y("JUNTAR")}",
                               $"{Y("ARRASTRA")} 2 cartas a los huecos y pulsa {Y("FUSIONAR")}", $"{Y("GLISSE")} 2 cartes dans les emplacements, puis {Y("FUSIONNER")}",
                               $"{Y("ZIEH")} 2 Karten in die Plätze, dann {Y("FUSION")}", $"{Y("TRASCINA")} 2 carte negli slot, poi {Y("FONDI")}")
                    : Loc.Pick($"{Y(N(GameKey.SlotA))} / {Y(N(GameKey.SlotB))} (or drag) place 2 cards   {Y("CLICK")} merge",
                               $"{Y(N(GameKey.SlotA))} / {Y(N(GameKey.SlotB))} (ou arraste) coloca 2 cartas   {Y("CLIQUE")} funde",
                               $"{Y(N(GameKey.SlotA))} / {Y(N(GameKey.SlotB))} (o arrastra) coloca 2 cartas   {Y("CLIC")} fusiona",
                               $"{Y(N(GameKey.SlotA))} / {Y(N(GameKey.SlotB))} (ou glisse) place 2 cartes   {Y("CLIC")} fusionne",
                               $"{Y(N(GameKey.SlotA))} / {Y(N(GameKey.SlotB))} (oder ziehen) 2 Karten ablegen   {Y("KLICK")} verschmelzen",
                               $"{Y(N(GameKey.SlotA))} / {Y(N(GameKey.SlotB))} (o trascina) metti 2 carte   {Y("CLIC")} fondi");
            case "suits":
                return Loc.Pick($"{C(Suit.Spades)} x1.5 damage   {C(Suit.Hearts)} heals you   {C(Suit.Diamonds)} +1 card",
                                $"{C(Suit.Spades)} dano x1.5   {C(Suit.Hearts)} cura você   {C(Suit.Diamonds)} +1 carta",
                                $"{C(Suit.Spades)} daño x1.5   {C(Suit.Hearts)} te cura   {C(Suit.Diamonds)} +1 carta",
                                $"{C(Suit.Spades)} dégâts x1.5   {C(Suit.Hearts)} te soigne   {C(Suit.Diamonds)} +1 carte",
                                $"{C(Suit.Spades)} Schaden x1.5   {C(Suit.Hearts)} heilt dich   {C(Suit.Diamonds)} +1 Karte",
                                $"{C(Suit.Spades)} danni x1.5   {C(Suit.Hearts)} ti cura   {C(Suit.Diamonds)} +1 carta");
            case "redraw":
            {
                // No toque, o botão é a carta com setas curvas (em cima do pulo)
                string key = touch ? Y(Loc.Pick("The card button", "O botão da carta", "El botón de la carta", "Le bouton carte", "Der Kartenknopf", "Il pulsante carta"))
                                   : Y(N(GameKey.Redraw));
                return Loc.Pick($"Bad hand? {key} draws a new one (3 per run)", $"Mão ruim? {key} compra uma nova (3 por partida)",
                                $"¿Mala mano? {key} roba una nueva (3 por partida)", $"Mauvaise main ? {key} en pioche une nouvelle (3 par partie)",
                                $"Schlechte Hand? {key} zieht eine neue (3 pro Lauf)", $"Mano scarsa? {key} ne pesca una nuova (3 a partita)");
            }
        }
        return id;
    }
}
