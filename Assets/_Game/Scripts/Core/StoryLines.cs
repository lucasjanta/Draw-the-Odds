using System.Collections.Generic;
using UnityEngine;

// Uma fala da história: quem fala (nome já traduzido, vazio = narrador), cor do nome e o texto.
public struct StoryLine
{
    public string speaker;
    public Color color;
    public string text;
    public string portrait; // "joker", "jack", "queen", "king", "ace" ou vazio (narrador). StoryDialog acha o sprite.

    public StoryLine(string speaker, Color color, string text, string portrait = "")
    {
        this.speaker = speaker;
        this.color = color;
        this.text = text;
        this.portrait = portrait;
    }
}

// Roteiro da história "O Reino da Banca" (ver Docs/HISTORIA.md).
// Cada cena aparece UMA vez (fica salvo no PlayerPrefs); quem já viu não perde tempo nas runs seguintes.
// Os textos são montados na hora (Loc.Pick), então seguem o idioma atual do jogo.
// Colunas do Loc.Pick: inglês, português, espanhol, francês, alemão, italiano.
public static class StoryLines
{
    private const string PrefPrefix = "dto_story_";

    // Ids das cenas
    public const string Intro = "intro";
    public const string TutorialIntro = "tutorial_intro";
    public const string TutorialOutro = "tutorial_outro";
    public const string Ending = "ending";
    public const string AceHint = "ace_hint";         // Bobo dá a pista dos boatos (final normal)
    public const string AceReveal = "ace_reveal";     // o Ás aparece depois do Rei
    public const string AceDefeat = "ace_defeat";
    public const string EndingThrone = "ending_throne";
    public const string EndingFree = "ending_free";
    public static string BossIntro(int index) => "boss" + index + "_intro";
    public static string BossDefeat(int index) => "boss" + index + "_defeat";

    private static readonly string[] AllIds =
    {
        Intro, TutorialIntro, TutorialOutro, Ending, AceHint, AceReveal, AceDefeat, EndingThrone, EndingFree,
        "boss0_intro", "boss1_intro", "boss2_intro", "boss0_defeat", "boss1_defeat", "boss2_defeat",
    };

    public static bool Seen(string id) => PlayerPrefs.GetInt(PrefPrefix + id, 0) == 1;

    public static void MarkSeen(string id)
    {
        PlayerPrefs.SetInt(PrefPrefix + id, 1);
        PlayerPrefs.Save();
    }

    // ===================== Quem fala =====================
    private static readonly Color JokerColor = new Color32(0xB5, 0x7C, 0xFF, 0xFF);
    private static readonly Color RoyalColor = new Color32(0xFF, 0xD4, 0x47, 0xFF);

    private static StoryLine Narrator(string text) => new StoryLine("", Color.white, text);
    private static readonly Color AceColor = new Color32(0x4C, 0xFF, 0x9A, 0xFF);

    private static StoryLine Joker(string text) => new StoryLine(Loc.T("JOKER"), JokerColor, text, "joker");
    private static StoryLine Royal(string nameEn, string text) => new StoryLine(Loc.T(nameEn), RoyalColor, text, nameEn.ToLowerInvariant());
    private static StoryLine Ace(string text) => new StoryLine(Loc.T("ACE"), AceColor, text, "ace");

    // Cena pelo id (null = não existe).
    public static List<StoryLine> Get(string id)
    {
        switch (id)
        {
            case Intro: return IntroLines();
            case TutorialIntro: return TutorialIntroLines();
            case TutorialOutro: return TutorialOutroLines();
            case Ending: return EndingLines();
            case AceHint: return AceHintLines();
            case AceReveal: return AceRevealLines();
            case AceDefeat: return AceDefeatLines();
            case EndingThrone: return ThroneLines();
            case EndingFree: return FreeLines();
            case "boss0_intro": return JackIntro();
            case "boss1_intro": return QueenIntro();
            case "boss2_intro": return KingIntro();
            case "boss0_defeat": return JackDefeat();
            case "boss1_defeat": return QueenDefeat();
            case "boss2_defeat": return KingDefeat();
        }
        return null;
    }

