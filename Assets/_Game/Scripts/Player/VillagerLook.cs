using System.Collections.Generic;
using UnityEngine;

// Visual do aldeão escolhido no guarda-roupa (chapéu + roupa).
//  - Chapéu: o Animator do personagem continua o mesmo (aldeão de palha). Depois que ele escolhe o
//    quadro ("Frame_12"), este script troca pelo quadro de mesmo número do arquivo com o chapéu
//    (GameConfig.HatFrame) — todos os arquivos têm os mesmos quadros e tags.
//  - Roupa: troca as cores do lenço, camisa, colete e calça direto na textura do chapéu escolhido
//    (por isso os .aseprite do aldeão importam com Read/Write ligado). Ver VillagerPalette.
// Fica no objeto do Animator do player (o GameManager adiciona). Atualiza sozinho quando o save muda.
public class VillagerLook : MonoBehaviour
{
    private GameConfig config;
    private SpriteRenderer sr;
    private int hat, shownOutfit = -1, shownHat = -1;
    private readonly Dictionary<Sprite, int> frameOf = new Dictionary<Sprite, int>(); // quadro do aldeão de palha → número

    public static VillagerLook Attach(GameObject visual, GameConfig config)
    {
        var look = visual.GetComponent<VillagerLook>();
        if (look == null) look = visual.AddComponent<VillagerLook>();
        look.config = config;
        look.sr = visual.GetComponentInChildren<SpriteRenderer>();
        return look;
    }

    private void Start()
    {
        ProgressSave.OnChanged += Apply;
        Apply();
    }

    private void OnDestroy() => ProgressSave.OnChanged -= Apply;

    public void Apply()
    {
        if (this == null || config == null) return;
        hat = Wardrobe.SelectedHat;
        int outfit = Wardrobe.SelectedOutfit;
        if (hat == shownHat && outfit == shownOutfit) return;
        var first = config.HatSprite(hat);
        if (first != null) VillagerPalette.ApplyTo(first.texture, outfit);
        shownHat = hat;
        shownOutfit = outfit;
    }

    // Roda depois do Animator: troca o quadro pelo do chapéu escolhido.
    private void LateUpdate()
    {
        if (hat == 0 || sr == null || sr.sprite == null || config == null) return;
        var current = sr.sprite;
        if (!frameOf.TryGetValue(current, out int n))
        {
            n = -1;
            string name = current.name;
            int i = name.LastIndexOf('_');
            if (i >= 0 && int.TryParse(name.Substring(i + 1), out int parsed) && config.HatFrame(0, parsed) == current) n = parsed;
            frameOf[current] = n; // -1 = sprite que não é do aldeão de palha (já é de um chapéu)
        }
        if (n < 0) return;
        var replacement = config.HatFrame(hat, n);
        if (replacement != null) sr.sprite = replacement;
    }
}
