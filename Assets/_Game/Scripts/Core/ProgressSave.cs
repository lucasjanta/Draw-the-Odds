using System;
using UnityEngine;

// Progresso que fica salvo ENTRE as runs (PlayerPrefs): fichas, coringas liberados na taverna,
// versos de carta, boatos descobertos e finais vistos.
// Tudo num lugar só para, no futuro, trocar pelo save do portal (CrazyGames/Poki) sem mexer no resto.
public static class ProgressSave
{
    private const string ChipsKey = "dto_chips";
    private const string JokersKey = "dto_jokers_owned";   // bits: 1 por JokerId
    private const string RumorsKey = "dto_rumors";         // bits: 1 por boato
    private const string BacksKey = "dto_backs_owned";     // bits: 1 por verso de carta
    private const string BackKey = "dto_back";             // verso escolhido
    private const string EndingsKey = "dto_endings";       // bits: 1 = trono, 2 = livre

    public static event Action OnChanged;

    // ===================== Fichas (moeda) =====================
    public static int Chips => PlayerPrefs.GetInt(ChipsKey, 0);

    public static void AddChips(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(ChipsKey, Chips + amount);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static bool SpendChips(int amount)
    {
        if (amount < 0 || Chips < amount) return false;
        PlayerPrefs.SetInt(ChipsKey, Chips - amount);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        return true;
    }

    // ===================== Bits genéricos =====================
    private static bool GetBit(string key, int bit, int defaults = 0) => (PlayerPrefs.GetInt(key, defaults) & (1 << bit)) != 0;

    private static void SetBit(string key, int bit, int defaults = 0)
    {
        PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, defaults) | (1 << bit));
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    // ===================== Coringas liberados (taverna) =====================
    // Os coringas "iniciais" já vêm liberados; os outros são comprados com fichas.
    private static int StarterMask
    {
        get
        {
            int mask = 0;
            foreach (var def in JokerCatalog.All) if (def.cost == 0) mask |= 1 << (int)def.id;
            return mask;
        }
    }

    public static bool JokerOwned(JokerId id) => GetBit(JokersKey, (int)id, StarterMask);
    public static void UnlockJoker(JokerId id) => SetBit(JokersKey, (int)id, StarterMask);

    // ===================== Versos de carta (cosmético) =====================
    public static bool BackOwned(int index) => index == 0 || GetBit(BacksKey, index);
    public static void UnlockBack(int index) => SetBit(BacksKey, index);

    public static int SelectedBack
    {
        get => PlayerPrefs.GetInt(BackKey, 0);
        set { PlayerPrefs.SetInt(BackKey, value); PlayerPrefs.Save(); OnChanged?.Invoke(); }
    }

    // ===================== Boatos (Mural) =====================
    public static bool RumorFound(int index) => GetBit(RumorsKey, index);
    public static void FindRumor(int index) => SetBit(RumorsKey, index);

    // O Ás secreto aparece depois do Rei quando os 3 boatos do Rei foram descobertos.
    public static bool AceUnlocked
    {
        get
        {
            for (int i = 0; i < Rumors.PerBoss; i++) if (!RumorFound(Rumors.Index(2, i))) return false;
            return true;
        }
    }

    // ===================== Finais =====================
    public static bool EndingSeen(int ending) => GetBit(EndingsKey, ending);
    public static void MarkEnding(int ending) => SetBit(EndingsKey, ending);

    // Debug (ponte do Editor): call ProgressSave.DebugChips 500 · DebugReset
    public static string DebugChips(string amount) { AddChips(int.Parse(amount)); return "chips " + Chips; }

    public static string DebugReset()
    {
        foreach (var k in new[] { ChipsKey, JokersKey, RumorsKey, BacksKey, BackKey, EndingsKey }) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        return "progress reset";
    }

    // Debug: descobre todos os boatos do Rei (libera o Ás).
    public static string DebugUnlockAce()
    {
        for (int i = 0; i < Rumors.PerBoss; i++) FindRumor(Rumors.Index(2, i));
        return "ace unlocked " + AceUnlocked;
    }
}