    // ===================== Abertura =====================
    private static List<StoryLine> IntroLines() => new List<StoryLine>
    {
        Narrator(Loc.Pick(
            "In the Kingdom of Cards, the Royal Family decides everything\nwith a card game they <color=#FFD447>never lose</color>.",
            "No Reino de Baralho, a Família Real decide tudo\nnum jogo de cartas que eles <color=#FFD447>nunca perdem</color>.",
            "En el Reino de los Naipes, la Familia Real lo decide todo\ncon un juego de cartas que <color=#FFD447>nunca pierde</color>.",
            "Au Royaume des Cartes, la Famille Royale décide de tout\npar un jeu de cartes qu'elle <color=#FFD447>ne perd jamais</color>.",
            "Im Kartenkönigreich entscheidet die Königsfamilie alles\nmit einem Kartenspiel, das sie <color=#FFD447>nie verliert</color>.",
            "Nel Regno delle Carte, la Famiglia Reale decide tutto\ncon un gioco di carte che <color=#FFD447>non perde mai</color>.")),
        Narrator(Loc.Pick(
            "Taxes, harvests, who goes to the gallows...\nall settled at the royal table.",
            "Impostos, colheitas, quem vai pra forca...\ntudo resolvido na mesa real.",
            "Impuestos, cosechas, quién va a la horca...\ntodo se decide en la mesa real.",
            "Impôts, récoltes, qui finit à la potence...\ntout se joue à la table royale.",
            "Steuern, Ernten, wer an den Galgen kommt...\nalles wird am königlichen Tisch entschieden.",
            "Tasse, raccolti, chi finisce sulla forca...\ntutto si decide al tavolo reale.")),
        Narrator(Loc.Pick(
            "They call it luck. You, a humble <color=#3CC86E>Seven of Clubs</color>,\nknow better: it's a <color=#FF6A2E>BLUFF</color>.",
            "Eles chamam de sorte. Você, um humilde <color=#3CC86E>Sete de Paus</color>,\nsabe a verdade: é <color=#FF6A2E>BLEFE</color>.",
            "Lo llaman suerte. Tú, un humilde <color=#3CC86E>Siete de Tréboles</color>,\nsabes la verdad: es un <color=#FF6A2E>FAROL</color>.",
            "Ils appellent ça la chance. Toi, humble <color=#3CC86E>Sept de Trèfle</color>,\ntu sais la vérité : c'est du <color=#FF6A2E>BLUFF</color>.",
            "Sie nennen es Glück. Du, eine einfache <color=#3CC86E>Kreuz-Sieben</color>,\nweißt es besser: Es ist ein <color=#FF6A2E>BLUFF</color>.",
            "La chiamano fortuna. Tu, un umile <color=#3CC86E>Sette di Fiori</color>,\nsai la verità: è un <color=#FF6A2E>BLUFF</color>.")),
        Narrator(Loc.Pick(
            "Every royal hides a <color=#FFD447>secret number</color>.\nFind it, call their bluff, and climb to the throne.",
            "Cada nobre esconde um <color=#FFD447>número secreto</color>.\nDescubra, pegue o blefe e suba até o trono.",
            "Cada noble esconde un <color=#FFD447>número secreto</color>.\nDescúbrelo, destapa su farol y sube hasta el trono.",
            "Chaque noble cache un <color=#FFD447>nombre secret</color>.\nTrouve-le, démasque son bluff et grimpe jusqu'au trône.",
            "Jeder Adlige versteckt eine <color=#FFD447>geheime Zahl</color>.\nFinde sie, entlarve den Bluff und erklimm den Thron.",
            "Ogni nobile nasconde un <color=#FFD447>numero segreto</color>.\nTrovalo, smaschera il bluff e sali fino al trono.")),
    };

