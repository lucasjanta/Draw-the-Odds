using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Ranking global (Supabase, REST). SQL: Supabase/draw_the_odds_setup.sql + _modes + _endless + _v2.
// v2 (27/11): 5 chefes, coringas e visual do jogador (chapéu.roupa.verso) em cada linha, e a melhor run do
// próprio jogador ("SEU RECORDE"). Enquanto o SQL v2 não for rodado no Supabase, tudo cai sozinho na
// versão antiga (sem visual/coringas) — e sem internet, num top 10 LOCAL (PlayerPrefs).
// A chave abaixo é a "publishable" (pública de propósito): só permite LER; gravar passa pelas funções
// dto_submit_*, que conferem se a run é plausível no servidor.
public static class LeaderboardClient
{
    private const string Url = "https://ujkwiczikyolhlhachlk.supabase.co";
    private const string Key = "sb_publishable_fGRiVmAvRI_qOyY2n5agfw_92gEte-4";
    private static string Platform => Portal.PlatformId; // itch / gamepix / crazygames / android
    public const int Size = 10;
    private const int TimeoutSeconds = 8;
    private const string LocalKey = "dto_local_top";      // + "_v2" (o top local antigo era de outra versão do jogo)
    private const string NameKey = "dto_player_name";

    // Uma linha do ranking (nomes iguais às colunas da view, para o JsonUtility).
    [Serializable]
    public class Entry
    {
        public string name;
        public int score;
        public int time_sec;
        public int cards_thrown;
        public int criticals;
        public int fusions;
        public int damage_taken;
        public int deaths;
        public string outfit = "";   // "chapéu.roupa.verso" (Wardrobe.Code)
        public string jokers = "";   // "0,5,13" (Jokers.Code)
        public int bosses;           // chefes vencidos
        public long rank;            // só na resposta do dto_player_best

        public List<JokerId> JokerList()
        {
            var list = new List<JokerId>();
            if (string.IsNullOrEmpty(jokers)) return list;
            foreach (var part in jokers.Split(','))
                if (int.TryParse(part, out int id) && id >= 0 && id < JokerCatalog.All.Length) list.Add((JokerId)id);
            return list;
        }
    }

    [Serializable] private class EntryList { public List<Entry> items = new List<Entry>(); }

    public static List<Entry> Entries { get; private set; } = new List<Entry>();
    public static string Mode { get; private set; } = "normal";   // modo da lista carregada
    public static bool Online { get; private set; }
    public static bool Legacy { get; private set; }               // servidor ainda sem o SQL v2
    public static Entry Best { get; private set; }                // melhor run do jogador neste modo (null = nenhuma)

    // Infinito: pontos = chefes vencidos × EndlessPerBoss + (99999 − segundos).
    public const string EndlessId = "endless";
    public const int EndlessPerBoss = 100000;
    public static int EndlessScore(int bossesBeaten, int timeSec) => bossesBeaten * EndlessPerBoss + Mathf.Max(0, 99999 - timeSec);

    public static string ModeId(Difficulty d) => d == Difficulty.Easy ? "easy" : d == Difficulty.Hardcore ? "hardcore" : "normal";

    public static string SavedName
    {
        get => PlayerPrefs.GetString(NameKey, "");
        set { PlayerPrefs.SetString(NameKey, value); PlayerPrefs.Save(); }
    }

    // MESMA fórmula do servidor (dto_score2 no SQL v2). Tempo pesa mais; cartas tiram; críticos somam.
    public static int Score(int timeSec, int cards, int crits) => Mathf.Max(1, 60000 - 8 * timeSec - 15 * cards + 200 * crits);

    // Posição (1..10) que esta pontuação pegaria na lista atual, ou -1 se não entra no top 10.
    public static int RankFor(int score)
    {
        for (int i = 0; i < Entries.Count; i++) if (score > Entries[i].score) return i + 1;
        return Entries.Count < Size ? Entries.Count + 1 : -1;
    }

    private static List<Entry> ParseList(string json) =>
        JsonUtility.FromJson<EntryList>("{\"items\":" + json + "}")?.items ?? new List<Entry>();

