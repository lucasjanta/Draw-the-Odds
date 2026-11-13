using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public enum Language { English, Portuguese }

// Tradução do jogo (inglês ↔ português do Brasil).
// O código continua escrevendo os textos em INGLÊS; a tradução acontece sozinha:
//  - textos de UI: o componente LocText (UIFactory.Text coloca em todos) troca o texto pela tradução;
//  - textos no mundo (popups): Fx.WorldText chama Loc.T.
// Textos montados com cores no meio da frase (tutorial, How to Play) usam Loc.Pick(inglês, português).
// Para traduzir um texto novo: adicione uma linha em Pt abaixo ("INGLÊS", "PORTUGUÊS").
// {n} = um número qualquer, {0} = qualquer texto (que também é traduzido, ex.: nome do chefe).
public static class Loc
{
    private const string PrefKey = "dto_lang";

    public static event Action OnChanged;
    public static int Version { get; private set; }

    private static Language? current;
    public static Language Current
    {
        get
        {
            if (current == null)
            {
                // Primeira vez: português se o sistema estiver em português.
                int saved = PlayerPrefs.GetInt(PrefKey, -1);
                current = saved >= 0 ? (Language)saved
                    : Application.systemLanguage == SystemLanguage.Portuguese ? Language.Portuguese : Language.English;
            }
            return current.Value;
        }
        set
        {
            if (current == value) return;
            current = value;
            PlayerPrefs.SetInt(PrefKey, (int)value);
            PlayerPrefs.Save();
            cache.Clear();
            Version++;
            OnChanged?.Invoke();
        }
    }

    public static bool IsPt => Current == Language.Portuguese;
    public static void Toggle() => Current = IsPt ? Language.English : Language.Portuguese;
    public static string LanguageName => IsPt ? "PORTUGUÊS" : "ENGLISH";

    // Escolhe direto entre as duas versões (para frases montadas no código).
    public static string Pick(string en, string pt) => IsPt ? pt : en;