    // ===================== Tutorial (Coringa = bobo da corte) =====================
    private static List<StoryLine> TutorialIntroLines() => new List<StoryLine>
    {
        Joker(Loc.Pick(
            "Psst! Over here, villager. I'm the court jester...\nI hear every secret in this palace.",
            "Psiu! Aqui, aldeão. Eu sou o bobo da corte...\nescuto todos os segredos deste palácio.",
            "¡Psst! Aquí, aldeano. Soy el bufón de la corte...\noigo todos los secretos de este palacio.",
            "Psst ! Par ici, villageois. Je suis le bouffon de la cour...\nj'entends tous les secrets de ce palais.",
            "Psst! Hier drüben, Dorfbewohner. Ich bin der Hofnarr...\nich höre jedes Geheimnis in diesem Palast.",
            "Psst! Di qua, popolano. Sono il giullare di corte...\nsento ogni segreto di questo palazzo.")),
        Joker(Loc.Pick(
            "Want to beat the royals? First, beat me.\nConsider it... training.",
            "Quer derrotar a realeza? Primeiro, me derrote.\nConsidere um... treino.",
            "¿Quieres vencer a la realeza? Primero, véncome a mí.\nConsidéralo... entrenamiento.",
            "Tu veux battre les nobles ? D'abord, bats-moi.\nDisons que c'est... un entraînement.",
            "Du willst die Royals schlagen? Schlag erst mich.\nBetrachte es als... Training.",
            "Vuoi battere i reali? Prima batti me.\nConsideralo... un allenamento.")),
    };

    private static List<StoryLine> TutorialOutroLines() => new List<StoryLine>
    {
        Joker(Loc.Pick(
            "Not bad! The Jack is waiting upstairs.\nRemember: royals lie with a <color=#FFD447>poker face</color>.",
            "Nada mal! O Valete te espera lá em cima.\nLembre: a realeza mente com <color=#FFD447>cara de paisagem</color>.",
            "¡Nada mal! La J te espera arriba.\nRecuerda: la realeza miente con <color=#FFD447>cara de póker</color>.",
            "Pas mal ! Le Valet t'attend en haut.\nSouviens-toi : les nobles mentent avec un <color=#FFD447>visage de marbre</color>.",
            "Nicht schlecht! Der Bube wartet oben.\nDenk dran: Royals lügen mit <color=#FFD447>Pokerface</color>.",
            "Niente male! Il Fante ti aspetta di sopra.\nRicorda: i reali mentono con la <color=#FFD447>faccia da poker</color>.")),
    };

    // ===================== Valete, o Coletor =====================
    private static List<StoryLine> JackIntro() => new List<StoryLine>
    {
        Royal("JACK", Loc.Pick(
            "A peasant at the royal table?\nYour <color=#FFD447>taxes</color> are due, villager.",
            "Um plebeu na mesa real?\nSeus <color=#FFD447>impostos</color> estão atrasados, aldeão.",
            "¿Un plebeyo en la mesa real?\nTus <color=#FFD447>impuestos</color> están vencidos, aldeano.",
            "Un manant à la table royale ?\nTes <color=#FFD447>impôts</color> sont en retard, villageois.",
            "Ein Bauer am königlichen Tisch?\nDeine <color=#FFD447>Steuern</color> sind fällig, Dorfbewohner.",
            "Un plebeo al tavolo reale?\nLe tue <color=#FFD447>tasse</color> sono scadute, popolano.")),
        Joker(Loc.Pick(
            "The Jack plays it straight: his number never changes on its own.\nFollow the <color=#FF8A3D>▲</color> <color=#5A9BFF>▼</color> hints!",
            "O Valete joga limpo: o número dele não muda sozinho.\nSiga as pistas <color=#FF8A3D>▲</color> <color=#5A9BFF>▼</color>!",
            "La J juega limpio: su número no cambia solo.\n¡Sigue las pistas <color=#FF8A3D>▲</color> <color=#5A9BFF>▼</color>!",
            "Le Valet joue franc-jeu : son nombre ne change pas tout seul.\nSuis les indices <color=#FF8A3D>▲</color> <color=#5A9BFF>▼</color> !",
            "Der Bube spielt ehrlich: Seine Zahl ändert sich nicht von selbst.\nFolge den Hinweisen <color=#FF8A3D>▲</color> <color=#5A9BFF>▼</color>!",
            "Il Fante gioca pulito: il suo numero non cambia da solo.\nSegui gli indizi <color=#FF8A3D>▲</color> <color=#5A9BFF>▼</color>!")),
    };

