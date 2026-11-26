using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Roteiro do tutorial com o miniboss Coringa, dividido em PASSOS (uma lista de coroutines).
// Cada passo mostra uma instrução e espera o jogador fazer a ação. A seta "◄" da caixa (ou Backspace)
// interrompe o passo atual e volta para o anterior; o Coringa recupera a vida para o tutorial não
// acabar antes da hora (e ele não morre enquanto o tutorial não chega na luta livre).
public class TutorialDirector : MonoBehaviour
{
    private GameManager gm;
    private GameUI ui;
    private PlayerController player;
    private BossController boss;
    private Coroutine slowRoutine;
    private Coroutine stepRoutine;
    private bool stepDone, backRequested;
    private int jumpTo = -1;                 // debug: pular direto para um passo
    private static TutorialDirector instance;

    // "Bandeiras" ligadas pelos eventos do jogo
    private bool jumped, dropped, dashed, hit, dieLanded, attackDone, redrew, fusionSuccess;
    private string landedAttack = "";

    private List<Func<IEnumerator>> steps;

    public void Run(GameManager gm, GameUI ui)
    {
        this.gm = gm;
        this.ui = ui;
        player = gm.Player;
        player.OnJumped += MarkJumped;
        player.OnDroppedThrough += MarkDropped;
        player.OnDashed += MarkDashed;
        gm.OnBossHit += MarkHit;
        gm.OnRedraw += MarkRedrew;
        gm.Fusion.OnEnded += MarkFusion;
        ui.OnPromptBack += RequestBack;
        instance = this;

        steps = new List<Func<IEnumerator>>
        {
            StepMove, StepJump, StepDrop, StepDash,
            StepBossIntro, StepCardsIntro, StepThrow, StepHints, StepCritical, StepSuits,
            StepAttack, StepFusion, StepFusionInfo, StepRedraw, StepPacks, StepFreeFight,
        };
        StartCoroutine(Runner());
    }

    private void OnDestroy()
    {
        if (gm == null) return;
        player.OnJumped -= MarkJumped;
        player.OnDroppedThrough -= MarkDropped;
        player.OnDashed -= MarkDashed;
        gm.OnBossHit -= MarkHit;
        gm.OnRedraw -= MarkRedrew;
        gm.Fusion.OnEnded -= MarkFusion;
        ui.OnPromptBack -= RequestBack;
        ui.CanGoBack = false;
        gm.Fusion.Unlimited = false;
        gm.InputLocked = false;
        if (boss != null) boss.CannotDie = false;
    }

    private void MarkJumped() => jumped = true;
    private void MarkDropped() => dropped = true;
    private void MarkDashed() => dashed = true;
    private void MarkHit(HitResult _) => hit = true;
    private void MarkRedrew() => redrew = true;
    private void MarkFusion(bool success) => fusionSuccess = success;
    private void RequestBack() => backRequested = true;

    // Texto de tecla destacado: [SPACE]
    private static string K(string key) => $"<color=#FFD447>[{key}]</color>";
    // Tecla configurada pelo jogador (Opções → CONTROLS)
    private static string K(GameKey key) => K(KeyBindings.Name(key));
    // Frase com a tecla de PAUSA: "P / ESC"
    private static string PauseKeys => KeyBindings.Name(GameKey.Pause) + " / ESC";
    // Controles de toque ligados: os textos falam dos botões da tela em vez das teclas.
    private static bool Touch => VirtualInput.Active;