    // ===================== Leitura =====================
    // Top 10 do modo + a melhor run do jogador (Best).
    public static IEnumerator FetchTop(string mode, Action onDone)
    {
        Mode = mode;
        Best = null;
        if (!Portal.AllowExternalRequests)
        {
            UseLocal(mode);
            onDone?.Invoke();
            yield break;
        }
        const string v2Cols = "name,score,time_sec,cards_thrown,criticals,fusions,damage_taken,deaths,outfit,jokers,bosses";
        const string v1Cols = "name,score,time_sec,cards_thrown,criticals,fusions,damage_taken,deaths";
        string order = $"&order=score.desc,created_at.asc&limit={Size}&mode=eq.{mode}";

        long code = 0;
        string text = "";
        yield return Get($"/rest/v1/dto_top2?select={v2Cols}{order}", (c, t) => { code = c; text = t; });
        if (code == 200)
        {
            Legacy = false;
            Online = true;
            Entries = ParseList(text);
            yield return FetchBest(mode);
        }
        else if (code == 404 || code == 400)
        {
            // SQL v2 ainda não rodado: lista antiga (sem visual/coringas)
            Legacy = true;
            yield return Get($"/rest/v1/dto_top?select={v1Cols}{order}", (c, t) => { code = c; text = t; });
            Online = code == 200;
            if (Online) Entries = ParseList(text);
            else UseLocal(mode);
        }
        else
        {
            Debug.LogWarning($"Leaderboard: {code}");
            UseLocal(mode);
        }
        if (Best == null) Best = FindLocalBest(mode);
        onDone?.Invoke();
    }

    private static void UseLocal(string mode)
    {
        Entries = LoadLocal(mode);
        Online = false;
        Best = FindLocalBest(mode);
    }

    // Melhor run do jogador no servidor (dto_player_best) — o nome salvo neste aparelho.
    private static IEnumerator FetchBest(string mode)
    {
        string nick = SavedName;
        if (string.IsNullOrEmpty(nick)) yield break;
        string json = "{\"p_name\":\"" + nick.Replace("\\", "").Replace("\"", "") + "\",\"p_mode\":\"" + mode + "\"}";
        bool ok = false;
        string text = "";
        yield return Post(json, (o, c, t) => { ok = o; text = t; }, "dto_player_best");
        if (!ok) yield break;
        var list = ParseList(text);
        if (list.Count > 0) Best = list[0];
    }

    private static Entry FindLocalBest(string mode)
    {
        string nick = SavedName;
        if (string.IsNullOrEmpty(nick)) return null;
        var list = LoadLocal(mode);
        for (int i = 0; i < list.Count; i++)
            if (string.Equals(list[i].name, nick, StringComparison.OrdinalIgnoreCase)) { list[i].rank = i + 1; return list[i]; }
        return null;
    }

    private static IEnumerator Get(string path, Action<long, string> onDone)
    {
        using (var req = UnityWebRequest.Get(Url + path))
        {
            AddHeaders(req);
            yield return req.SendWebRequest();
            onDone(req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : "");
        }
    }

    // ===================== Envio =====================
    // onDone(ok, mensagem). Também guarda no top 10 local.
    public static IEnumerator Submit(string playerName, Entry run, string mode, Action<bool, string> onDone)
    {
        string nick = CleanName(playerName);
        if (!IsNameOk(nick)) { onDone?.Invoke(false, "INVALID NAME"); yield break; }
        SavedName = nick;
        run.name = nick;
        AddLocal(run, mode);
        if (!Portal.AllowExternalRequests)
        {
            yield return FetchTop(mode, null);
            onDone?.Invoke(true, "");
            yield break;
        }

        bool endless = mode == EndlessId;
        var common = new StringBuilder();
        common.Append("\"p_name\":\"").Append(nick.Replace("\\", "").Replace("\"", "")).Append("\",");
        common.Append("\"p_time\":").Append(run.time_sec).Append(',');
        common.Append("\"p_cards\":").Append(run.cards_thrown).Append(',');
        common.Append("\"p_crits\":").Append(run.criticals).Append(',');
        common.Append("\"p_fusions\":").Append(run.fusions).Append(',');
        common.Append("\"p_damage\":").Append(run.damage_taken).Append(',');
        common.Append("\"p_deaths\":").Append(run.deaths).Append(',');
        common.Append("\"p_platform\":\"").Append(Platform).Append('"');
        string extra = endless ? $",\"p_rounds\":{run.score / EndlessPerBoss}" : $",\"p_mode\":\"{mode}\"";
        string v2 = "{" + common + extra + $",\"p_outfit\":\"{Safe(run.outfit)}\",\"p_jokers\":\"{Safe(run.jokers)}\"" +
                    (endless ? "" : $",\"p_bosses\":{run.bosses}") + "}";
        string v1 = "{" + common + extra + "}";

        bool ok = false;
        string text = "";
        long code = 0;
        yield return Post(v2, (o, c, t) => { ok = o; code = c; text = t; }, endless ? "dto_submit_endless2" : "dto_submit_run2");
        // Servidor sem o SQL v2: envia na função antiga (sem visual/coringas)
        if (!ok && code == 404) yield return Post(v1, (o, c, t) => { ok = o; code = c; text = t; }, endless ? "dto_submit_endless" : "dto_submit_run");
        string error = "";
        if (!ok)
        {
            error = text.Contains("name") ? "INVALID NAME" : text.Contains("too fast") ? "WAIT A FEW SECONDS" : "OFFLINE - SAVED LOCALLY";
            Debug.LogWarning($"Leaderboard submit: {code} {text}");
        }
        yield return FetchTop(mode, null);
        onDone?.Invoke(ok, error);
    }