    private static List<StoryLine> JackDefeat() => new List<StoryLine>
    {
        Royal("JACK", Loc.Pick(
            "Fine... keep your coins.\nThe Queen won't be so generous.",
            "Tá bom... fique com suas moedas.\nA Dama não vai ser tão generosa.",
            "Está bien... quédate tus monedas.\nLa Reina no será tan generosa.",
            "Soit... garde tes pièces.\nLa Dame ne sera pas aussi généreuse.",
            "Schon gut... behalte deine Münzen.\nDie Dame wird nicht so großzügig sein.",
            "Va bene... tieniti le monete.\nLa Regina non sarà così generosa.")),
    };

    // ===================== Dama, a Intrigante =====================
    private static List<StoryLine> QueenIntro() => new List<StoryLine>
    {
        Royal("QUEEN", Loc.Pick(
            "Darling, everything I say is true...\nuntil it isn't.",
            "Querido, tudo o que eu digo é verdade...\naté deixar de ser.",
            "Querido, todo lo que digo es verdad...\nhasta que deja de serlo.",
            "Mon cher, tout ce que je dis est vrai...\njusqu'à ce que ça ne le soit plus.",
            "Mein Lieber, alles, was ich sage, ist wahr...\nbis es das nicht mehr ist.",
            "Caro, tutto ciò che dico è vero...\nfinché non lo è più.")),
        Joker(Loc.Pick(
            "Careful: when she's hurt, she changes her story.\nAnd her <color=#FF6A2E>number</color>.",
            "Cuidado: quando ela se machuca, muda a versão da história.\nE o <color=#FF6A2E>número</color>.",
            "Cuidado: cuando la hieren, cambia su versión.\nY su <color=#FF6A2E>número</color>.",
            "Attention : quand elle est blessée, elle change sa version.\nEt son <color=#FF6A2E>nombre</color>.",
            "Vorsicht: Wenn sie verletzt ist, ändert sie ihre Geschichte.\nUnd ihre <color=#FF6A2E>Zahl</color>.",
            "Attento: quando è ferita, cambia versione.\nE anche <color=#FF6A2E>numero</color>.")),
    };

    private static List<StoryLine> QueenDefeat() => new List<StoryLine>
    {
        Royal("QUEEN", Loc.Pick(
            "You... read me? Nobody reads me!",
            "Você... me leu? Ninguém me lê!",
            "¿Tú... me leíste? ¡Nadie me lee!",
            "Tu... m'as percée à jour ? Personne ne me perce à jour !",
            "Du... hast mich durchschaut? Niemand durchschaut mich!",
            "Tu... mi hai letta? Nessuno mi legge!")),
    };

