using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Ranking global (Supabase, REST). Tabela/função/view: Supabase/draw_the_odds_setup.sql
// + Supabase/draw_the_odds_modes.sql (um ranking por modo: easy / normal / hardcore).
// Sem internet (ou antes do SQL ser rodado) tudo funciona com um top 10 LOCAL (PlayerPrefs).
// A chave abaixo é a "publishable" (pública de propósito): só permite LER; gravar passa pela
// função dto_submit_run, que confere se a run é plausível no servidor.
public static class LeaderboardClient
{
    private const string Url = "https://ujkwiczikyolhlhachlk.supabase.co";
    private const string Key = "sb_publishable_fGRiVmAvRI_qOyY2n5agfw_92gEte-4";
    private const string Platform = "itch";
    public const int Size = 10;
    private const int TimeoutSeconds = 8;
    private const string LocalKey = "dto_local_top";
    private const string NameKey = "dto_player_name";

    // Uma linha do ranking (nomes iguais às colunas da view dto_top, para o JsonUtility).
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
    }

    [Serializable] private class EntryList { public List<Entry> items = new List<Entry>(); }

    public static List<Entry> Entries { get; private set; } = new List<Entry>();
    public static string Mode { get; private set; } = "normal";   // modo da lista carregada

    // Nome do modo como o servidor usa ("easy" | "normal" | "hardcore").
    public static string ModeId(Difficulty d) => d == Difficulty.Easy ? "easy" : d == Difficulty.Hardcore ? "hardcore" : "normal";
    public static bool Online { get; private set; }

    public static string SavedName
    {
        get => PlayerPrefs.GetString(NameKey, "");
        set { PlayerPrefs.SetString(NameKey, value); PlayerPrefs.Save(); }
    }

    // MESMA fórmula do servidor (dto_score no SQL). Tempo pesa mais; cartas tiram; críticos somam.
    public static int Score(int timeSec, int cards, int crits) => Mathf.Max(1, 20000 - 10 * timeSec - 25 * cards + 150 * crits);

    // Posição (1..10) que esta pontuação pegaria na lista atual, ou -1 se não entra no top 10.
    public static int RankFor(int score)
    {
        for (int i = 0; i < Entries.Count; i++) if (score > Entries[i].score) return i + 1;
        return Entries.Count < Size ? Entries.Count + 1 : -1;
    }

    // ===================== Leitura =====================
    public static IEnumerator FetchTop(string mode, Action onDone)
    {
        Mode = mode;
        string select = $"/rest/v1/dto_top?select=name,score,time_sec,cards_thrown,criticals,fusions,damage_taken,deaths&order=score.desc,created_at.asc&limit={Size}";
        string path = select + "&mode=eq." + mode;
        bool fallback = false;
        using (var req = UnityWebRequest.Get(Url + path))
        {
            AddHeaders(req);
            yield return req.SendWebRequest();
            // Sem a coluna "mode" no servidor (SQL dos modos ainda não rodado): o NORMAL usa a lista antiga.
            fallback = req.responseCode == 400 && mode == "normal";
            if (fallback) { }
            else if (req.result == UnityWebRequest.Result.Success)
            {
                // O JsonUtility não lê uma lista solta: embrulha num objeto.
                var list = JsonUtility.FromJson<EntryList>("{\"items\":" + req.downloadHandler.text + "}");
                Entries = list.items ?? new List<Entry>();
                Online = true;
            }
            else
            {
                Debug.LogWarning($"Leaderboard: {req.responseCode} {req.error}");
                Entries = LoadLocal(mode);
                Online = false;
            }
        }
        if (fallback)
        {
            using (var req = UnityWebRequest.Get(Url + select))
            {
                AddHeaders(req);
                yield return req.SendWebRequest();
                Online = req.result == UnityWebRequest.Result.Success;
                Entries = Online ? JsonUtility.FromJson<EntryList>("{\"items\":" + req.downloadHandler.text + "}").items ?? new List<Entry>()
                                 : LoadLocal(mode);
            }
        }
        onDone?.Invoke();
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

        var body = new StringBuilder();
        body.Append('{');
        body.Append("\"p_name\":\"").Append(nick.Replace("\\", "").Replace("\"", "")).Append("\",");
        body.Append("\"p_time\":").Append(run.time_sec).Append(',');
        body.Append("\"p_cards\":").Append(run.cards_thrown).Append(',');
        body.Append("\"p_crits\":").Append(run.criticals).Append(',');
        body.Append("\"p_fusions\":").Append(run.fusions).Append(',');
        body.Append("\"p_damage\":").Append(run.damage_taken).Append(',');
        body.Append("\"p_deaths\":").Append(run.deaths).Append(',');
        body.Append("\"p_platform\":\"").Append(Platform).Append('"');
        string oldBody = body.ToString() + "}";                         // sem o modo (função antiga)
        body.Append(",\"p_mode\":\"").Append(mode).Append("\"}");

        bool ok = false;
        string error = "", text = "";
        long code = 0;
        yield return Post(body.ToString(), (o, c, t) => { ok = o; code = c; text = t; });
        // Servidor sem o SQL dos modos (função com p_mode não existe): o NORMAL ainda dá para enviar na antiga.
        if (!ok && code == 404 && mode == "normal") yield return Post(oldBody, (o, c, t) => { ok = o; code = c; text = t; });
        if (!ok)
        {
            error = text.Contains("name") ? "INVALID NAME" : text.Contains("too fast") ? "WAIT A FEW SECONDS" : "OFFLINE - SAVED LOCALLY";
            Debug.LogWarning($"Leaderboard submit: {code} {text}");
        }
        yield return FetchTop(mode, null);
        onDone?.Invoke(ok, error);
    }

    private static IEnumerator Post(string json, Action<bool, long, string> onDone)
    {
        using (var req = new UnityWebRequest(Url + "/rest/v1/rpc/dto_submit_run", "POST"))
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
    // Top 10 local de cada modo (o NORMAL usa a chave antiga, para não perder o que já estava salvo).
    private static string LocalKeyFor(string mode) => mode == "normal" ? LocalKey : LocalKey + "_" + mode;

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
