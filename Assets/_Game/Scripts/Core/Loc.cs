using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// A ordem importa: o número de cada idioma fica salvo no PlayerPrefs (0 = inglês, 1 = português...).
// Só idiomas com alfabeto latino: as fontes (Silkscreen) têm acentos, mas não cirílico, turco ou japonês.
public enum Language { English, Portuguese, Spanish, French, German, Italian }

// Tradução do jogo. O código continua escrevendo os textos em INGLÊS; a tradução acontece sozinha:
//  - textos de UI: o componente LocText (UIFactory.Text coloca em todos) troca o texto pela tradução;
//  - textos no mundo (popups): Fx.WorldText chama Loc.T.
// Textos montados com cores no meio da frase (tutorial, How to Play) usam
// Loc.Pick(inglês, português, espanhol, francês, alemão, italiano).
// Para traduzir um texto novo: adicione uma linha R(...) na tabela abaixo, na mesma ordem.
// {n} = um número qualquer, {0} = qualquer texto (que também é traduzido, ex.: nome do chefe).
public static class Loc
{
    private const string PrefKey = "dto_lang";
    public const int Count = 6;

    private static readonly string[] Names = { "ENGLISH", "PORTUGUÊS", "ESPAÑOL", "FRANÇAIS", "DEUTSCH", "ITALIANO" };

    public static event Action OnChanged;
    public static int Version { get; private set; }