    private static string Safe(string s) => (s ?? "").Replace("\\", "").Replace("\"", "");

    private static IEnumerator Post(string json, Action<bool, long, string> onDone, string function)
    {
        using (var req = new UnityWebRequest(Url + "/rest/v1/rpc/" + function, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            AddHeaders(req);
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            onDone(req.result == UnityWebRequest.Result.Success, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : "");
        }
    }

    private static void AddHeaders(UnityWebRequest req)
    {
        req.timeout = TimeoutSeconds;
        req.SetRequestHeader("apikey", Key);
        req.SetRequestHeader("Authorization", "Bearer " + Key);
    }

    // ===================== Nomes =====================
    private static readonly string[] BadWords =
    {
        "fuck", "shit", "bitch", "nigg", "fag", "cunt", "porn", "sex", "nazi", "hitler", "dick", "cock", "pussy",
        "whore", "slut", "rape", "porra", "caralho", "buceta", "puta", "viado", "merda", "foda", "pinto", "cuzao",
        "arrombad", "piroca", "xoxota", "corno", "mierda", "puto", "coño", "pendej", "verga", "joder", "culo",
        "polla", "zorra", "marica",
    };

    // Só letras/números/espaço/_ . - (até 14 caracteres).
    public static string CleanName(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in (s ?? "").Trim())
        {
            bool latin = c >= 'À' && c <= 'ÿ';
            if (char.IsLetterOrDigit(c) && (c < 128 || latin) || c == ' ' || c == '_' || c == '.' || c == '-') sb.Append(c);
        }
        string r = sb.ToString();
        while (r.Contains("  ")) r = r.Replace("  ", " ");
        if (r.Length > 14) r = r.Substring(0, 14);
        return r.Trim();
    }

    public static bool IsNameOk(string s)
    {
        if (s.Length < 2) return false;
        string flat = s.ToLowerInvariant().Replace(" ", "").Replace(".", "").Replace("_", "").Replace("-", "")
            .Replace("0", "o").Replace("1", "i").Replace("3", "e").Replace("4", "a").Replace("5", "s").Replace("7", "t");
        foreach (var w in BadWords) if (flat.Contains(w)) return false;
        return true;
    }

    // ===================== Top 10 local =====================
    private static string LocalKeyFor(string mode) => LocalKey + "_v2_" + mode;

    private static List<Entry> LoadLocal(string mode)
    {
        var json = PlayerPrefs.GetString(LocalKeyFor(mode), "");
        if (string.IsNullOrEmpty(json)) return new List<Entry>();
        var list = JsonUtility.FromJson<EntryList>(json);
        return list?.items ?? new List<Entry>();
    }

    private static void AddLocal(Entry run, string mode)
    {
        var list = LoadLocal(mode);
        var same = list.Find(e => string.Equals(e.name, run.name, StringComparison.OrdinalIgnoreCase));
        if (same == null) list.Add(run);
        else if (run.score > same.score) list[list.IndexOf(same)] = run;
        list.Sort((a, b) => b.score.CompareTo(a.score));
        if (list.Count > Size) list.RemoveRange(Size, list.Count - Size);
        PlayerPrefs.SetString(LocalKeyFor(mode), JsonUtility.ToJson(new EntryList { items = list }));
        PlayerPrefs.Save();
    }
}