    // ===================== Rei, o Propagandista =====================
    private static List<StoryLine> KingIntro() => new List<StoryLine>
    {
        Royal("KING", Loc.Pick(
            "By royal decree, the truth is whatever I say.\nAnd I change my mind <color=#FFD447>every 25 seconds</color>.",
            "Por decreto real, a verdade é o que eu disser.\nE eu mudo de ideia <color=#FFD447>a cada 25 segundos</color>.",
            "Por decreto real, la verdad es lo que yo diga.\nY cambio de opinión <color=#FFD447>cada 25 segundos</color>.",
            "Par décret royal, la vérité est ce que je dis.\nEt je change d'avis <color=#FFD447>toutes les 25 secondes</color>.",
            "Per königlichem Erlass ist die Wahrheit, was ich sage.\nUnd ich ändere meine Meinung <color=#FFD447>alle 25 Sekunden</color>.",
            "Per decreto reale, la verità è ciò che dico io.\nE cambio idea <color=#FFD447>ogni 25 secondi</color>.")),
        Joker(Loc.Pick(
            "Royal propaganda! His number rewrites itself on schedule.\nStrike fast, villager!",
            "Propaganda real! O número dele se reescreve no relógio.\nSeja rápido, aldeão!",
            "¡Propaganda real! Su número se reescribe a horario.\n¡Golpea rápido, aldeano!",
            "Propagande royale ! Son nombre se réécrit à heure fixe.\nFrappe vite, villageois !",
            "Königliche Propaganda! Seine Zahl schreibt sich nach Plan neu.\nSchlag schnell zu, Dorfbewohner!",
            "Propaganda reale! Il suo numero si riscrive a orario.\nColpisci in fretta, popolano!")),
    };

    private static List<StoryLine> KingDefeat() => new List<StoryLine>
    {
        Royal("KING", Loc.Pick(
            "Impossible... <color=#FFD447>the house always wins!</color>",
            "Impossível... <color=#FFD447>a casa sempre ganha!</color>",
            "Imposible... <color=#FFD447>¡la casa siempre gana!</color>",
            "Impossible... <color=#FFD447>la maison gagne toujours !</color>",
            "Unmöglich... <color=#FFD447>die Bank gewinnt immer!</color>",
            "Impossibile... <color=#FFD447>il banco vince sempre!</color>")),
    };

    // ===================== Final (gancho para o Ás secreto) =====================
    private static List<StoryLine> EndingLines() => new List<StoryLine>
    {
        Joker(Loc.Pick(
            "The King has folded. Cards on the table, villager:\nthe kingdom is yours.",
            "O Rei saiu da mesa. Cartas na mesa, aldeão:\no reino é seu.",
            "El Rey se retira. Cartas sobre la mesa, aldeano:\nel reino es tuyo.",
            "Le Roi se couche. Cartes sur table, villageois :\nle royaume est à toi.",
            "Der König steigt aus. Karten auf den Tisch, Dorfbewohner:\nDas Königreich gehört dir.",
            "Il Re passa. Carte in tavola, popolano:\nil regno è tuo.")),
        Joker(Loc.Pick(
            "But tell me... who dealt the King his cards?",
            "Mas me diga... quem deu as cartas para o Rei?",
            "Pero dime... ¿quién le repartió las cartas al Rey?",
            "Mais dis-moi... qui a distribué ses cartes au Roi ?",
            "Aber sag mal... wer hat dem König seine Karten gegeben?",
            "Ma dimmi... chi ha dato le carte al Re?")),
        Narrator(Loc.Pick(
            "Somewhere beneath the throne, an <color=#FFD447>Ace</color> waits up a sleeve...",
            "Em algum lugar sob o trono, um <color=#FFD447>Ás</color> espera na manga...",
            "En algún lugar bajo el trono, un <color=#FFD447>As</color> espera en la manga...",
            "Quelque part sous le trône, un <color=#FFD447>As</color> attend dans une manche...",
            "Irgendwo unter dem Thron wartet ein <color=#FFD447>Ass</color> im Ärmel...",
            "Da qualche parte sotto il trono, un <color=#FFD447>Asso</color> aspetta nella manica...")),
    };

