using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Roteiro do tutorial com o miniboss Ás. Cada passo mostra uma instrução e espera o jogador
// fazer a ação. Passos de movimento começam em câmera lenta; explicações longas param o tempo
// e esperam um clique.
public class TutorialDirector : MonoBehaviour
{
    private GameManager gm;
    private GameUI ui;
    private PlayerController player;
    private Coroutine slowRoutine;

    // "Bandeiras" ligadas pelos eventos do jogo
    private bool jumped, dropped, dashed, hit, telegraph, attackDone, redrew, fusionSuccess;

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
        StartCoroutine(Script());
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
        gm.InputLocked = false;
    }

    private void MarkJumped() => jumped = true;
    private void MarkDropped() => dropped = true;
    private void MarkDashed() => dashed = true;
    private void MarkHit(HitResult _) => hit = true;
    private void MarkRedrew() => redrew = true;
    private void MarkFusion(bool success) => fusionSuccess = success;

    // Texto de tecla destacado: [SPACE]
    private static string K(string key) => $"<color=#FFD447>[{key}]</color>";

    private IEnumerator Script()
    {
        yield return new WaitForSeconds(0.3f);
        const string esc = "\n<size=8><color=#8888AA>ESC skips the tutorial</color></size>";

        // ---------- Movimento ----------
        Say($"Welcome to DRAW THE ODDS!\n{K("A")} / {K("D")} to move.{esc}");
        float moved = 0f, lastX = player.transform.position.x;
        while (moved < 5f)
        {
            moved += Mathf.Abs(player.transform.position.x - lastX);
            lastX = player.transform.position.x;
            yield return null;
        }

        Say($"{K("SPACE")} to jump.\nHold it to jump higher!");
        jumped = false;
        yield return Until(() => jumped);
        yield return new WaitForSecondsRealtime(0.8f);

        Say($"Jump onto a platform, then press\n{K("S")} + {K("SPACE")} to drop down through it.");
        dropped = false;
        yield return Until(() => dropped);
        yield return new WaitForSecondsRealtime(0.6f);

        Say($"{K("SHIFT")} to DASH.\nYou can't be hit while dashing, but it has a cooldown (top-left bar).");
        dashed = false;
        yield return Until(() => dashed);
        yield return new WaitForSecondsRealtime(0.9f);
        ui.HidePrompt();

        // ---------- O chefe e o número secreto ----------
        var boss = gm.SpawnBoss(gm.CurrentBossData);
        boss.AttacksEnabled = false;
        boss.OnTelegraph += () => telegraph = true;
        boss.OnAttackFinished += () => attackDone = true;
        yield return new WaitForSeconds(1.8f);

        yield return Pause($"This is the <color=#73D8FF>ACE</color>. Every boss hides a <color=#FFD447>SECRET NUMBER</color>.\n" +
                           $"The Ace's number is between {boss.Data.MinNumber} and {boss.Data.MaxNumber}. Find it with your cards!");
        yield return Pause($"Your cards are at the bottom of the screen.\n" +
                           $"{K("SCROLL")} picks a card (or keys 1-0).  {K("LEFT CLICK")} throws it toward the mouse.");

        Say("Throw a card at the Ace!\n<size=9>Aim well: the card only curves a little toward the boss.</size>");
        hit = false;
        yield return Until(() => hit);
        yield return new WaitForSecondsRealtime(0.7f);

        yield return Pause("Every hit gives you a HINT (top-right corner):\n" +
                           "<color=#FF8A3D>▲</color> the number is HIGHER than your card   " +
                           "<color=#5A9BFF>▼</color> the number is LOWER\n" +
                           "Closer guesses deal more damage. Warm colors = close!");
        yield return Pause($"Suits matter too:\n" +
                           $"{SuitInfo.Colored("♣ CLUBS", Suit.Clubs)} normal damage    {SuitInfo.Colored("♠ SPADES", Suit.Spades)} x1.5 damage\n" +
                           $"{SuitInfo.Colored("♥ HEARTS", Suit.Hearts)} heal YOU    {SuitInfo.Colored("♦ DIAMONDS", Suit.Diamonds)} damage + draw a card");

        // ---------- Ataques ----------
        telegraph = attackDone = false;
        boss.AttacksEnabled = true;
        yield return Until(() => telegraph);
        StopSlow();
        FusionController.SetTimeScale(0.2f);
        ui.ShowPrompt($"Watch out! The die above the boss shows HOW MANY dice are coming.\nDodge them: jump or {K("SHIFT")} dash!", false);
        yield return new WaitForSecondsRealtime(1.8f);
        FusionController.SetTimeScale(1f);
        yield return Until(() => attackDone);
        boss.AttacksEnabled = false; // calma durante as próximas lições
        yield return new WaitForSeconds(0.8f);
        ui.HidePrompt();

        // ---------- Fusão ----------
        fusionSuccess = false;
        while (!fusionSuccess)
        {
            float side = player.transform.position.x > 0f ? -1f : 1f;
            Vector2 orbPos = new Vector2(Mathf.Clamp(player.transform.position.x + side * 4f, -17f, 17f), LevelBuilder.FloorTop + 0.9f);
            gm.Pickups.SpawnPowerAt(PowerType.Subtract, orbPos);
            Say("Power orbs appear around the arena.\nGrab the glowing orb!");
            yield return Until(() => gm.Fusion.Active || gm.Hand.Count < 2);
            if (!gm.Fusion.Active) { gm.GiveCards(new[] { Card.Random(), Card.Random() }); continue; }

            ui.ShowPrompt($"TIME IS STOPPED! {K("SCROLL")} pick a card, {K("Q")} / {K("E")} place it in a slot,\n" +
                          $"{K("RIGHT CLICK")} to merge. The result is only revealed after merging!", false, true);
            yield return Until(() => !gm.Fusion.Active);
            ui.HidePrompt();
            yield return new WaitForSecondsRealtime(0.4f);
        }
        yield return Pause("Orbs: <color=#3CC86E>+ ADD</color>   <color=#E84A5F>− SUBTRACT</color>   " +
                           "<color=#FFD447>× MULTIPLY</color> (rare!)\nMixing two suits gives one of them at random.");

        // ---------- Redraw ----------
        Say($"Bad hand? {K("R")} throws it away and draws a new one\n(3 times per boss). Try it now!");
        redrew = false;
        yield return Until(() => redrew);
        yield return new WaitForSecondsRealtime(0.8f);

        yield return Pause("Card packs drop when your hand runs low.\n" +
                           "Hit the <color=#FFD447>EXACT</color> number for a CRITICAL: big damage, and the boss picks a NEW number.");

        // ---------- Luta livre ----------
        gm.Pickups.CardsEnabled = true;
        gm.Pickups.PowersEnabled = true;
        boss.AttacksEnabled = true;
        Say("Now defeat the Ace!");
        yield return new WaitForSecondsRealtime(3.5f);
        ui.HidePrompt();
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

    // Para o tempo, mostra a explicação e espera um clique.
    private IEnumerator Pause(string text)
    {
        StopSlow();
        gm.InputLocked = true;
        FusionController.SetTimeScale(0f);
        ui.ShowPrompt(text, true);
        yield return new WaitForSecondsRealtime(0.5f);

        while (true)
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if ((mouse != null && mouse.leftButton.wasPressedThisFrame) ||
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
