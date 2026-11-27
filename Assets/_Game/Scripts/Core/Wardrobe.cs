using UnityEngine;

// Guarda-roupa do aldeão (taverna → aba GUARDA-ROUPA): chapéus, roupas e versos de carta.
//  - Chapéu = um desenho por arquivo (Art/Player/Hats/villager_<chapéu>.aseprite, gerados pelo
//    Tools/villager_hats.lua); o jogo troca o Animator do personagem pelo do chapéu.
//  - Roupa = troca de cores (lenço, camisa, túnica, calça) na textura do personagem (VillagerLook).
// Itens com custo são comprados com fichas; os de conquista (achievement >= 0) só pela conquista.
// A ORDEM das listas importa (índice salvo no save e enviado ao ranking). Só adicione no FIM.
public static class Wardrobe
{
    public class Item
    {
        public string[] names;     // 6 idiomas
        public int cost;           // 0 = de graça
        public int achievement = -1;
        public string Name => Loc.Pick(names[0], names[1], names[2], names[3], names[4], names[5]);
        public bool Free => cost == 0 && achievement < 0;
    }

    public class Hat : Item { }

    public class Outfit : Item
    {
        public Color32 scarf, shirt, tunic, pants;   // cor do meio de cada parte (as sombras são calculadas)
    }

    private static string[] N(params string[] n) => n;

    public static readonly Hat[] Hats =
    {
        new Hat { names = N("STRAW HAT", "CHAPÉU DE PALHA", "SOMBRERO DE PAJA", "CHAPEAU DE PAILLE", "STROHHUT", "CAPPELLO DI PAGLIA") },
        new Hat { names = N("NO HAT", "SEM CHAPÉU", "SIN SOMBRERO", "SANS CHAPEAU", "KEIN HUT", "SENZA CAPPELLO") },
        new Hat { names = N("FLAT CAP", "BOINA", "BOINA", "CASQUETTE", "SCHIEBERMÜTZE", "COPPOLA"), cost = 30 },
        new Hat { names = N("FEATHER CAP", "GORRO DE PENA", "GORRO CON PLUMA", "TOQUE À PLUME", "FEDERMÜTZE", "BERRETTO PIUMATO"), cost = 60 },
        new Hat { names = N("TOP HAT", "CARTOLA", "CHISTERA", "HAUT-DE-FORME", "ZYLINDER", "CILINDRO"), cost = 90 },
        new Hat { names = N("JESTER HAT", "CHAPÉU DE BOBO", "GORRO DE BUFÓN", "BONNET DE BOUFFON", "NARRENKAPPE", "CAPPELLO DA GIULLARE"), achievement = Achievements.EndlessNight },
        new Hat { names = N("CROWN", "COROA", "CORONA", "COURONNE", "KRONE", "CORONA"), achievement = Achievements.Crowned },
    };

    public static readonly Outfit[] Outfits =
    {
        new Outfit { names = N("PEASANT", "CAMPONÊS", "CAMPESINO", "PAYSAN", "BAUER", "CONTADINO"),
            scarf = new Color32(104, 128, 58, 255), shirt = new Color32(170, 152, 116, 255), tunic = new Color32(116, 92, 58, 255), pants = new Color32(90, 68, 48, 255) },
        new Outfit { names = N("FARMER", "LAVRADOR", "GRANJERO", "FERMIER", "LANDWIRT", "AGRICOLTORE"), cost = 40,
            scarf = new Color32(200, 60, 52, 255), shirt = new Color32(214, 206, 184, 255), tunic = new Color32(64, 96, 160, 255), pants = new Color32(70, 82, 120, 255) },
        new Outfit { names = N("HUNTER", "CAÇADOR", "CAZADOR", "CHASSEUR", "JÄGER", "CACCIATORE"), cost = 60,
            scarf = new Color32(230, 130, 40, 255), shirt = new Color32(190, 170, 120, 255), tunic = new Color32(60, 110, 56, 255), pants = new Color32(96, 70, 44, 255) },
        new Outfit { names = N("BARD", "BARDO", "BARDO", "BARDE", "BARDE", "BARDO"), cost = 80,
            scarf = new Color32(220, 64, 90, 255), shirt = new Color32(236, 200, 90, 255), tunic = new Color32(120, 64, 160, 255), pants = new Color32(60, 44, 70, 255) },
        new Outfit { names = N("NIGHT", "NOTURNO", "NOCTURNO", "NOCTURNE", "NACHT", "NOTTURNO"), cost = 100,
            scarf = new Color32(150, 40, 50, 255), shirt = new Color32(120, 120, 132, 255), tunic = new Color32(44, 42, 52, 255), pants = new Color32(30, 28, 36, 255) },
        new Outfit { names = N("ROYAL", "REAL", "REAL", "ROYAL", "KÖNIGLICH", "REALE"), achievement = Achievements.AceSleeve,
            scarf = new Color32(240, 240, 240, 255), shirt = new Color32(240, 196, 64, 255), tunic = new Color32(176, 30, 44, 255), pants = new Color32(80, 20, 40, 255) },
        new Outfit { names = N("SHADOW", "SOMBRA", "SOMBRA", "OMBRE", "SCHATTEN", "OMBRA"), achievement = Achievements.HighRoller,
            scarf = new Color32(60, 220, 190, 255), shirt = new Color32(70, 60, 96, 255), tunic = new Color32(40, 26, 60, 255), pants = new Color32(24, 18, 36, 255) },
    };

    // ===================== Posse e escolha =====================
    public static bool HatOwned(int i) => i >= 0 && i < Hats.Length && (Hats[i].Free || ProgressSave.HatBought(i) ||
        (Hats[i].achievement >= 0 && Achievements.Has(Hats[i].achievement)));

    public static bool OutfitOwned(int i) => i >= 0 && i < Outfits.Length && (Outfits[i].Free || ProgressSave.OutfitBought(i) ||
        (Outfits[i].achievement >= 0 && Achievements.Has(Outfits[i].achievement)));

    public static int SelectedHat => HatOwned(ProgressSave.SelectedHat) ? ProgressSave.SelectedHat : 0;
    public static int SelectedOutfit => OutfitOwned(ProgressSave.SelectedOutfit) ? ProgressSave.SelectedOutfit : 0;

    // Visual do jogador no ranking: "chapéu.roupa.verso" (ex.: "6.5.2")
    public static string Code => $"{SelectedHat}.{SelectedOutfit}.{Mathf.Max(0, ProgressSave.SelectedBack)}";

    public static void Parse(string code, out int hat, out int outfit, out int back)
    {
        hat = outfit = back = 0;
        if (string.IsNullOrEmpty(code)) return;
        var parts = code.Split('.');
        if (parts.Length > 0) int.TryParse(parts[0], out hat);
        if (parts.Length > 1) int.TryParse(parts[1], out outfit);
        if (parts.Length > 2) int.TryParse(parts[2], out back);
        hat = Mathf.Clamp(hat, 0, Hats.Length - 1);
        outfit = Mathf.Clamp(outfit, 0, Outfits.Length - 1);
        back = Mathf.Clamp(back, 0, CardBacks.All.Length - 1);
    }

    // Debug (ponte): call Wardrobe.DebugWear "4.3" (chapéu.roupa, mesmo sem ter)
    public static string DebugWear(string code)
    {
        Parse(code, out int h, out int o, out _);
        ProgressSave.BuyHat(h);
        ProgressSave.BuyOutfit(o);
        ProgressSave.SelectedHat = h;
        ProgressSave.SelectedOutfit = o;
        return "wearing " + Code;
    }
}
