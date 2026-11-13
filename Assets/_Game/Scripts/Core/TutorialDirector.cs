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
        string esc = Loc.Pick("\n<size=7><color=#8888AA>ESC: pause / skip tutorial</color></size>",
                              "\n<size=7><color=#8888AA>ESC: pausa / pular tutorial</color></size>");
        Say(Loc.Pick($"{K("A")} / {K("D")} move{esc}", $"{K("A")} / {K("D")} anda{esc}"));
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
        Say(Loc.Pick($"{K("SPACE")} jump (hold = higher)", $"{K("ESPAÇO")} pula (segurar = mais alto)"));
        jumped = false;
        yield return Until(() => jumped);
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private IEnumerator StepDrop()
    {
        Setup(false, false);
        Say(Loc.Pick($"On a platform: {K("S")} + {K("SPACE")} drops down", $"Na plataforma: {K("S")} + {K("ESPAÇO")} desce"));
        dropped = false;
        yield return Until(() => dropped);
        yield return new WaitForSecondsRealtime(0.6f);
    }

    private IEnumerator StepDash()
    {
        Setup(false, false);
        Say(Loc.Pick($"{K("SHIFT")} dash (invincible!)", $"{K("SHIFT")} dash (invencível!)"));
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
        }
        Setup(false, false);
        yield return Pause(Loc.Pick(
            $"Every boss hides a <color=#FFD447>SECRET NUMBER</color>.\nJoker: {boss.Data.MinNumber} to {boss.Data.MaxNumber}. Find it!",
            $"Todo chefe esconde um <color=#FFD447>NÚMERO SECRETO</color>.\nCoringa: de {boss.Data.MinNumber} a {boss.Data.MaxNumber}. Descubra!"));
    }

    private IEnumerator StepCardsIntro()
    {
        Setup(false, false);
        yield return Pause(Loc.Pick($"{K("SCROLL")} / {K("1-0")} pick a card\n{K("LEFT CLICK")} throw it",
                                    $"{K("RODINHA")} / {K("1-0")} escolhe a carta\n{K("CLIQUE ESQ.")} arremessa"));
    }

    private IEnumerator StepThrow()
    {
        Setup(false, false);
        Say(Loc.Pick("Hit the Joker with a card!", "Acerte o Coringa com uma carta!"));
        hit = false;
        yield return Until(() => hit);
        yield return new WaitForSecondsRealtime(0.7f);
    }

    private IEnumerator StepHints()
    {
        Setup(false, false);
        yield return Pause(Loc.Pick("<color=#FF8A3D>▲ HIGHER</color>  <color=#5A9BFF>▼ LOWER</color>\nCloser = more damage.",
                                    "<color=#FF8A3D>▲ MAIOR</color>  <color=#5A9BFF>▼ MENOR</color>\nMais perto = mais dano."));
    }

    // Ênfase pedida: acertar o número faz o chefe sortear OUTRO.
    private IEnumerator StepCritical()
    {
        Setup(false, false);
        yield return Pause(Loc.Pick(
            "<size=11><color=#FFD447>EXACT = CRITICAL!</color></size>\nThen the boss rolls a <color=#FF6A2E>NEW NUMBER</color>.",
            "<size=11><color=#FFD447>EXATO = CRÍTICO!</color></size>\nDepois o chefe sorteia um <color=#FF6A2E>NÚMERO NOVO</color>."));
    }

    private IEnumerator StepSuits()
    {
        Setup(false, false);
        yield return Pause(Loc.Pick(
            $"{SuitInfo.Colored("♣", Suit.Clubs)} normal  {SuitInfo.Colored("♠", Suit.Spades)} x1.5 damage\n" +
            $"{SuitInfo.Colored("♥", Suit.Hearts)} heals you  {SuitInfo.Colored("♦", Suit.Diamonds)} +1 card",
            $"{SuitInfo.Colored("♣", Suit.Clubs)} normal  {SuitInfo.Colored("♠", Suit.Spades)} dano x1.5\n" +
            $"{SuitInfo.Colored("♥", Suit.Hearts)} cura você  {SuitInfo.Colored("♦", Suit.Diamonds)} +1 carta"));
    }

    // Primeiro ataque: quando o dado para, o tempo PARA e a dica explica o que o dado decide.
    private IEnumerator StepAttack()
    {
        dieLanded = attackDone = false;
        Setup(true, false);
        ui.HidePrompt();
        yield return Until(() => dieLanded);
        boss.AttacksEnabled = false; // depois deste ataque o chefe só passeia
        yield return Pause(Loc.Pick(
            $"The <color=#FFD447>DIE</color> picks the attack: <color=#FF6A2E>{landedAttack}</color>\nDodge it!",
            $"O <color=#FFD447>DADO</color> escolhe o ataque: <color=#FF6A2E>{Loc.T(landedAttack)}</color>\nDesvie!"));
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
            Say(Loc.Pick("Grab the orb!", "Pegue o orbe!"));
            yield return Until(() => gm.Fusion.Active || gm.Hand.Count < 2);
            if (!gm.Fusion.Active) { gm.GiveCards(new[] { Card.Random(), Card.Random() }); continue; }

            ui.ShowPrompt(Loc.Pick($"TIME STOPPED!\n{K("Q")} / {K("E")} place 2 cards\n{K("LEFT CLICK")} merge",
                                   $"TEMPO PARADO!\n{K("Q")} / {K("E")} coloca 2 cartas\n{K("CLIQUE ESQ.")} funde"), false);
            yield return Until(() => !gm.Fusion.Active);
            ui.HidePrompt();
            yield return new WaitForSecondsRealtime(0.4f);
        }
        gm.Fusion.Unlimited = false;
    }

    private IEnumerator StepFusionInfo()
    {
        Setup(false, false);
        yield return Pause(Loc.Pick(
            "<color=#3CC86E>+</color>  <color=#E84A5F>−</color>  <color=#FFD447>× (rare)</color>\nIn fights you only get a few seconds!",
            "<color=#3CC86E>+</color>  <color=#E84A5F>−</color>  <color=#FFD447>× (raro)</color>\nNas lutas você só tem alguns segundos!"));
    }

    private IEnumerator StepRedraw()
    {
        Setup(false, false);
        Say(Loc.Pick($"{K("R")} new hand (3 per run). Try it!", $"{K("R")} mão nova (3 por partida). Tente!"));
        redrew = false;
        yield return Until(() => redrew);
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private IEnumerator StepPacks()
    {
        Setup(false, false);
        yield return Pause(Loc.Pick("Low on cards? Grab the card packs.", "Poucas cartas? Pegue os pacotes."));
    }

    private IEnumerator StepFreeFight()
    {
        Setup(true, true);
        Say(Loc.Pick("Defeat the Joker!", "Derrote o Coringa!"));
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
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            // Clique em cima de um botão (a seta de voltar) não conta como "continuar"
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if ((mouse != null && mouse.leftButton.wasPressedThisFrame && !overUi) ||
                (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)))
                break;
            yield return null;
        }

        ui.HidePrompt();
        FusionController.SetTimeScale(1f);
        yield return null; // espera 1 frame para o clique não virar arremesso
        gm.InputLocked = false;
    }
}