    // ===================== Dicionário =====================
    private static readonly string[,] Pt =
    {
        // Menu principal / páginas
        { "PLAY", "JOGAR" }, { "SKIP TUTORIAL", "PULAR TUTORIAL" }, { "HOW TO PLAY", "COMO JOGAR" },
        { "RANKING", "RANKING" }, { "OPTIONS", "OPÇÕES" }, { "BACK", "VOLTAR" },
        { "Every boss hides a secret number. Hit it with the right card.", "Cada chefe esconde um número secreto. Acerte com a carta certa." },
        { "Official 2026 Unity Game Jam  ·  theme: LUCKY NUMBER", "Unity Game Jam oficial 2026  ·  tema: LUCKY NUMBER" },
        // Pausa / opções
        { "PAUSED", "PAUSADO" }, { "RESUME", "CONTINUAR" }, { "MAIN MENU", "MENU PRINCIPAL" },
        { "MASTER", "GERAL" }, { "MUSIC", "MÚSICA" }, { "EFFECTS", "EFEITOS" }, { "TRACK", "TRILHA" },
        { "LANGUAGE", "IDIOMA" }, { "◄ AUTO ►", "◄ AUTO ►" }, { "◄ GAMEPLAY {n} ►", "◄ MÚSICA {n} ►" },
        // Dificuldade
        { "CHOOSE YOUR ODDS", "ESCOLHA SUA SORTE" }, { "EASY", "FÁCIL" }, { "NORMAL", "NORMAL" }, { "HARDCORE", "HARDCORE" },
        { "RECOMMENDED", "RECOMENDADO" },
        { "See the merge result", "Vê o resultado da fusão" }, { "No merge timer", "Fusão sem tempo" },
        { "More orbs", "Mais orbes" }, { "Bosses attack less", "Chefes atacam menos" }, { "Own ranking", "Ranking próprio" },
        { "The game as designed", "O jogo como foi pensado" }, { "{n}s merge timer", "{n}s para a fusão" },
        { "Fair, fast bosses", "Chefes justos e rápidos" }, { "Faster attacks", "Ataques mais rápidos" },
        { "Attacks more often", "Ataques mais frequentes" }, { "Up to {n} charges", "Até {n} investidas" },
        { "Click a card. Dying restarts from the JACK.", "Clique num cartão. Morrer recomeça do VALETE." },
        // Ranking
        { "NAME", "NOME" }, { "SCORE", "PONTOS" }, { "TIME", "TEMPO" }, { "CARDS", "CARTAS" }, { "CRITS", "CRÍT." },
        { "FUSES", "FUSÕES" }, { "DMG", "DANO" }, { "DEATHS", "MORTES" }, { "SUBMIT", "ENVIAR" },
        { "TYPE YOUR NAME", "DIGITE SEU NOME" }, { "LOADING...", "CARREGANDO..." }, { "SENDING...", "ENVIANDO..." },
        { "SAVED!", "SALVO!" }, { "TOP 10", "TOP 10" }, { "NO RUNS YET. BE THE FIRST!", "NENHUMA PARTIDA AINDA. SEJA O PRIMEIRO!" },
        { "TOP 10 - GLOBAL - {0}", "TOP 10 - GLOBAL - {0}" }, { "TOP 10 - LOCAL (OFFLINE) - {0}", "TOP 10 - LOCAL (OFFLINE) - {0}" },
        { "INVALID NAME", "NOME INVÁLIDO" }, { "WAIT A FEW SECONDS", "ESPERE ALGUNS SEGUNDOS" },
        { "OFFLINE - SAVED LOCALLY", "OFFLINE - SALVO NESTE PC" },
        { "INVALID NAME (2-14 LETTERS/NUMBERS)", "NOME INVÁLIDO (2-14 LETRAS/NÚMEROS)" },
        { "NOT IN THE TOP 10 THIS TIME. LESS TIME, FEWER CARDS, MORE CRITS!", "NÃO ENTROU NO TOP 10. MENOS TEMPO, MENOS CARTAS, MAIS CRÍTICOS!" },
        // HUD
        { "[R] REDRAW {0}", "[R] NOVA MÃO {0}" }, { "HAND {n}/{n}", "MÃO {n}/{n}" }, { "[SHIFT] DASH", "[SHIFT] DASH" },
        { "NO CARDS - GRAB A PACK!", "SEM CARTAS - PEGUE UM PACOTE!" }, { "LAST", "ÚLTIMA" },
        { "NUMBER ? ({0})", "NÚMERO ? ({0})" }, { "CLICK TO CONTINUE", "CLIQUE PARA CONTINUAR" },
        // Banners / fim
        { "BOSS {n}/{n}", "CHEFE {n}/{n}" }, { "TUTORIAL", "TUTORIAL" }, { "TUTORIAL COMPLETE", "TUTORIAL COMPLETO" },
        { "GOOD LUCK!", "BOA SORTE!" }, { "DEFEATED", "DERROTADO" }, { "{0} FOLDED!", "{0} SAIU DA MESA!" },
        { "YOU FOLDED", "VOCÊ SAIU DA MESA" }, { "The {0} called your bluff.", "{0} pagou para ver o seu blefe." },
        { "CLICK TO START OVER FROM THE JACK", "CLIQUE PARA RECOMEÇAR DO VALETE" },
        { "YOU BEAT THE ODDS!", "VOCÊ VENCEU A SORTE!" }, { "HARDCORE CLEAR!", "HARDCORE ZERADO!" }, { "EASY MODE", "MODO FÁCIL" },
        // Fusão
        { "ADD", "SOMAR" }, { "SUBTRACT", "SUBTRAIR" }, { "MULTIPLY", "MULTIPLICAR" },
        { "PUT CARDS IN Q AND E", "COLOQUE CARTAS NO Q E NO E" }, { "THAT MAKES ZERO!", "ISSO DÁ ZERO!" },
        { "OVER {n}!", "PASSA DE {n}!" }, { "SAME SUIT ONLY", "SÓ O MESMO NAIPE" },
        { "pick", "escolhe" }, { "place", "coloca" }, { "merge", "funde" }, { "skip", "pula" },
        { "MERGED! {0}", "FUNDIU! {0}" }, { "SKIPPED", "PULOU" }, { "TIME UP", "ACABOU O TEMPO" },
        // Popups do combate
        { "▲ HIGHER", "▲ MAIOR" }, { "▼ LOWER", "▼ MENOR" }, { "EXACT!", "EXATO!" }, { "NO HEAL", "SEM CURA" },
        { "SHUFFLE!", "EMBARALHOU!" }, { "NEW NUMBER!", "NOVO NÚMERO!" }, { "REDRAW!", "NOVA MÃO!" },
        { "HAND FULL", "MÃO CHEIA" }, { "+{n} CARDS", "+{n} CARTAS" },
        { "DICE x{n}", "DADOS x{n}" }, { "CHIP RAIN x{n}", "CHUVA DE FICHAS x{n}" }, { "CHARGE", "INVESTIDA" },
        { "CHARGE x{n}", "INVESTIDA x{n}" }, { "JOKERS x{n}", "CORINGAS x{n}" },
        // Nomes dos chefes (por último: "JACK {0}" pega "JACK ♠")
        { "JACK", "VALETE" }, { "QUEEN", "DAMA" }, { "KING", "REI" }, { "JOKER", "CORINGA" },
        { "JACK {0}", "VALETE {0}" }, { "QUEEN {0}", "DAMA {0}" }, { "KING {0}", "REI {0}" },
    };