    private static Language? current;
    public static Language Current
    {
        get
        {
            if (current == null)
            {
                // Primeira vez: inglês (o jogador troca nas Opções; a escolha fica salva).
                // Exceção: num portal vale o idioma que o portal informa (regra deles:
                // abrir no idioma do jogador; se não tivermos esse idioma, inglês).
                int saved = PlayerPrefs.GetInt(PrefKey, -1);
                current = saved >= 0 && saved < Count ? (Language)saved : Portal.PreferredLanguage ?? Language.English;
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

    // Idioma padrão vindo do portal (chega depois que o SDK inicia). Só vale se o jogador nunca escolheu
    // um idioma nas Opções, e não é salvo (a escolha manual continua tendo prioridade).
    public static void ApplyDefault(Language lang)
    {
        if (PlayerPrefs.GetInt(PrefKey, -1) >= 0 || Current == lang) return;
        current = lang;
        cache.Clear();
        Version++;
        OnChanged?.Invoke();
    }

    // Idioma a partir de um código ("pt-BR", "es", "de_DE"...). null = não temos esse idioma.
    public static Language? FromCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length < 2) return null;
        switch (code.Substring(0, 2).ToLowerInvariant())
        {
            case "en": return Language.English;
            case "pt": return Language.Portuguese;
            case "es": return Language.Spanish;
            case "fr": return Language.French;
            case "de": return Language.German;
            case "it": return Language.Italian;
        }
        return null;
    }

    public static Language? FromSystem(SystemLanguage lang)
    {
        switch (lang)
        {
            case SystemLanguage.English: return Language.English;
            case SystemLanguage.Portuguese: return Language.Portuguese;
            case SystemLanguage.Spanish: return Language.Spanish;
            case SystemLanguage.French: return Language.French;
            case SystemLanguage.German: return Language.German;
            case SystemLanguage.Italian: return Language.Italian;
        }
        return null;
    }

    public static bool IsEnglish => Current == Language.English;
    public static void Next() => Current = (Language)(((int)Current + 1) % Count);
    public static void Previous() => Current = (Language)(((int)Current + Count - 1) % Count);
    public static string LanguageName => Names[(int)Current];

    // Escolhe direto entre as versões (para frases montadas no código, com cores no meio).
    public static string Pick(string en, string pt, string es, string fr, string de, string it)
    {
        switch (Current)
        {
            case Language.Portuguese: return pt;
            case Language.Spanish: return es;
            case Language.French: return fr;
            case Language.German: return de;
            case Language.Italian: return it;
        }
        return en;
    }

    // ===================== Dicionário =====================
    // Colunas: inglês, português, espanhol, francês, alemão, italiano.
    private static string[] R(string en, string pt, string es, string fr, string de, string it) => new[] { en, pt, es, fr, de, it };

    private static readonly string[][] Table =
    {
        // Menu principal / páginas
        R("PLAY", "JOGAR", "JUGAR", "JOUER", "SPIELEN", "GIOCA"),
        R("SKIP TUTORIAL", "PULAR TUTORIAL", "SALTAR TUTORIAL", "PASSER LE TUTO", "TUTORIAL ÜBERSPRINGEN", "SALTA TUTORIAL"),
        R("HOW TO PLAY", "COMO JOGAR", "CÓMO JUGAR", "COMMENT JOUER", "ANLEITUNG", "COME SI GIOCA"),
        R("RANKING", "RANKING", "RANKING", "CLASSEMENT", "RANGLISTE", "CLASSIFICA"),
        R("OPTIONS", "OPÇÕES", "OPCIONES", "OPTIONS", "OPTIONEN", "OPZIONI"),
        R("BACK", "VOLTAR", "VOLVER", "RETOUR", "ZURÜCK", "INDIETRO"),
        R("Every boss hides a secret number. Hit it with the right card.",
          "Cada chefe esconde um número secreto. Acerte com a carta certa.",
          "Cada jefe esconde un número secreto. Acierta con la carta correcta.",
          "Chaque boss cache un nombre secret. Touche-le avec la bonne carte.",
          "Jeder Boss versteckt eine geheime Zahl. Triff sie mit der richtigen Karte.",
          "Ogni boss nasconde un numero segreto. Colpiscilo con la carta giusta."),
        R("Official 2026 Unity Game Jam  ·  theme: LUCKY NUMBER",
          "Unity Game Jam oficial 2026  ·  tema: LUCKY NUMBER",
          "Unity Game Jam oficial 2026  ·  tema: LUCKY NUMBER",
          "Unity Game Jam officielle 2026  ·  thème : LUCKY NUMBER",
          "Offizieller Unity Game Jam 2026  ·  Thema: LUCKY NUMBER",
          "Unity Game Jam ufficiale 2026  ·  tema: LUCKY NUMBER"),
        // Pausa / opções
        R("PAUSED", "PAUSADO", "PAUSA", "PAUSE", "PAUSE", "PAUSA"),
        R("RESUME", "CONTINUAR", "CONTINUAR", "REPRENDRE", "WEITER", "RIPRENDI"),
        R("MAIN MENU", "MENU PRINCIPAL", "MENÚ PRINCIPAL", "MENU PRINCIPAL", "HAUPTMENÜ", "MENU PRINCIPALE"),
        R("MASTER", "GERAL", "GENERAL", "GÉNÉRAL", "GESAMT", "GENERALE"),
        R("MUSIC", "MÚSICA", "MÚSICA", "MUSIQUE", "MUSIK", "MUSICA"),
        R("EFFECTS", "EFEITOS", "EFECTOS", "EFFETS", "EFFEKTE", "EFFETTI"),
        R("TRACK", "TRILHA", "PISTA", "PISTE", "TRACK", "BRANO"),
        R("LANGUAGE", "IDIOMA", "IDIOMA", "LANGUE", "SPRACHE", "LINGUA"),
        R("◄ AUTO ►", "◄ AUTO ►", "◄ AUTO ►", "◄ AUTO ►", "◄ AUTO ►", "◄ AUTO ►"),
        R("◄ GAMEPLAY {n} ►", "◄ MÚSICA {n} ►", "◄ MÚSICA {n} ►", "◄ MUSIQUE {n} ►", "◄ MUSIK {n} ►", "◄ MUSICA {n} ►"),
        R("AUTO", "AUTO", "AUTO", "AUTO", "AUTO", "AUTO"),
        R("ON", "LIGADO", "SÍ", "OUI", "AN", "SÌ"),
        R("OFF", "DESLIGADO", "NO", "NON", "AUS", "NO"),
        R("AUTO (ON)", "AUTO (LIGADO)", "AUTO (SÍ)", "AUTO (OUI)", "AUTO (AN)", "AUTO (SÌ)"),
        // Teclas (Opções → CONTROLS)
        R("CONTROLS", "CONTROLES", "CONTROLES", "COMMANDES", "STEUERUNG", "COMANDI"),
        R("KEYS", "TECLAS", "TECLAS", "TOUCHES", "TASTEN", "TASTI"),
        R("MOVE LEFT", "ESQUERDA", "IZQUIERDA", "GAUCHE", "LINKS", "SINISTRA"),
        R("MOVE RIGHT", "DIREITA", "DERECHA", "DROITE", "RECHTS", "DESTRA"),
        R("DROP DOWN", "DESCER", "BAJAR", "DESCENDRE", "RUNTER", "SCENDI"),
        R("JUMP", "PULAR", "SALTAR", "SAUTER", "SPRINGEN", "SALTA"),
        R("NEW HAND", "NOVA MÃO", "NUEVA MANO", "NOUVELLE MAIN", "NEUE HAND", "NUOVA MANO"),
        R("MERGE SLOT 1", "ESPAÇO 1 DA FUSÃO", "HUECO 1 DE FUSIÓN", "EMPLACEMENT 1", "FUSIONSPLATZ 1", "SLOT FUSIONE 1"),
        R("MERGE SLOT 2", "ESPAÇO 2 DA FUSÃO", "HUECO 2 DE FUSIÓN", "EMPLACEMENT 2", "FUSIONSPLATZ 2", "SLOT FUSIONE 2"),
        R("PAUSE", "PAUSA", "PAUSA", "PAUSE", "PAUSE", "PAUSA"),
        R("PRESS A KEY...", "APERTE UMA TECLA...", "PULSA UNA TECLA...", "APPUIE SUR UNE TOUCHE...", "TASTE DRÜCKEN...", "PREMI UN TASTO..."),
        R("RESET", "PADRÃO", "RESTABLECER", "PAR DÉFAUT", "STANDARD", "PREDEFINITI"),
        R("Click an action, then press the new key. ESC cancels.",
          "Clique numa ação e aperte a nova tecla. ESC cancela.",
          "Haz clic en una acción y pulsa la nueva tecla. ESC cancela.",
          "Clique sur une action puis appuie sur la nouvelle touche. ÉCHAP annule.",
          "Aktion anklicken, dann neue Taste drücken. ESC bricht ab.",
          "Clicca un'azione e premi il nuovo tasto. ESC annulla."),
        R("Arrows and ESC always work. Mouse and 1-0 pick cards.",
          "Setas e ESC sempre funcionam. Mouse e 1-0 escolhem cartas.",
          "Flechas y ESC siempre funcionan. Ratón y 1-0 eligen cartas.",
          "Flèches et ÉCHAP marchent toujours. Souris et 1-0 choisissent les cartes.",
          "Pfeile und ESC gehen immer. Maus und 1-0 wählen Karten.",
          "Frecce ed ESC funzionano sempre. Mouse e 1-0 scelgono le carte."),
        R("SPACE", "ESPAÇO", "ESPACIO", "ESPACE", "LEER", "SPAZIO"),
        // Dificuldade
        R("CHOOSE YOUR ODDS", "ESCOLHA SUA SORTE", "ELIGE TU SUERTE", "CHOISIS TA CHANCE", "WÄHLE DEIN GLÜCK", "SCEGLI LA TUA SORTE"),
        R("EASY", "FÁCIL", "FÁCIL", "FACILE", "LEICHT", "FACILE"),
        R("NORMAL", "NORMAL", "NORMAL", "NORMAL", "NORMAL", "NORMALE"),
        R("HARDCORE", "HARDCORE", "HARDCORE", "HARDCORE", "HARDCORE", "HARDCORE"),
        R("RECOMMENDED", "RECOMENDADO", "RECOMENDADO", "RECOMMANDÉ", "EMPFOHLEN", "CONSIGLIATO"),
        R("See the merge result", "Vê o resultado da fusão", "Ves el resultado de la fusión", "Tu vois le résultat de la fusion", "Fusionsergebnis sichtbar", "Vedi il risultato della fusione"),
        R("No merge timer", "Fusão sem tempo", "Fusión sin tiempo", "Fusion sans chrono", "Fusion ohne Zeitlimit", "Fusione senza tempo"),
        R("More orbs", "Mais orbes", "Más orbes", "Plus d'orbes", "Mehr Orbs", "Più sfere"),
        R("Bosses attack less", "Chefes atacam menos", "Los jefes atacan menos", "Les boss attaquent moins", "Bosse greifen seltener an", "I boss attaccano meno"),
        R("Own ranking", "Ranking próprio", "Ranking propio", "Classement à part", "Eigene Rangliste", "Classifica a parte"),
        R("The game as designed", "O jogo como foi pensado", "El juego tal como se pensó", "Le jeu tel qu'il a été pensé", "Das Spiel wie gedacht", "Il gioco come è stato pensato"),
        R("{n}s merge timer", "{n}s para a fusão", "{n}s para la fusión", "{n}s pour la fusion", "{n}s für die Fusion", "{n}s per la fusione"),
        R("Fair, fast bosses", "Chefes justos e rápidos", "Jefes justos y rápidos", "Boss justes et rapides", "Faire, schnelle Bosse", "Boss giusti e veloci"),
        R("Faster attacks", "Ataques mais rápidos", "Ataques más rápidos", "Attaques plus rapides", "Schnellere Angriffe", "Attacchi più veloci"),
        R("Attacks more often", "Ataques mais frequentes", "Ataca más a menudo", "Attaque plus souvent", "Greift öfter an", "Attacca più spesso"),
        R("Up to {n} charges", "Até {n} investidas", "Hasta {n} embestidas", "Jusqu'à {n} charges", "Bis zu {n} Sturmangriffe", "Fino a {n} cariche"),
        R("Click a card. Dying restarts from the JACK.", "Clique num cartão. Morrer recomeça do VALETE.",
          "Haz clic en una carta. Morir reinicia desde la J.", "Clique sur une carte. Mourir recommence au VALET.",
          "Klicke eine Karte. Sterben startet beim BUBEN neu.", "Clicca una carta. Morire ricomincia dal FANTE."),
        // Ranking
        R("YOUR RUN", "SUA PARTIDA", "TU PARTIDA", "TA PARTIE", "DEIN LAUF", "LA TUA PARTITA"),
        R("NAME", "NOME", "NOMBRE", "NOM", "NAME", "NOME"),
        R("SCORE", "PONTOS", "PUNTOS", "SCORE", "PUNKTE", "PUNTI"),
        R("TIME", "TEMPO", "TIEMPO", "TEMPS", "ZEIT", "TEMPO"),
        R("CARDS", "CARTAS", "CARTAS", "CARTES", "KARTEN", "CARTE"),
        R("CRITS", "CRÍT.", "CRÍT.", "CRIT.", "KRIT.", "CRIT."),
        R("FUSES", "FUSÕES", "FUSIONES", "FUSIONS", "FUSIONEN", "FUSIONI"),
        R("DMG", "DANO", "DAÑO", "DÉGÂTS", "SCHADEN", "DANNI"),
        R("DEATHS", "MORTES", "MUERTES", "MORTS", "TODE", "MORTI"),
        R("SUBMIT", "ENVIAR", "ENVIAR", "ENVOYER", "SENDEN", "INVIA"),
        R("TYPE YOUR NAME", "DIGITE SEU NOME", "ESCRIBE TU NOMBRE", "TAPE TON NOM", "NAMEN EINGEBEN", "SCRIVI IL TUO NOME"),
        R("LOADING...", "CARREGANDO...", "CARGANDO...", "CHARGEMENT...", "LADEN...", "CARICAMENTO..."),
        R("SENDING...", "ENVIANDO...", "ENVIANDO...", "ENVOI...", "SENDEN...", "INVIO..."),
        R("SAVED!", "SALVO!", "¡GUARDADO!", "ENREGISTRÉ !", "GESPEICHERT!", "SALVATO!"),
        R("TOP 10", "TOP 10", "TOP 10", "TOP 10", "TOP 10", "TOP 10"),
        R("NO RUNS YET. BE THE FIRST!", "NENHUMA PARTIDA AINDA. SEJA O PRIMEIRO!", "AÚN NO HAY PARTIDAS. ¡SÉ EL PRIMERO!",
          "AUCUNE PARTIE. SOIS LE PREMIER !", "NOCH KEINE RUNDEN. SEI DER ERSTE!", "ANCORA NESSUNA PARTITA. SII IL PRIMO!"),
        R("TOP 10 - GLOBAL - {0}", "TOP 10 - GLOBAL - {0}", "TOP 10 - GLOBAL - {0}", "TOP 10 - MONDIAL - {0}", "TOP 10 - WELTWEIT - {0}", "TOP 10 - GLOBALE - {0}"),
        R("TOP 10 - LOCAL (OFFLINE) - {0}", "TOP 10 - LOCAL (OFFLINE) - {0}", "TOP 10 - LOCAL (SIN CONEXIÓN) - {0}", "TOP 10 - LOCAL (HORS LIGNE) - {0}", "TOP 10 - LOKAL (OFFLINE) - {0}", "TOP 10 - LOCALE (OFFLINE) - {0}"),
        R("TOP 10 - LOCAL - {0}", "TOP 10 - LOCAL - {0}", "TOP 10 - LOCAL - {0}", "TOP 10 - LOCAL - {0}", "TOP 10 - LOKAL - {0}", "TOP 10 - LOCALE - {0}"),
        R("INVALID NAME", "NOME INVÁLIDO", "NOMBRE NO VÁLIDO", "NOM INVALIDE", "UNGÜLTIGER NAME", "NOME NON VALIDO"),
        R("TUTORIAL", "TUTORIAL", "TUTORIAL", "TUTORIEL", "TUTORIAL", "TUTORIAL"),
        R("TOUCH", "TOQUE", "TÁCTIL", "TACTILE", "TOUCH", "TOUCH"),
        R("MERGE", "JUNTAR", "FUSIONAR", "FUSIONNER", "FUSION", "FONDI"),
        R("SKIP", "PULAR", "SALTAR", "PASSER", "ÜBERSPRINGEN", "SALTA"),
        R("RESTART", "RECOMEÇAR", "REINICIAR", "RECOMMENCER", "NEUSTART", "RICOMINCIA"),
        R("ENDLESS", "INFINITO", "INFINITO", "INFINI", "ENDLOS", "INFINITO"),
        R("TOP 10 - GLOBAL - ENDLESS", "TOP 10 - GLOBAL - INFINITO", "TOP 10 - GLOBAL - INFINITO", "TOP 10 - MONDIAL - INFINI", "TOP 10 - WELTWEIT - ENDLOS", "TOP 10 - GLOBALE - INFINITO"),
        R("TOP 10 - LOCAL - ENDLESS", "TOP 10 - LOCAL - INFINITO", "TOP 10 - LOCAL - INFINITO", "TOP 10 - LOCAL - INFINI", "TOP 10 - LOKAL - ENDLOS", "TOP 10 - LOCALE - INFINITO"),
        R("TOP 10 - LOCAL (OFFLINE) - ENDLESS", "TOP 10 - LOCAL (OFFLINE) - INFINITO", "TOP 10 - LOCAL (SIN CONEXIÓN) - INFINITO", "TOP 10 - LOCAL (HORS LIGNE) - INFINI", "TOP 10 - LOKAL (OFFLINE) - ENDLOS", "TOP 10 - LOCALE (OFFLINE) - INFINITO"),
        R("REDRAW", "TROCAR", "CAMBIAR", "REPIOCHER", "NEU ZIEHEN", "CAMBIA"),
        R("DASH", "DASH", "DASH", "DASH", "DASH", "DASH"),
        R("WAIT A FEW SECONDS", "ESPERE ALGUNS SEGUNDOS", "ESPERA UNOS SEGUNDOS", "ATTENDS QUELQUES SECONDES", "WARTE EIN PAAR SEKUNDEN", "ASPETTA QUALCHE SECONDO"),
        R("OFFLINE - SAVED LOCALLY", "OFFLINE - SALVO NESTE PC", "SIN CONEXIÓN - GUARDADO AQUÍ", "HORS LIGNE - ENREGISTRÉ ICI", "OFFLINE - LOKAL GESPEICHERT", "OFFLINE - SALVATO QUI"),
        R("INVALID NAME (2-14 LETTERS/NUMBERS)", "NOME INVÁLIDO (2-14 LETRAS/NÚMEROS)", "NOMBRE NO VÁLIDO (2-14 LETRAS/NÚMEROS)",
          "NOM INVALIDE (2-14 LETTRES/CHIFFRES)", "UNGÜLTIGER NAME (2-14 BUCHSTABEN/ZIFFERN)", "NOME NON VALIDO (2-14 LETTERE/NUMERI)"),
        R("NOT IN THE TOP 10 THIS TIME. LESS TIME, FEWER CARDS, MORE CRITS!", "NÃO ENTROU NO TOP 10. MENOS TEMPO, MENOS CARTAS, MAIS CRÍTICOS!",
          "NO ENTRASTE EN EL TOP 10. ¡MENOS TIEMPO, MENOS CARTAS, MÁS CRÍTICOS!", "PAS DANS LE TOP 10 CETTE FOIS. MOINS DE TEMPS, MOINS DE CARTES, PLUS DE CRITIQUES !",
          "DIESMAL NICHT IN DEN TOP 10. WENIGER ZEIT, WENIGER KARTEN, MEHR KRITS!", "NIENTE TOP 10 STAVOLTA. MENO TEMPO, MENO CARTE, PIÙ CRITICI!"),
        // HUD (a tecla vem de KeyBindings)
        R("[{0}] REDRAW {0}", "[{0}] NOVA MÃO {0}", "[{0}] NUEVA MANO {0}", "[{0}] NOUVELLE MAIN {0}", "[{0}] NEUE HAND {0}", "[{0}] NUOVA MANO {0}"),
        R("REDRAW {0}", "NOVA MÃO {0}", "NUEVA MANO {0}", "NOUVELLE MAIN {0}", "NEUE HAND {0}", "NUOVA MANO {0}"),
        R("HAND {n}/{n}", "MÃO {n}/{n}", "MANO {n}/{n}", "MAIN {n}/{n}", "HAND {n}/{n}", "MANO {n}/{n}"),
        R("[{0}] DASH", "[{0}] DASH", "[{0}] DASH", "[{0}] DASH", "[{0}] DASH", "[{0}] DASH"),
        R("NO CARDS - GRAB A PACK!", "SEM CARTAS - PEGUE UM PACOTE!", "SIN CARTAS - ¡COGE UN PAQUETE!", "PLUS DE CARTES - PRENDS UN PAQUET !", "KEINE KARTEN - HOL DIR EIN PACK!", "NIENTE CARTE - PRENDI UN PACCHETTO!"),
        R("LAST", "ÚLTIMA", "ÚLTIMA", "DERNIÈRE", "LETZTE", "ULTIMA"),
        R("NUMBER ? ({0})", "NÚMERO ? ({0})", "NÚMERO ? ({0})", "NOMBRE ? ({0})", "ZAHL ? ({0})", "NUMERO ? ({0})"),
        R("CLICK TO CONTINUE", "CLIQUE PARA CONTINUAR", "HAZ CLIC PARA CONTINUAR", "CLIQUE POUR CONTINUER", "KLICKEN ZUM FORTFAHREN", "CLICCA PER CONTINUARE"),
        // Banners / fim
        R("BOSS {n}/{n}", "CHEFE {n}/{n}", "JEFE {n}/{n}", "BOSS {n}/{n}", "BOSS {n}/{n}", "BOSS {n}/{n}"),
        R("TUTORIAL COMPLETE", "TUTORIAL COMPLETO", "TUTORIAL COMPLETADO", "TUTORIEL TERMINÉ", "TUTORIAL ABGESCHLOSSEN", "TUTORIAL COMPLETATO"),
        R("GOOD LUCK!", "BOA SORTE!", "¡BUENA SUERTE!", "BONNE CHANCE !", "VIEL GLÜCK!", "BUONA FORTUNA!"),
        R("DEFEATED", "DERROTADO", "DERROTADO", "VAINCU", "BESIEGT", "SCONFITTO"),
        R("{0} FOLDED!", "{0} SAIU DA MESA!", "¡{0} SE RETIRA!", "{0} SE COUCHE !", "{0} STEIGT AUS!", "{0} PASSA!"),
        R("YOU FOLDED", "VOCÊ SAIU DA MESA", "TE RETIRASTE", "TU T'ES COUCHÉ", "DU BIST RAUS", "HAI PASSATO"),
        R("The {0} called your bluff.", "{0} pagou para ver o seu blefe.", "{0} descubrió tu farol.", "{0} a démasqué ton bluff.", "{0} hat deinen Bluff durchschaut.", "{0} ha visto il tuo bluff."),
        R("CLICK TO START OVER FROM THE JACK", "CLIQUE PARA RECOMEÇAR DO VALETE", "HAZ CLIC PARA VOLVER A LA J", "CLIQUE POUR RECOMMENCER AU VALET", "KLICKEN, UM BEIM BUBEN NEU ZU STARTEN", "CLICCA PER RICOMINCIARE DAL FANTE"),
        R("YOU BEAT THE ODDS!", "VOCÊ VENCEU A SORTE!", "¡VENCISTE A LA SUERTE!", "TU AS DÉFIÉ LE SORT !", "DU HAST DAS GLÜCK BESIEGT!", "HAI SFIDATO LA SORTE!"),
        R("HARDCORE CLEAR!", "HARDCORE ZERADO!", "¡HARDCORE SUPERADO!", "HARDCORE RÉUSSI !", "HARDCORE GESCHAFFT!", "HARDCORE COMPLETATO!"),
        R("EASY MODE", "MODO FÁCIL", "MODO FÁCIL", "MODE FACILE", "LEICHTER MODUS", "MODALITÀ FACILE"),
        // Fusão
        R("ADD", "SOMAR", "SUMAR", "ADDITION", "ADDIEREN", "SOMMA"),
        R("SUBTRACT", "SUBTRAIR", "RESTAR", "SOUSTRACTION", "SUBTRAHIEREN", "SOTTRAI"),
        R("MULTIPLY", "MULTIPLICAR", "MULTIPLICAR", "MULTIPLICATION", "MULTIPLIZIEREN", "MOLTIPLICA"),
        R("PUT CARDS IN Q AND E", "COLOQUE CARTAS NO Q E NO E", "PON CARTAS EN Q Y E", "METS DES CARTES EN Q ET E", "LEG KARTEN AUF Q UND E", "METTI CARTE IN Q ED E"),
        R("PUT 2 CARDS IN THE SLOTS", "COLOQUE 2 CARTAS NOS ESPAÇOS", "PON 2 CARTAS EN LOS HUECOS", "METS 2 CARTES DANS LES EMPLACEMENTS", "LEG 2 KARTEN IN DIE PLÄTZE", "METTI 2 CARTE NEGLI SLOT"),
        R("THAT MAKES ZERO!", "ISSO DÁ ZERO!", "¡ESO DA CERO!", "ÇA FAIT ZÉRO !", "DAS ERGIBT NULL!", "FA ZERO!"),
        R("OVER {n}!", "PASSA DE {n}!", "¡MÁS DE {n}!", "PLUS DE {n} !", "ÜBER {n}!", "OLTRE {n}!"),
        R("SAME SUIT ONLY", "SÓ O MESMO NAIPE", "SOLO EL MISMO PALO", "MÊME COULEUR SEULEMENT", "NUR GLEICHE FARBE", "SOLO STESSO SEME"),
        R("pick", "escolhe", "elige", "choisir", "wählen", "scegli"),
        R("place", "coloca", "coloca", "placer", "ablegen", "metti"),
        R("merge", "funde", "fusiona", "fusionner", "fusionieren", "fondi"),
        R("skip", "pula", "salta", "passer", "überspringen", "salta"),
        R("MERGED! {0}", "FUNDIU! {0}", "¡FUSIONADO! {0}", "FUSIONNÉ ! {0}", "FUSIONIERT! {0}", "FUSO! {0}"),
        R("SKIPPED", "PULOU", "SALTADO", "PASSÉ", "ÜBERSPRUNGEN", "SALTATO"),
        R("TIME UP", "ACABOU O TEMPO", "SE ACABÓ EL TIEMPO", "TEMPS ÉCOULÉ", "ZEIT UM", "TEMPO SCADUTO"),
        // Popups do combate
        R("▲ HIGHER", "▲ MAIOR", "▲ MAYOR", "▲ PLUS", "▲ HÖHER", "▲ PIÙ ALTO"),
        R("▼ LOWER", "▼ MENOR", "▼ MENOR", "▼ MOINS", "▼ TIEFER", "▼ PIÙ BASSO"),
        R("EXACT!", "EXATO!", "¡EXACTO!", "EXACT !", "GENAU!", "ESATTO!"),
        R("NO HEAL", "SEM CURA", "SIN CURA", "PAS DE SOIN", "KEINE HEILUNG", "NIENTE CURA"),
        R("SHUFFLE!", "EMBARALHOU!", "¡BARAJADO!", "MÉLANGE !", "GEMISCHT!", "MESCOLATO!"),
        R("NEW NUMBER!", "NOVO NÚMERO!", "¡NUEVO NÚMERO!", "NOUVEAU NOMBRE !", "NEUE ZAHL!", "NUOVO NUMERO!"),
        R("REDRAW!", "NOVA MÃO!", "¡NUEVA MANO!", "NOUVELLE MAIN !", "NEUE HAND!", "NUOVA MANO!"),
        R("HAND FULL", "MÃO CHEIA", "MANO LLENA", "MAIN PLEINE", "HAND VOLL", "MANO PIENA"),
        R("+{n} CARDS", "+{n} CARTAS", "+{n} CARTAS", "+{n} CARTES", "+{n} KARTEN", "+{n} CARTE"),
        R("DICE x{n}", "DADOS x{n}", "DADOS x{n}", "DÉS x{n}", "WÜRFEL x{n}", "DADI x{n}"),
        R("CHIP RAIN x{n}", "CHUVA DE FICHAS x{n}", "LLUVIA DE FICHAS x{n}", "PLUIE DE JETONS x{n}", "CHIPREGEN x{n}", "PIOGGIA DI FICHE x{n}"),
        R("CHARGE", "INVESTIDA", "EMBESTIDA", "CHARGE", "STURMANGRIFF", "CARICA"),
        R("CHARGE x{n}", "INVESTIDA x{n}", "EMBESTIDA x{n}", "CHARGE x{n}", "STURMANGRIFF x{n}", "CARICA x{n}"),
        R("JOKERS x{n}", "CORINGAS x{n}", "COMODINES x{n}", "JOKERS x{n}", "JOKER x{n}", "JOLLY x{n}"),
        R("ROULETTE", "ROLETA", "RULETA", "ROULETTE", "ROULETTE", "ROULETTE"),
        R("MARKED CARDS x{n}", "CARTAS MARCADAS x{n}", "CARTAS MARCADAS x{n}", "CARTES MARQUÉES x{n}", "GEZINKTE KARTEN x{n}", "CARTE SEGNATE x{n}"),
        R("ACE", "ÁS", "AS", "AS", "ASS", "ASSO"),
        R("TAVERN", "TAVERNA", "TABERNA", "TAVERNE", "TAVERNE", "TAVERNA"),
        R("JOKERS", "CORINGAS", "COMODINES", "JOKERS", "JOKER", "JOLLY"),
        R("CARD BACKS", "VERSOS", "REVERSOS", "DOS DE CARTE", "KARTENRÜCKEN", "DORSI"),
        R("RUMORS", "BOATOS", "RUMORES", "RUMEURS", "GERÜCHTE", "VOCI"),
        R("CHOOSE A JOKER", "ESCOLHA UM CORINGA", "ELIGE UN COMODÍN", "CHOISIS UN JOKER", "WÄHLE EINEN JOKER", "SCEGLI UN JOLLY"),
        R("REPLAY STORY", "REVER HISTÓRIA", "VER HISTORIA", "REVOIR L'HISTOIRE", "GESCHICHTE NOCHMAL", "RIVEDI STORIA"),
        R("OWNED", "SEU", "TUYO", "ACQUIS", "GEHÖRT DIR", "TUO"),
        R("EQUIPPED", "EM USO", "EN USO", "ÉQUIPÉ", "AUSGERÜSTET", "IN USO"),
        R("NEW!", "NOVO!", "¡NUEVO!", "NOUVEAU !", "NEU!", "NUOVO!"),
        R("RAISE", "TRUCO", "TRUCO", "RELANCE", "RAISE", "RILANCIO"),
        R("CLASSIC", "CLÁSSICO", "CLÁSICO", "CLASSIQUE", "KLASSISCH", "CLASSICO"),
        R("ROYAL GOLD", "OURO REAL", "ORO REAL", "OR ROYAL", "KÖNIGSGOLD", "ORO REALE"),
        R("EMERALD", "ESMERALDA", "ESMERALDA", "ÉMERAUDE", "SMARAGD", "SMERALDO"),
        R("CRIMSON", "CARMIM", "CARMESÍ", "CRAMOISI", "KARMESIN", "CREMISI"),
        R("MIDNIGHT", "MEIA-NOITE", "MEDIANOCHE", "MINUIT", "MITTERNACHT", "MEZZANOTTE"),
        // 27/11: pausa, guarda-roupa, conquistas, ranking
        R("RUN STATUS", "STATUS DA RUN", "ESTADO DE LA PARTIDA", "ÉTAT DE LA PARTIE", "LAUF-STATUS", "STATO DELLA PARTITA"),
        R("CARDS THROWN", "CARTAS LANÇADAS", "CARTAS LANZADAS", "CARTES LANCÉES", "GEWORFENE KARTEN", "CARTE LANCIATE"),
        R("HITS", "ACERTOS", "ACIERTOS", "TOUCHES", "TREFFER", "COLPI"),
        R("CRITICALS", "CRÍTICOS", "CRÍTICOS", "CRITIQUES", "KRITISCH", "CRITICI"),
        R("DAMAGE DEALT", "DANO CAUSADO", "DAÑO HECHO", "DÉGÂTS INFLIGÉS", "SCHADEN VERURSACHT", "DANNI INFLITTI"),
        R("FUSIONS", "FUSÕES", "FUSIONES", "FUSIONS", "FUSIONEN", "FUSIONI"),
        R("RAISES WON", "TRUCOS GANHOS", "TRUCOS GANADOS", "RELANCES GAGNÉES", "RAISES GEWONNEN", "RILANCI VINTI"),
        R("DAMAGE TAKEN", "DANO SOFRIDO", "DAÑO RECIBIDO", "DÉGÂTS SUBIS", "SCHADEN ERLITTEN", "DANNI SUBITI"),
        R("HEALED", "CURADO", "CURADO", "SOIGNÉ", "GEHEILT", "CURATO"),
        R("CHIPS EARNED", "FICHAS GANHAS", "FICHAS GANADAS", "JETONS GAGNÉS", "CHIPS VERDIENT", "FICHE VINTE"),
        R("NO JOKERS YET", "NENHUM CORINGA AINDA", "AÚN SIN COMODINES", "PAS ENCORE DE JOKERS", "NOCH KEINE JOKER", "NESSUN JOLLY ANCORA"),
        R("WARDROBE", "GUARDA-ROUPA", "VESTUARIO", "GARDE-ROBE", "KLEIDERSCHRANK", "GUARDAROBA"),
        R("TROPHIES", "CONQUISTAS", "LOGROS", "SUCCÈS", "ERFOLGE", "OBIETTIVI"),
        R("HATS", "CHAPÉUS", "SOMBREROS", "CHAPEAUX", "HÜTE", "CAPPELLI"),
        R("OUTFITS", "ROUPAS", "ROPA", "TENUES", "OUTFITS", "ABITI"),
        R("ACHIEVEMENT", "CONQUISTA", "LOGRO", "SUCCÈS", "ERFOLG", "OBIETTIVO"),
        R("LOCKED", "BLOQUEADO", "BLOQUEADO", "VERROUILLÉ", "GESPERRT", "BLOCCATO"),
        R("OBSIDIAN", "OBSIDIANA", "OBSIDIANA", "OBSIDIENNE", "OBSIDIAN", "OSSIDIANA"),
        R("SHARK", "TUBARÃO", "TIBURÓN", "REQUIN", "HAI", "SQUALO"),
        R("YOUR BEST", "SEU RECORDE", "TU MEJOR", "TON MEILLEUR", "DEIN BESTES", "IL TUO MIGLIORE"),
        R("BOSSES", "CHEFES", "JEFES", "BOSS", "BOSSE", "BOSS"),
        R("PLAYER", "JOGADOR", "JUGADOR", "JOUEUR", "SPIELER", "GIOCATORE"),
        R("ACCURACY", "PRECISÃO", "PRECISIÓN", "PRÉCISION", "GENAUIGKEIT", "PRECISIONE"),
        R("TAP A ROW TO SEE THE RUN", "TOQUE NUMA LINHA PARA VER A RUN", "TOCA UNA FILA PARA VER LA PARTIDA", "TOUCHE UNE LIGNE POUR VOIR LA PARTIE", "TIPPE EINE ZEILE FÜR DEN LAUF", "TOCCA UNA RIGA PER VEDERE LA PARTITA"),
        // Nomes dos chefes (por último: "JACK {0}" pega "JACK ♠")
        R("TWIN JACKS", "VALETES GÊMEOS", "J GEMELOS", "VALETS JUMEAUX", "ZWILLINGSBUBEN", "FANTI GEMELLI"),
        R("ELDER JACK", "VALETE-MOR", "J MAYOR", "VALET AÎNÉ", "ALTER BUBE", "GRAN FANTE"),
        R("TWIN JACKS {0}", "VALETES GÊMEOS {0}", "J GEMELOS {0}", "VALETS JUMEAUX {0}", "ZWILLINGSBUBEN {0}", "FANTI GEMELLI {0}"),
        R("ELDER JACK {0}", "VALETE-MOR {0}", "J MAYOR {0}", "VALET AÎNÉ {0}", "ALTER BUBE {0}", "GRAN FANTE {0}"),
        R("JACK", "VALETE", "J", "VALET", "BUBE", "FANTE"),
        R("QUEEN", "DAMA", "REINA", "DAME", "DAME", "REGINA"),
        R("KING", "REI", "REY", "ROI", "KÖNIG", "RE"),
        R("JOKER", "CORINGA", "COMODÍN", "JOKER", "JOKER", "JOLLY"),
        R("JACK {0}", "VALETE {0}", "J {0}", "VALET {0}", "BUBE {0}", "FANTE {0}"),
        R("QUEEN {0}", "DAMA {0}", "REINA {0}", "DAME {0}", "DAME {0}", "REGINA {0}"),
        R("KING {0}", "REI {0}", "REY {0}", "ROI {0}", "KÖNIG {0}", "RE {0}"),
    };

    private class LangData
    {
        public Dictionary<string, string> exact = new Dictionary<string, string>();
        public List<(Regex regex, string template, bool[] numeric)> patterns = new List<(Regex, string, bool[])>();
    }

    private static LangData[] data;
    private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

    private static void Build()
    {
        data = new LangData[Count];
        for (int lang = 1; lang < Count; lang++)
        {
            var d = data[lang] = new LangData();
            foreach (var row in Table)
            {
                string en = row[0], tr = row[lang];
                if (!en.Contains("{")) { d.exact[en] = tr; continue; }
                // "HAND {n}/{n}" → ^HAND (\d+)/(\d+)$ ; os grupos voltam na mesma ordem no texto traduzido
                var kinds = new List<bool>();
                string rx = "^" + Regex.Replace(Regex.Escape(en).Replace("\\{", "{"), @"\{(n|0)\}", m =>
                {
                    kinds.Add(m.Groups[1].Value == "n");
                    return m.Groups[1].Value == "n" ? @"(\d+)" : "(.+)";
                }) + "$";
                int k = 0;
                string template = Regex.Replace(tr, @"\{(n|0)\}", _ => "{" + (k++) + "}");
                d.patterns.Add((new Regex(rx, RegexOptions.Singleline), template, kinds.ToArray()));
            }
        }
    }

    // Tradução de um texto em inglês (devolve o próprio texto se não houver tradução).
    public static string T(string en)
    {
        if (IsEnglish || string.IsNullOrEmpty(en)) return en;
        if (cache.TryGetValue(en, out var hit)) return hit;
        if (data == null) Build();
        string result = Translate(data[(int)Current], en, 0);
        if (cache.Count > 4000) cache.Clear();
        cache[en] = result;
        return result;
    }

    private static string Translate(LangData d, string s, int depth)
    {
        if (depth > 4 || string.IsNullOrWhiteSpace(s)) return s;
        if (d.exact.TryGetValue(s, out var tr)) return tr;

        // Dois ataques juntos: "DICE x4 + CHIP RAIN x5"
        if (s.Contains(" + "))
        {
            var parts = s.Split(new[] { " + " }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++) parts[i] = Translate(d, parts[i], depth + 1);
            return string.Join(" + ", parts);
        }

        foreach (var (regex, template, numeric) in d.patterns)
        {
            var m = regex.Match(s);
            if (!m.Success) continue;
            var args = new object[m.Groups.Count - 1];
            for (int g = 1; g < m.Groups.Count; g++)
                args[g - 1] = numeric[g - 1] ? m.Groups[g].Value : Translate(d, m.Groups[g].Value, depth + 1);
            return string.Format(template, args);
        }

        // Várias linhas: traduz cada uma
        if (s.Contains("\n"))
        {
            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = Translate(d, lines[i], depth + 1);
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
                sb.Append(piece, 0, lead).Append(Translate(d, core, depth + 1)).Append(piece, lead + core.Length, piece.Length - lead - core.Length);
            }
            return sb.ToString();
        }
        return s;
    }
}