    // ===================== Executor dos passos =====================
    private IEnumerator Runner()
    {
        yield return new WaitForSeconds(0.3f);
        int index = 0;
        while (index < steps.Count)
        {
            ui.CanGoBack = index > 0;
            stepDone = backRequested = false;
            stepRoutine = StartCoroutine(RunStep(steps[index]));

            while (!stepDone && !backRequested && jumpTo < 0)
            {
                // Backspace também volta (fora da pausa)
                var kb = Keyboard.current;
                if (index > 0 && !GameTime.Paused && kb != null && kb.backspaceKey.wasPressedThisFrame) backRequested = true;
                yield return null;
            }

            if (jumpTo >= 0)
            {
                StopCoroutine(stepRoutine);
                CleanupInterruptedStep();
                index = Mathf.Clamp(jumpTo, 0, steps.Count - 1);
                jumpTo = -1;
                yield return null;
                continue;
            }
            if (backRequested && !stepDone && index > 0)
            {
                StopCoroutine(stepRoutine); // para o passo no meio (e as coroutines aninhadas dele)
                CleanupInterruptedStep();
                index--;
                AudioManager.Ui(gm.Config.SfxUiClick);
                yield return null; // o clique na seta não vira arremesso
                continue;
            }
            index++;
        }
        ui.CanGoBack = false;
    }

    private IEnumerator RunStep(Func<IEnumerator> step)
    {
        yield return step();
        stepDone = true;
    }

    // Desfaz o que um passo interrompido pode ter deixado ligado.
    private void CleanupInterruptedStep()
    {
        StopSlow();
        if (gm.Fusion.Active) gm.Fusion.Cancel();
        gm.Fusion.Unlimited = false;
        gm.Pickups.ClearPowers();
        gm.InputLocked = false;
        FusionController.SetTimeScale(1f);
        ui.HidePrompt();
        ui.Story.Hide();
        if (boss != null)
        {
            boss.RestoreFullHp(); // volta com a vida cheia: o tutorial não termina antes da hora
            boss.CannotDie = true;
        }
    }

    // Liga/desliga ataques do chefe e os spawns automáticos (cada passo define o que quer).
    private void Setup(bool attacks, bool pickups)
    {
        if (boss != null)
        {
            boss.AttacksEnabled = attacks;
            boss.CannotDie = !pickups; // só pode morrer na luta livre
        }
        gm.Pickups.CardsEnabled = pickups;
        gm.Pickups.PowersEnabled = pickups;
    }

    // ===================== Passos =====================
    private IEnumerator StepMove()
    {
        Setup(false, false);
        string keys = Touch ? "II" : PauseKeys;
        string esc = $"\n<size=7><color=#8888AA>{keys}: " + Loc.Pick("pause / skip tutorial", "pausa / pular tutorial",
            "pausa / saltar tutorial", "pause / passer le tuto", "Pause / Tutorial überspringen", "pausa / salta tutorial") + "</color></size>";
        Say(Touch
            ? Loc.Pick($"{K("JOYSTICK")} (left side) moves{esc}", $"{K("JOYSTICK")} (lado esquerdo) anda{esc}",
                       $"{K("JOYSTICK")} (lado izquierdo) mueve{esc}", $"{K("JOYSTICK")} (à gauche) pour bouger{esc}",
                       $"{K("JOYSTICK")} (links) bewegt dich{esc}", $"{K("JOYSTICK")} (a sinistra) per muoverti{esc}")
            : Loc.Pick($"{K(GameKey.Left)} / {K(GameKey.Right)} move{esc}", $"{K(GameKey.Left)} / {K(GameKey.Right)} anda{esc}",
                       $"{K(GameKey.Left)} / {K(GameKey.Right)} moverse{esc}", $"{K(GameKey.Left)} / {K(GameKey.Right)} bouger{esc}",
                       $"{K(GameKey.Left)} / {K(GameKey.Right)} laufen{esc}", $"{K(GameKey.Left)} / {K(GameKey.Right)} muoversi{esc}"));
        float moved = 0f, lastX = player.transform.position.x;
        while (moved < 5f)
        {
            moved += Mathf.Abs(player.transform.position.x - lastX);
            lastX = player.transform.position.x;
            yield return null;
        }
    }