    private static Dictionary<string, string> exact;
    private static List<(Regex regex, string pt, bool[] numeric)> patterns;
    private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

    private static void Build()
    {
        exact = new Dictionary<string, string>();
        patterns = new List<(Regex, string, bool[])>();
        for (int i = 0; i < Pt.GetLength(0); i++)
        {
            string en = Pt[i, 0], pt = Pt[i, 1];
            if (!en.Contains("{")) { exact[en] = pt; continue; }
            // "HAND {n}/{n}" → ^HAND (\d+)/(\d+)$ ; os grupos voltam na mesma ordem no texto em português
            var kinds = new List<bool>();
            string rx = "^" + Regex.Replace(Regex.Escape(en).Replace("\\{", "{"), @"\{(n|0)\}", m =>
            {
                kinds.Add(m.Groups[1].Value == "n");
                return m.Groups[1].Value == "n" ? @"(\d+)" : "(.+)";
            }) + "$";
            int k = 0;
            string template = Regex.Replace(pt, @"\{(n|0)\}", _ => "{" + (k++) + "}");
            patterns.Add((new Regex(rx, RegexOptions.Singleline), template, kinds.ToArray()));
        }
    }

    // Tradução de um texto em inglês (devolve o próprio texto se não houver tradução).
    public static string T(string en)
    {
        if (!IsPt || string.IsNullOrEmpty(en)) return en;
        if (cache.TryGetValue(en, out var hit)) return hit;
        string result = Translate(en, 0);
        if (cache.Count > 4000) cache.Clear();
        cache[en] = result;
        return result;
    }

    private static string Translate(string s, int depth)
    {
        if (depth > 4 || string.IsNullOrWhiteSpace(s)) return s;
        if (exact == null) Build();
        if (exact.TryGetValue(s, out var pt)) return pt;

        // Dois ataques juntos: "DICE x4 + CHIP RAIN x5"
        if (s.Contains(" + "))
        {
            var parts = s.Split(new[] { " + " }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++) parts[i] = Translate(parts[i], depth + 1);
            return string.Join(" + ", parts);
        }

        foreach (var (regex, template, numeric) in patterns)
        {
            var m = regex.Match(s);
            if (!m.Success) continue;
            var args = new object[m.Groups.Count - 1];
            for (int g = 1; g < m.Groups.Count; g++)
                args[g - 1] = numeric[g - 1] ? m.Groups[g].Value : Translate(m.Groups[g].Value, depth + 1);
            return string.Format(template, args);
        }

        // Várias linhas: traduz cada uma
        if (s.Contains("\n"))
        {
            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = Translate(lines[i], depth + 1);
            return string.Join("\n", lines);
        }

        // Texto com tags (<color>, <size>...): traduz os pedaços entre as tags
        if (s.Contains("<"))
        {
            var sb = new StringBuilder();
            foreach (var piece in Regex.Split(s, "(<[^>]+>)"))
            {
                if (piece.StartsWith("<") && piece.EndsWith(">")) { sb.Append(piece); continue; }
                string core = piece.Trim();
                if (core.Length == 0) { sb.Append(piece); continue; }
                int lead = piece.IndexOf(core, StringComparison.Ordinal);
                sb.Append(piece, 0, lead).Append(Translate(core, depth + 1)).Append(piece, lead + core.Length, piece.Length - lead - core.Length);
            }
            return sb.ToString();
        }
        return s;
    }
}
