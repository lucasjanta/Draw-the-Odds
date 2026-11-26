using System;
using UnityEngine;

// RAISE / TRUCO: a aposta de risco do jogador. 1 uso por luta.
//  - Apertou RAISE: a PRÓXIMA carta arremessada vale a aposta.
//    Até 2 do número = dano x2. Errar por 6 ou mais = o player leva 1 de dano. No meio = normal.
//  - Às vezes o chefe responde RE-RAISE (aposta dobrada). O tempo para e o jogador escolhe:
//    CALL = dano x3 / leva 2 se errar feio, e o chefe fica mais rápido por alguns segundos;
//    FOLD = desiste (a aposta da luta é gasta, sem penalidade).
// Os números ficam em LuckyMath (RaiseMultiplier, RaiseNear, RaiseBustFrom).
public class RaiseController
{
    public enum Phase { Ready, Deciding, Armed, Spent }

    public Phase State { get; private set; } = Phase.Spent;
    public int Level { get; private set; }            // 1 = RAISE · 2 = CALL
    public bool Available => State == Phase.Ready;
    public bool Armed => State == Phase.Armed;

    public event Action OnChanged;

    // Começo de cada luta (o tutorial não tem RAISE).
    public void NewFight(bool enabled)
    {
        State = enabled ? Phase.Ready : Phase.Spent;
        Level = 0;
        OnChanged?.Invoke();
    }

    public void Disable()
    {
        State = Phase.Spent;
        Level = 0;
        OnChanged?.Invoke();
    }

    // Apertou RAISE. Devolve true se o chefe vai responder com RE-RAISE (quem chama abre a escolha).
    public bool Start(float reRaiseChance)
    {
        if (State != Phase.Ready) return false;
        Level = 1;
        bool reRaise = UnityEngine.Random.value < reRaiseChance;
        State = reRaise ? Phase.Deciding : Phase.Armed;
        OnChanged?.Invoke();
        return reRaise;
    }

    // Resposta ao RE-RAISE.
    public void Answer(bool call)
    {
        if (State != Phase.Deciding) return;
        if (call) { Level = 2; State = Phase.Armed; }
        else { Level = 0; State = Phase.Spent; }
        OnChanged?.Invoke();
    }

    // A carta arremessada leva a aposta junto (0 = sem aposta).
    public int ConsumeArmed()
    {
        if (State != Phase.Armed) return 0;
        int level = Level;
        State = Phase.Spent;
        Level = 0;
        OnChanged?.Invoke();
        return level;
    }

    // Textos (gritos): termos de pôquer no inglês, truco no português e no espanhol.
    public static string RaiseWord => Loc.Pick("RAISE!", "TRUCO!", "¡TRUCO!", "RELANCE !", "RAISE!", "RILANCIO!");
    public static string ReRaiseWord => Loc.Pick("RE-RAISE!", "SEIS!", "¡QUIERO RETRUCO!", "SURRELANCE !", "RE-RAISE!", "RILANCIO DOPPIO!");
    public static string CallWord => Loc.Pick("CALL", "DESCE", "QUIERO", "SUIVRE", "MITGEHEN", "VEDO");
    public static string FoldWord => Loc.Pick("FOLD", "CORRO", "NO QUIERO", "SE COUCHER", "PASSEN", "LASCIO");
    public static string ButtonWord => Loc.Pick("RAISE", "TRUCO", "TRUCO", "RELANCE", "RAISE", "RILANCIO");
}