    private IEnumerator StepJump()
    {
        Setup(false, false);
        Say(Touch
            ? Loc.Pick($"{K("▲")} jump (hold = higher)", $"{K("▲")} pula (segurar = mais alto)", $"{K("▲")} salta (mantén = más alto)",
                       $"{K("▲")} saute (maintenir = plus haut)", $"{K("▲")} springen (halten = höher)", $"{K("▲")} salta (tieni premuto = più in alto)")
            : Loc.Pick($"{K(GameKey.Jump)} jump (hold = higher)", $"{K(GameKey.Jump)} pula (segurar = mais alto)", $"{K(GameKey.Jump)} salta (mantén = más alto)",
                       $"{K(GameKey.Jump)} saute (maintenir = plus haut)", $"{K(GameKey.Jump)} springen (halten = höher)", $"{K(GameKey.Jump)} salta (tieni premuto = più in alto)"));
        jumped = false;
        yield return Until(() => jumped);
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private IEnumerator StepDrop()
    {
        Setup(false, false);
        Say(Touch
            ? Loc.Pick($"On a platform: joystick {K("DOWN")} + {K("▲")} drops down", $"Na plataforma: joystick {K("BAIXO")} + {K("▲")} desce",
                       $"En una plataforma: joystick {K("ABAJO")} + {K("▲")} baja", $"Sur une plateforme : joystick {K("BAS")} + {K("▲")} descend",
                       $"Auf einer Plattform: Joystick {K("RUNTER")} + {K("▲")} fällt durch", $"Su una piattaforma: joystick {K("GIÙ")} + {K("▲")} scendi")
            : Loc.Pick($"On a platform: {K(GameKey.Down)} + {K(GameKey.Jump)} drops down", $"Na plataforma: {K(GameKey.Down)} + {K(GameKey.Jump)} desce",
                       $"En una plataforma: {K(GameKey.Down)} + {K(GameKey.Jump)} baja", $"Sur une plateforme : {K(GameKey.Down)} + {K(GameKey.Jump)} descend",
                       $"Auf einer Plattform: {K(GameKey.Down)} + {K(GameKey.Jump)} fällt durch", $"Su una piattaforma: {K(GameKey.Down)} + {K(GameKey.Jump)} scendi"));
        dropped = false;
        yield return Until(() => dropped);
        yield return new WaitForSecondsRealtime(0.6f);
    }

    private IEnumerator StepDash()
    {
        Setup(false, false);
        Say(Touch
            ? Loc.Pick($"{K("»")} dash (invincible!)", $"{K("»")} dash (invencível!)", $"{K("»")} dash (¡invencible!)",
                       $"{K("»")} dash (invincible !)", $"{K("»")} Dash (unverwundbar!)", $"{K("»")} dash (invincibile!)")
            : Loc.Pick($"{K(GameKey.Dash)} dash (invincible!)", $"{K(GameKey.Dash)} dash (invencível!)", $"{K(GameKey.Dash)} dash (¡invencible!)",
                       $"{K(GameKey.Dash)} dash (invincible !)", $"{K(GameKey.Dash)} Dash (unverwundbar!)", $"{K(GameKey.Dash)} dash (invincibile!)"));
        dashed = false;
        yield return Until(() => dashed);
        yield return new WaitForSecondsRealtime(0.9f);
        ui.HidePrompt();
    }

    private IEnumerator StepBossIntro()
    {
        if (boss == null)
        {
            boss = gm.SpawnBoss(gm.CurrentBossData);
            boss.OnDieLanded += label => { dieLanded = true; landedAttack = label; };
            boss.OnAttackFinished += () => attackDone = true;
            Setup(false, false);
            yield return new WaitForSeconds(1.8f);
            ui.HidePrompt();
            yield return gm.PlayStory(StoryLines.TutorialIntro); // o Coringa (bobo da corte) se apresenta
        }
        Setup(false, false);
        int lo = boss.Data.MinNumber, hi = boss.Data.MaxNumber;
        yield return Pause(Loc.Pick(
            $"Every boss hides a <color=#FFD447>SECRET NUMBER</color>.\nJoker: {lo} to {hi}. Find it!",
            $"Todo chefe esconde um <color=#FFD447>NÚMERO SECRETO</color>.\nCoringa: de {lo} a {hi}. Descubra!",
            $"Cada jefe esconde un <color=#FFD447>NÚMERO SECRETO</color>.\nComodín: de {lo} a {hi}. ¡Descúbrelo!",
            $"Chaque boss cache un <color=#FFD447>NOMBRE SECRET</color>.\nJoker : de {lo} à {hi}. Trouve-le !",
            $"Jeder Boss versteckt eine <color=#FFD447>GEHEIME ZAHL</color>.\nJoker: {lo} bis {hi}. Finde sie!",
            $"Ogni boss nasconde un <color=#FFD447>NUMERO SEGRETO</color>.\nJolly: da {lo} a {hi}. Trovalo!"));
    }

    private IEnumerator StepCardsIntro()
    {
        Setup(false, false);
        yield return Pause(Touch
            ? Loc.Pick($"{K("TAP")} a card to pick it\n{K("DRAG")} it up (or tap the screen) to throw",
                       $"{K("TOQUE")} numa carta para escolher\n{K("ARRASTE")} para cima (ou toque na tela) para arremessar",
                       $"{K("TOCA")} una carta para elegirla\n{K("ARRÁSTRALA")} hacia arriba (o toca la pantalla) para lanzarla",
                       $"{K("TOUCHE")} une carte pour la choisir\n{K("GLISSE")}-la vers le haut (ou touche l'écran) pour la lancer",
                       $"{K("TIPPE")} eine Karte an, um sie zu wählen\n{K("ZIEH")} sie nach oben (oder tippe den Bildschirm), um zu werfen",
                       $"{K("TOCCA")} una carta per sceglierla\n{K("TRASCINALA")} in alto (o tocca lo schermo) per lanciarla")
            : Loc.Pick($"{K("SCROLL")} / {K("1-0")} pick a card\n{K("LEFT CLICK")} throw it",
                       $"{K("RODINHA")} / {K("1-0")} escolhe a carta\n{K("CLIQUE ESQ.")} arremessa",
                       $"{K("RUEDA")} / {K("1-0")} elige una carta\n{K("CLIC IZQ.")} la lanza",
                       $"{K("MOLETTE")} / {K("1-0")} choisir une carte\n{K("CLIC GAUCHE")} la lancer",
                       $"{K("MAUSRAD")} / {K("1-0")} Karte wählen\n{K("LINKSKLICK")} werfen",
                       $"{K("ROTELLA")} / {K("1-0")} scegli una carta\n{K("CLIC SINISTRO")} lanciala"));
    }

    private IEnumerator StepThrow()
    {
        Setup(false, false);
        Say(Loc.Pick("Hit the Joker with a card!", "Acerte o Coringa com uma carta!", "¡Golpea al Comodín con una carta!",
                     "Touche le Joker avec une carte !", "Triff den Joker mit einer Karte!", "Colpisci il Jolly con una carta!"));
        hit = false;
        yield return Until(() => hit);
        yield return new WaitForSecondsRealtime(0.7f);
    }

    private IEnumerator StepHints()
    {
        Setup(false, false);
        yield return Pause(Loc.Pick("<color=#FF8A3D>▲ HIGHER</color>  <color=#5A9BFF>▼ LOWER</color>\nCloser = more damage.",
                                    "<color=#FF8A3D>▲ MAIOR</color>  <color=#5A9BFF>▼ MENOR</color>\nMais perto = mais dano.",
                                    "<color=#FF8A3D>▲ MAYOR</color>  <color=#5A9BFF>▼ MENOR</color>\nMás cerca = más daño.",
                                    "<color=#FF8A3D>▲ PLUS</color>  <color=#5A9BFF>▼ MOINS</color>\nPlus près = plus de dégâts.",
                                    "<color=#FF8A3D>▲ HÖHER</color>  <color=#5A9BFF>▼ TIEFER</color>\nNäher dran = mehr Schaden.",
                                    "<color=#FF8A3D>▲ PIÙ ALTO</color>  <color=#5A9BFF>▼ PIÙ BASSO</color>\nPiù vicino = più danni."));
    }

    // Ênfase pedida: acertar o número faz o chefe sortear OUTRO.
    private IEnumerator StepCritical()
    {
        Setup(false, false);
        yield return Pause(Loc.Pick(
            "<size=11><color=#FFD447>EXACT = CRITICAL!</color></size>\nThen the boss rolls a <color=#FF6A2E>NEW NUMBER</color>.",
            "<size=11><color=#FFD447>EXATO = CRÍTICO!</color></size>\nDepois o chefe sorteia um <color=#FF6A2E>NÚMERO NOVO</color>.",
            "<size=11><color=#FFD447>¡EXACTO = CRÍTICO!</color></size>\nLuego el jefe sortea un <color=#FF6A2E>NÚMERO NUEVO</color>.",
            "<size=11><color=#FFD447>EXACT = CRITIQUE !</color></size>\nPuis le boss tire un <color=#FF6A2E>NOUVEAU NOMBRE</color>.",
            "<size=11><color=#FFD447>GENAU = KRITISCH!</color></size>\nDann würfelt der Boss eine <color=#FF6A2E>NEUE ZAHL</color>.",
            "<size=11><color=#FFD447>ESATTO = CRITICO!</color></size>\nPoi il boss estrae un <color=#FF6A2E>NUOVO NUMERO</color>."));
    }

    private IEnumerator StepSuits()
    {
        Setup(false, false);
        string c = SuitInfo.Colored("♣", Suit.Clubs), s = SuitInfo.Colored("♠", Suit.Spades);
        string h = SuitInfo.Colored("♥", Suit.Hearts), d = SuitInfo.Colored("♦", Suit.Diamonds);
        yield return Pause(Loc.Pick(
            $"{c} normal  {s} x1.5 damage\n{h} heals you  {d} +1 card",
            $"{c} normal  {s} dano x1.5\n{h} cura você  {d} +1 carta",
            $"{c} normal  {s} daño x1.5\n{h} te cura  {d} +1 carta",
            $"{c} normal  {s} dégâts x1.5\n{h} te soigne  {d} +1 carte",
            $"{c} normal  {s} Schaden x1.5\n{h} heilt dich  {d} +1 Karte",
            $"{c} normale  {s} danni x1.5\n{h} ti cura  {d} +1 carta"));
    }

    // Primeiro ataque: quando o dado para, o tempo PARA e a dica explica o que o dado decide.
    private IEnumerator StepAttack()
    {
        dieLanded = attackDone = false;
        Setup(true, false);
        ui.HidePrompt();
        yield return Until(() => dieLanded);
        boss.AttacksEnabled = false; // depois deste ataque o chefe só passeia
        string attack = Loc.T(landedAttack);
        yield return Pause(Loc.Pick(
            $"The <color=#FFD447>DIE</color> picks the attack: <color=#FF6A2E>{attack}</color>\nDodge it!",
            $"O <color=#FFD447>DADO</color> escolhe o ataque: <color=#FF6A2E>{attack}</color>\nDesvie!",
            $"El <color=#FFD447>DADO</color> elige el ataque: <color=#FF6A2E>{attack}</color>\n¡Esquívalo!",
            $"Le <color=#FFD447>DÉ</color> choisit l'attaque : <color=#FF6A2E>{attack}</color>\nÉvite-la !",
            $"Der <color=#FFD447>WÜRFEL</color> wählt den Angriff: <color=#FF6A2E>{attack}</color>\nWeich aus!",
            $"Il <color=#FFD447>DADO</color> sceglie l'attacco: <color=#FF6A2E>{attack}</color>\nSchivalo!"));
        yield return Until(() => attackDone);
        yield return new WaitForSeconds(0.8f);
    }

    // Orbe de fusão: sem ataques e sem limite de tempo (dá para ler com calma).
    private IEnumerator StepFusion()
    {
        Setup(false, false);
        fusionSuccess = false;
        while (!fusionSuccess)
        {
            gm.Fusion.Unlimited = true;
            if (!gm.Pickups.HasPowerOnMap)
            {
                float side = player.transform.position.x > 0f ? -1f : 1f;
                Vector2 orbPos = new Vector2(Mathf.Clamp(player.transform.position.x + side * 4f, -17f, 17f), LevelBuilder.FloorTop + 0.9f);
                gm.Pickups.SpawnPowerAt(PowerType.Subtract, orbPos, 0f); // 0 = não some sozinho
            }
            Say(Loc.Pick("Grab the orb!", "Pegue o orbe!", "¡Coge el orbe!", "Prends l'orbe !", "Schnapp dir den Orb!", "Prendi la sfera!"));
            yield return Until(() => gm.Fusion.Active || gm.Hand.Count < 2);
            if (!gm.Fusion.Active) { gm.GiveCards(new[] { Card.Random(), Card.Random() }); continue; }

            ui.ShowPrompt(Touch
                ? Loc.Pick($"TIME STOPPED!\n{K("DRAG")} 2 cards into the slots\n{K("MERGE")} button merges",
                           $"TEMPO PARADO!\n{K("ARRASTE")} 2 cartas para os espaços\n{K("JUNTAR")} funde",
                           $"¡TIEMPO DETENIDO!\n{K("ARRASTRA")} 2 cartas a los huecos\n{K("FUSIONAR")} las fusiona",
                           $"TEMPS ARRÊTÉ !\n{K("GLISSE")} 2 cartes dans les emplacements\n{K("FUSIONNER")} les fusionne",
                           $"ZEIT ANGEHALTEN!\n{K("ZIEH")} 2 Karten in die Plätze\n{K("FUSION")} verschmilzt sie",
                           $"TEMPO FERMO!\n{K("TRASCINA")} 2 carte negli slot\n{K("FONDI")} le unisce")
                : Loc.Pick($"TIME STOPPED!\n{K(GameKey.SlotA)} / {K(GameKey.SlotB)} place 2 cards\n{K("LEFT CLICK")} merge",
                           $"TEMPO PARADO!\n{K(GameKey.SlotA)} / {K(GameKey.SlotB)} coloca 2 cartas\n{K("CLIQUE ESQ.")} funde",
                           $"¡TIEMPO DETENIDO!\n{K(GameKey.SlotA)} / {K(GameKey.SlotB)} coloca 2 cartas\n{K("CLIC IZQ.")} fusiona",
                           $"TEMPS ARRÊTÉ !\n{K(GameKey.SlotA)} / {K(GameKey.SlotB)} place 2 cartes\n{K("CLIC GAUCHE")} fusionne",
                           $"ZEIT ANGEHALTEN!\n{K(GameKey.SlotA)} / {K(GameKey.SlotB)} legt 2 Karten ab\n{K("LINKSKLICK")} verschmilzt",
                           $"TEMPO FERMO!\n{K(GameKey.SlotA)} / {K(GameKey.SlotB)} metti 2 carte\n{K("CLIC SINISTRO")} fondi"), false);
            yield return Until(() => !gm.Fusion.Active);
            ui.HidePrompt();
            yield return new WaitForSecondsRealtime(0.4f);
        }
        gm.Fusion.Unlimited = false;
    }

    private IEnumerator StepFusionInfo()
    {
        Setup(false, false);
        const string ops = "<color=#3CC86E>+</color>  <color=#E84A5F>−</color>  <color=#FFD447>×";
        yield return Pause(Loc.Pick(
            ops + " (rare)</color>\nIn fights you only get a few seconds!",
            ops + " (raro)</color>\nNas lutas você só tem alguns segundos!",
            ops + " (raro)</color>\n¡En combate solo tienes unos segundos!",
            ops + " (rare)</color>\nEn combat, tu n'as que quelques secondes !",
            ops + " (selten)</color>\nIm Kampf hast du nur ein paar Sekunden!",
            ops + " (raro)</color>\nIn battaglia hai solo pochi secondi!"));
    }

    private IEnumerator StepRedraw()
    {
        Setup(false, false);
        // No toque, o botão é a carta com setas (em cima do pulo)
        string key = Touch ? Loc.Pick("The card button", "O botão da carta", "El botón de la carta", "Le bouton carte", "Der Kartenknopf", "Il pulsante carta")
                           : K(GameKey.Redraw);
        Say(Loc.Pick($"{key}: new hand (3 per run). Try it!", $"{key}: mão nova (3 por partida). Tente!",
                     $"{key}: mano nueva (3 por partida). ¡Pruébalo!", $"{key} : nouvelle main (3 par partie). Essaie !",
                     $"{key}: neue Hand (3 pro Lauf). Probier's!", $"{key}: nuova mano (3 a partita). Provalo!"));
        redrew = false;
        yield return Until(() => redrew);
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private IEnumerator StepPacks()
    {
        Setup(false, false);
        yield return Pause(Loc.Pick("Low on cards? Grab the card packs.", "Poucas cartas? Pegue os pacotes.", "¿Pocas cartas? Coge los paquetes.",
                                    "Plus beaucoup de cartes ? Prends les paquets.", "Wenig Karten? Schnapp dir die Kartenpacks.", "Poche carte? Prendi i pacchetti."));
    }

    private IEnumerator StepFreeFight()
    {
        Setup(true, true);
        Say(Loc.Pick("Defeat the Joker!", "Derrote o Coringa!", "¡Derrota al Comodín!", "Bats le Joker !", "Besiege den Joker!", "Sconfiggi il Jolly!"));
        yield return new WaitForSecondsRealtime(3.5f);
        ui.HidePrompt();
    }

    // Debug (ponte do Editor): call TutorialDirector.DebugStep 10
    public static string DebugStep(string n)
    {
        if (instance == null) return "no tutorial";
        instance.jumpTo = int.Parse(n);
        if (instance.boss == null && instance.jumpTo > 4) instance.jumpTo = 4; // o chefe surge no passo 4
        return "jump " + instance.jumpTo;
    }

    // ===================== Ajudantes =====================
    private static IEnumerator Until(Func<bool> condition)
    {
        while (!condition()) yield return null;
    }

    // Mostra uma instrução de ação com um "respiro" em câmera lenta.
    private void Say(string text)
    {
        ui.ShowPrompt(text, false);
        StopSlow();
        slowRoutine = StartCoroutine(SlowPulse());
    }

    private IEnumerator SlowPulse()
    {
        FusionController.SetTimeScale(0.3f);
        yield return new WaitForSecondsRealtime(0.5f);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
        {
            if (!gm.Fusion.Active) FusionController.SetTimeScale(Mathf.Lerp(0.3f, 1f, t));
            yield return null;
        }
        if (!gm.Fusion.Active) FusionController.SetTimeScale(1f);
        slowRoutine = null;
    }

    private void StopSlow()
    {
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = null;
    }

    // Para o tempo (inclusive ataques no meio), mostra a explicação e espera um clique.
    private IEnumerator Pause(string text)
    {
        StopSlow();
        gm.InputLocked = true;
        FusionController.SetTimeScale(0f);
        ui.ShowPrompt(text, true);
        yield return new WaitForSecondsRealtime(0.5f);

        while (true)
        {
            if (GameTime.Paused) { yield return null; continue; } // clique no menu de pausa não conta
            var kb = Keyboard.current;
            // Clique/toque em cima de um botão (a seta de voltar) não conta como "continuar".
            // (Toque no celular também vale: VirtualInput.AnyPressThisFrame olha mouse e tela.)
            bool pressed = VirtualInput.AnyPressThisFrame(out Vector2 at) && !VirtualInput.OverInteractive(at);
            if (pressed || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)))
                break;
            yield return null;
        }

        ui.HidePrompt();
        FusionController.SetTimeScale(1f);
        yield return null; // espera 1 frame para o clique não virar arremesso
        gm.InputLocked = false;
    }
}
