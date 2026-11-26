using UnityEngine;

// Versos de carta (cosmético comprado na taverna): uma cor aplicada por cima do verso do baralho
// nas cartas arremessadas e na cortina entre os chefes.
public static class CardBacks
{
    public struct Back
    {
        public string name;   // inglês (o Loc traduz)
        public Color tint;
        public int cost;
    }

    public static readonly Back[] All =
    {
        new Back { name = "CLASSIC", tint = Color.white, cost = 0 },
        new Back { name = "ROYAL GOLD", tint = new Color(1f, 0.86f, 0.45f), cost = 60 },
        new Back { name = "EMERALD", tint = new Color(0.55f, 1f, 0.72f), cost = 60 },
        new Back { name = "CRIMSON", tint = new Color(1f, 0.55f, 0.55f), cost = 80 },
        new Back { name = "MIDNIGHT", tint = new Color(0.62f, 0.66f, 1f), cost = 80 },
    };

    public static Color Tint
    {
        get
        {
            int i = ProgressSave.SelectedBack;
            return i >= 0 && i < All.Length && ProgressSave.BackOwned(i) ? All[i].tint : Color.white;
        }
    }
}
