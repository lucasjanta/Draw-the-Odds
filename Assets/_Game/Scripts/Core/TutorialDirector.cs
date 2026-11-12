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
        const string esc = "\n<size=7><color=#8888AA>ESC: pause menu (skip the tutorial there)</color></size>";
        Say($"Welcome to DRAW THE ODDS!\n{K("A")} / {K("D")} to move.{esc}");
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
        Say($"{K("SPACE")} to jump.\nHold it to jump higher!");
        jumped = false;
        yield return Until(() => jumped);
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private IEnumerator StepDrop()
    {
        Setup(false, false);
        Say($"Jump onto a platform, then press\n{K("S")} + {K("SPACE")} to drop down through it.");
        dropped = false;
        yield return Until(() => dropped);
        yield return new WaitForSecondsRealtime(0.6f);
    }

    private IEnumerator StepDash()
    {
        Setup(false, false);
        Say($"{K("SHIFT")} to DASH.\nYou can't be hit while dashing, but it has a cooldown (top-left bar).");
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
        yield return Pause($"This is the <color=#73D8FF>JOKER</color>. Every boss hides a <color=#FFD447>SECRET NUMBER</color>.\n" +
                           $"The Joker's number is between {boss.Data.MinNumber} and {boss.Data.MaxNumber}. Find it with your cards!");
    }

    private IEnumerator StepCardsIntro()
    {
        Setup(false, false);
        yield return Pause($"Your cards are in the bottom-left corner.\n" +
                           $"{K("SCROLL")} picks a card (or keys 1-0).\n{K("LEFT CLICK")} throws it toward the mouse.");
    }

    private IEnumerator StepThrow()
    {
        Setup(false, false);
        Say("Throw a card at the Joker!\n<size=8>Aim well: the card only curves a little toward the boss.</size>");
        hit = false;
        yield return Until(() => hit);
        yield return new WaitForSecondsRealtime(0.7f);
    }

    private IEnumerator StepHints()
    {
        Setup(false, false);
        yield return Pause("Every hit gives you a HINT (top-right corner):\n" +
                           "<color=#FF8A3D>▲</color> the number is HIGHER than your card\n" +
                           "<color=#5A9BFF>▼</color> the number is LOWER\n" +
                           "Closer guesses deal more damage. Warm colors = close!");
    }

    // Ênfase pedida: acertar o número faz o chefe sortear OUTRO.
    private IEnumerator StepCritical()
    {
        Setup(false, false);
        yield return Pause("<size=11><color=#FFD447>HIT THE EXACT NUMBER = CRITICAL!</color></size>\n" +
                           "Huge damage... but then the boss <color=#FF6A2E>ROLLS A NEW SECRET NUMBER</color>!\n" +
                           "<color=#FF6A2E>Your old hints are useless after that</color>: start guessing again.");
    }

    private IEnumerator StepSuits()
    {
        Setup(false, false);
        yield return Pause($"Suits matter too:\n" +
                           $"{SuitInfo.Colored("♣ CLUBS", Suit.Clubs)} normal damage\n{SuitInfo.Colored("♠ SPADES", Suit.Spades)} x1.5 damage\n" +
                           $"{SuitInfo.Colored("♥ HEARTS", Suit.Hearts)} heal YOU (only if close to the number!)\n" +
                           $"{SuitInfo.Colored("♦ DIAMONDS", Suit.Diamonds)} damage + draw a card");
    }

    // Primeiro ataque: quando o dado para, o tempo PARA e a dica explica o que o dado decide.
    private IEnumerator StepAttack()
    {
        dieLanded = attackDone = false;
        Setup(true, false);
        ui.HidePrompt();
        yield return Until(() => dieLanded);
        boss.AttacksEnabled = false; // depois deste ataque o chefe só passeia
        yield return Pause("The <color=#FFD447>DIE</color> above the boss decides <color=#FFD447>WHICH ATTACK</color> comes next\n" +
                           $"(each face is a different attack, or a stronger version of one).\nThis roll: <color=#FF6A2E>{landedAttack}</color>\n" +
                           $"Dodge it: jump, drop down or {K("SHIFT")} dash!");
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
            Say("Power orbs appear around the arena.\nGrab the glowing orb!");
            yield return Until(() => gm.Fusion.Active || gm.Hand.Count < 2);
            if (!gm.Fusion.Active) { gm.GiveCards(new[] { Card.Random(), Card.Random() }); continue; }

            ui.ShowPrompt($"TIME IS STOPPED (no time limit here)!\n{K("SCROLL")} pick a card, {K("Q")} / {K("E")} place it in a slot.\n" +
                          $"{K("LEFT CLICK")} merge   {K("RIGHT CLICK")} skip the orb\nThe result is only revealed after merging!", false);
            yield return Until(() => !gm.Fusion.Active);
            ui.HidePrompt();
            yield return new WaitForSecondsRealtime(0.4f);
        }
        gm.Fusion.Unlimited = false;
    }

    private IEnumerator StepFusionInfo()
    {
        Setup(false, false);
        yield return Pause("Orbs: <color=#3CC86E>+ ADD</color>   <color=#E84A5F>− SUBTRACT</color>   <color=#FFD447>× MULTIPLY</color> (rare!)\n" +
                           "Mixing two suits gives one of them at random.\n" +
                           "In the real fights you only have a few seconds, and orbs fade away if you leave them.");
    }

    private IEnumerator StepRedraw()
    {
        Setup(false, false);
        Say($"Bad hand? {K("R")} throws it away and draws a new one.\nOnly 3 redraws for the WHOLE RUN! Try it now.");
        redrew = false;
        yield return Until(() => redrew);
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private IEnumerator StepPacks()
    {
        Setup(false, false);
        yield return Pause("Card packs drop when your hand runs low.\nRemember: EXACT hit = critical, then a <color=#FF6A2E>NEW NUMBER</color>!");
    }

    private IEnumerator StepFreeFight()
    {
        Setup(true, true);
        Say("Now defeat the Joker!");
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