    // ===================== O Ás secreto =====================
    private static List<StoryLine> AceHintLines() => new List<StoryLine>
    {
        Joker(Loc.Pick(
            "Psst... the tavern talks. Find all 3 <color=#FFD447>rumors about the King</color>\nand the real dealer will have to show his face.",
            "Psiu... a taverna fala. Descubra os 3 <color=#FFD447>boatos do Rei</color>\ne o verdadeiro dono da mesa vai ter que aparecer.",
            "Psst... la taberna habla. Descubre los 3 <color=#FFD447>rumores del Rey</color>\ny el verdadero crupier tendrá que dar la cara.",
            "Psst... la taverne parle. Trouve les 3 <color=#FFD447>rumeurs sur le Roi</color>\net le vrai donneur devra se montrer.",
            "Psst... die Taverne redet. Finde alle 3 <color=#FFD447>Gerüchte über den König</color>\nund der wahre Geber muss sich zeigen.",
            "Psst... la taverna parla. Trova le 3 <color=#FFD447>voci sul Re</color>\ne il vero mazziere dovrà mostrarsi.")),
    };

    private static List<StoryLine> AceRevealLines() => new List<StoryLine>
    {
        Narrator(Loc.Pick(
            "The King's crown rolls across the floor...\nand a card slides out of his sleeve.",
            "A coroa do Rei rola pelo chão...\ne uma carta escorrega da manga dele.",
            "La corona del Rey rueda por el suelo...\ny una carta se desliza de su manga.",
            "La couronne du Roi roule sur le sol...\net une carte glisse de sa manche.",
            "Die Krone des Königs rollt über den Boden...\nund eine Karte rutscht aus seinem Ärmel.",
            "La corona del Re rotola sul pavimento...\ne una carta scivola dalla sua manica.")),
        Ace(Loc.Pick(
            "The King? Just a card in my hand.\nI am the <color=#4CFF9A>Ace up the Sleeve</color>. I dealt every game.",
            "O Rei? Só uma carta na minha mão.\nEu sou o <color=#4CFF9A>Ás na Manga</color>. Eu dei as cartas de todo jogo.",
            "¿El Rey? Solo una carta en mi mano.\nSoy el <color=#4CFF9A>As en la Manga</color>. Repartí cada partida.",
            "Le Roi ? Juste une carte dans ma main.\nJe suis l'<color=#4CFF9A>As dans la Manche</color>. J'ai distribué chaque partie.",
            "Der König? Nur eine Karte in meiner Hand.\nIch bin das <color=#4CFF9A>Ass im Ärmel</color>. Ich habe jedes Spiel gegeben.",
            "Il Re? Solo una carta nella mia mano.\nSono l'<color=#4CFF9A>Asso nella Manica</color>. Ho distribuito ogni partita.")),
        Ace(Loc.Pick(
            "I hide <color=#FFD447>TWO numbers</color>. Find the first, then the second...\nif your luck lasts that long.",
            "Eu escondo <color=#FFD447>DOIS números</color>. Ache o primeiro, depois o segundo...\nse a sua sorte durar até lá.",
            "Escondo <color=#FFD447>DOS números</color>. Encuentra el primero, luego el segundo...\nsi tu suerte dura tanto.",
            "Je cache <color=#FFD447>DEUX nombres</color>. Trouve le premier, puis le second...\nsi ta chance tient jusque-là.",
            "Ich verstecke <color=#FFD447>ZWEI Zahlen</color>. Finde die erste, dann die zweite...\nwenn dein Glück so lange hält.",
            "Nascondo <color=#FFD447>DUE numeri</color>. Trova il primo, poi il secondo...\nse la tua fortuna dura tanto.")),
    };

    private static List<StoryLine> AceDefeatLines() => new List<StoryLine>
    {
        Ace(Loc.Pick(
            "Both numbers... called. Fine, villager.\nThe table is yours. What will you do with it?",
            "Os dois números... descobertos. Tudo bem, aldeão.\nA mesa é sua. O que você vai fazer com ela?",
            "Los dos números... descubiertos. Está bien, aldeano.\nLa mesa es tuya. ¿Qué harás con ella?",
            "Les deux nombres... démasqués. Soit, villageois.\nLa table est à toi. Qu'en feras-tu ?",
            "Beide Zahlen... aufgedeckt. Na gut, Dorfbewohner.\nDer Tisch gehört dir. Was machst du damit?",
            "Entrambi i numeri... scoperti. Va bene, popolano.\nIl tavolo è tuo. Che cosa ne farai?")),
    };

