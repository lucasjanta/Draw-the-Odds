using UnityEngine;

// Encolhe um RectTransform (que estava esticado na tela toda) para a "área segura" do aparelho:
// a parte da tela sem o furo da câmera / entalhe (notch) e sem as bordas arredondadas.
// O jogo desenha na tela inteira (inclusive atrás do furo), mas os botões de toque e os painéis do HUD
// ficam dentro desta área para nada importante ficar escondido. No PC/navegador a área segura é a
// tela toda, então não muda nada.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform rect;
    private Rect applied;
    private Vector2Int appliedScreen;

    public static SafeArea Add(RectTransform target)
    {
        var s = target.gameObject.AddComponent<SafeArea>();
        s.Apply();
        return s;
    }

    private void Awake() => rect = (RectTransform)transform;

    private void Update() => Apply();

    private void Apply()
    {
        if (rect == null) rect = (RectTransform)transform;
        Rect safe = Screen.safeArea;
        var screen = new Vector2Int(Screen.width, Screen.height);
        if (safe == applied && screen == appliedScreen) return; // só recalcula quando muda
        applied = safe;
        appliedScreen = screen;
        if (screen.x <= 0 || screen.y <= 0) return;

        // Âncoras em fração da tela (0..1): o retângulo passa a cobrir só a área segura.
        rect.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
        rect.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
