using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Largura da arena = largura da tela. O jogo foi desenhado para 16:9 (640×360 px de referência = mundo
// de 40 × 22,5 unidades, x de −20 a 20). Em tela MAIS LARGA (celular 20:9, monitor ultrawide) a câmera
// passa a mostrar mais mundo dos lados em vez de deixar faixas pretas, e as paredes vão junto para a
// borda: o espaço jogável vai até o fim da tela. A altura nunca muda (o nível é fixo em 22,5 de altura).
// Em tela mais estreita que 16:9 (tablet 4:3) fica como sempre foi: 40 de largura e faixas em cima/embaixo.
public static class Arena
{
    public const float BaseHalfWidth = 20f;   // metade da largura em 16:9
    public const float HalfHeight = 11.25f;
    private const int RefHeight = 360;        // px de referência da Pixel Perfect Camera (altura fixa)

    // Metade da largura visível/jogável (x vai de −HalfWidth a +HalfWidth). Só muda em Refresh().
    public static float HalfWidth { get; private set; } = BaseHalfWidth;

    // Avisa quando a largura mudou (janela redimensionada / tela cheia no navegador) para o nível
    // mover as paredes e o chão.
    public static event Action OnChanged;

    private static int lastW, lastH;

    // Chamado pelo PortalRunner ao carregar a cena e a cada frame (só faz algo se o tamanho da tela mudou).
    public static void Refresh(bool force = false)
    {
        if (!force && Screen.width == lastW && Screen.height == lastH) return;
        lastW = Screen.width;
        lastH = Screen.height;

        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        // Largura de referência em px, arredondada para número PAR (o centro da tela cai num pixel inteiro).
        int refWidth = Mathf.Max(640, Mathf.RoundToInt(RefHeight * aspect / 2f) * 2);
        float half = refWidth / 16f / 2f; // 16 px por unidade

        var cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out PixelPerfectCamera ppc))
        {
            // StretchFill estica a imagem de referência para preencher a tela mantendo a proporção.
            // Com a referência na MESMA proporção da tela, não sobra faixa preta nenhuma
            // (no máximo 1 px de arredondamento).
            ppc.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;
            ppc.refResolutionX = refWidth;
            ppc.refResolutionY = RefHeight;
        }

        if (Mathf.Approximately(half, HalfWidth)) return;
        HalfWidth = half;
        OnChanged?.Invoke();
    }
}