    private static List<StoryLine> ThroneLines() => new List<StoryLine>
    {
        Narrator(Loc.Pick(
            "You sit on the throne. The deck feels warm in your hands.\nThe kingdom keeps dealing... and now <color=#FFD447>you</color> are the house.",
            "Você senta no trono. O baralho parece quente nas suas mãos.\nO reino continua dando cartas... e agora a casa é <color=#FFD447>você</color>.",
            "Te sientas en el trono. La baraja se siente cálida en tus manos.\nEl reino sigue repartiendo... y ahora la casa eres <color=#FFD447>tú</color>.",
            "Tu t'assois sur le trône. Le paquet est chaud dans tes mains.\nLe royaume continue de distribuer... et la maison, c'est <color=#FFD447>toi</color>.",
            "Du setzt dich auf den Thron. Das Deck fühlt sich warm an.\nDas Königreich gibt weiter... und jetzt bist <color=#FFD447>du</color> die Bank.",
            "Ti siedi sul trono. Il mazzo è caldo tra le tue mani.\nIl regno continua a distribuire... e ora il banco sei <color=#FFD447>tu</color>.")),
        Joker(Loc.Pick(
            "Long live the new King! ...I'll be watching your sleeves.",
            "Vida longa ao novo Rei! ...Vou ficar de olho nas suas mangas.",
            "¡Larga vida al nuevo Rey! ...Vigilaré tus mangas.",
            "Longue vie au nouveau Roi ! ...Je surveillerai tes manches.",
            "Lang lebe der neue König! ...Ich behalte deine Ärmel im Auge.",
            "Lunga vita al nuovo Re! ...Terrò d'occhio le tue maniche.")),
    };

    private static List<StoryLine> FreeLines() => new List<StoryLine>
    {
        Narrator(Loc.Pick(
            "You tear the deck in half. Cards rain over the square.\nNo more secret numbers. No more house.",
            "Você rasga o baralho ao meio. Cartas chovem sobre a praça.\nChega de números secretos. Chega de casa.",
            "Rompes la baraja por la mitad. Llueven cartas sobre la plaza.\nSe acabaron los números secretos. Se acabó la casa.",
            "Tu déchires le paquet en deux. Les cartes pleuvent sur la place.\nFini les nombres secrets. Finie la maison.",
            "Du zerreißt das Deck. Karten regnen über den Platz.\nKeine geheimen Zahlen mehr. Keine Bank mehr.",
            "Strappi il mazzo a metà. Le carte piovono sulla piazza.\nBasta numeri segreti. Basta banco.")),
        Joker(Loc.Pick(
            "A kingdom with no cards... I'll need a new job.\nTavern juggler, maybe?",
            "Um reino sem cartas... vou precisar de um emprego novo.\nMalabarista de taverna, talvez?",
            "Un reino sin cartas... necesitaré otro trabajo.\n¿Malabarista de taberna, quizás?",
            "Un royaume sans cartes... il me faudra un nouveau métier.\nJongleur de taverne, peut-être ?",
            "Ein Königreich ohne Karten... ich brauche einen neuen Job.\nTavernen-Jongleur vielleicht?",
            "Un regno senza carte... mi servirà un nuovo lavoro.\nGiocoliere da taverna, forse?")),
    };

    // Taverna → REVER HISTÓRIA: todas as cenas voltam a aparecer (uma vez cada) nas próximas runs.
    public static void ResetAll()
    {
        foreach (var id in AllIds) PlayerPrefs.DeleteKey(PrefPrefix + id);
        PlayerPrefs.Save();
    }

    // ===================== Debug (ponte do Editor) =====================
    // call StoryLines.DebugReset → todas as cenas voltam a aparecer
    public static string DebugReset()
    {
        foreach (var id in AllIds) PlayerPrefs.DeleteKey(PrefPrefix + id);
        PlayerPrefs.Save();
        return "story reset";
    }
}
