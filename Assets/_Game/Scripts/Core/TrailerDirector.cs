#if UNITY_EDITOR
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

// Só no Editor (não vai para a build): joga sozinho e grava os frames da tela para montar o
// vídeo de gameplay das lojas/portais. Os PNGs saem em Temp/Trailer/<nome>/ e o ffmpeg junta depois.
// Uso pela ponte: call TrailerDirector.Run "1 12 queen 2"  (chefe, segundos, nome da pasta, superSize)
public class TrailerDirector : MonoBehaviour
{
    private const int Fps = 30;

    private int bossIndex;
    private float seconds;
    private string folder;
    private int superSize;
    public static string Status = "idle";

    public static string Run(string arg)
    {
        var p = (arg ?? "").Split(' ');
        var go = new GameObject("TrailerDirector");
        var d = go.AddComponent<TrailerDirector>();
        d.bossIndex = p.Length > 0 ? int.Parse(p[0]) : 0;
        d.seconds = p.Length > 1 ? float.Parse(p[1], CultureInfo.InvariantCulture) : 12f;
        d.folder = Path.Combine("Temp", "Trailer", p.Length > 2 ? p[2] : "clip");
        d.superSize = p.Length > 3 ? int.Parse(p[3]) : 1;
        Status = "starting";
        return "trailer " + arg;
    }

    public static string GetStatus() => Status;

    private IEnumerator Start()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);

        // Tempo fixo por frame: o jogo anda 1/30 s por frame, não importa quanto demora salvar o PNG.
        Time.captureFramerate = Fps;

        GameManager.DebugBoss(bossIndex.ToString());
        var gm = GameManager.Instance;
        // Espera a cortina/introdução do chefe acabar
        while (gm.Boss == null || !gm.CanAct) yield return null;
        gm.PlayerHealth.forcedInvulnerable = true; // sem morrer no meio do vídeo
        gm.Boss.CannotDie = true;
        yield return new WaitForSeconds(0.4f);

        StartCoroutine(Bot(gm));

        int total = Mathf.RoundToInt(seconds * Fps);
        for (int i = 0; i < total; i++)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture(superSize);
            File.WriteAllBytes(Path.Combine(folder, $"f_{i:00000}.png"), tex.EncodeToPNG());
            Destroy(tex);
            Status = $"recording {i + 1}/{total}";
        }

        Time.captureFramerate = 0;
        VirtualInput.Clear();
        Status = "done " + total + " frames (" + Screen.width * superSize + "x" + Screen.height * superSize + ")";
        Destroy(gameObject);
    }

    // "Jogador" automático: anda, pula, escolhe a carta mais próxima do número (às vezes erra de propósito)
    // e faz uma fusão no meio para aparecer no vídeo.
    private IEnumerator Bot(GameManager gm)
    {
        var cards = gm.Player.GetComponent<PlayerCards>();
        float nextMove = 0f, nextJump = 1f, nextThrow = 0.3f;
        float target = 0f;
        int throws = 0;
        bool fused = false;
        float t = 0f;

        while (true)
        {
            t += Time.deltaTime;
            var p = gm.Player.transform.position;

            // Movimento: escolhe um ponto e anda até ele
            if (t >= nextMove) { target = Random.Range(-15f, 6f); nextMove = t + Random.Range(1.2f, 2.2f); }
            float dx = target - p.x;
            VirtualInput.Move = new Vector2(Mathf.Abs(dx) > 0.6f ? Mathf.Sign(dx) : 0f, 0f);
            if (t >= nextJump) { VirtualInput.PressJump(); VirtualInput.JumpHeld = true; nextJump = t + Random.Range(1.0f, 2.0f); }
            if (VirtualInput.JumpHeld && t > nextJump - 0.75f) VirtualInput.JumpHeld = false;

            // Fusão uma vez, depois de alguns arremessos (mostra a câmera lenta e a soma)
            if (!fused && throws >= 4 && gm.CanAct && gm.Hand.Count >= 2)
            {
                fused = true;
                VirtualInput.Move = Vector2.zero;
                if (gm.Fusion.Begin(PowerType.Add))
                {
                    // A fusão PARA o tempo (timeScale 0): WaitForSeconds nunca acabaria, então conta frames.
                    // Escolhe 2 cartas que somadas fiquem perto do número secreto.
                    PickFusionPair(gm, out int ia, out int ib);
                    yield return Frames(10);
                    gm.Hand.Select(ia);
                    yield return Frames(6);
                    gm.Fusion.PutInSlot(true);
                    yield return Frames(10);
                    gm.Hand.Select(ib);
                    yield return Frames(6);
                    gm.Fusion.PutInSlot(false);
                    yield return Frames(14);
                    gm.Fusion.TryConfirm();
                    while (gm.Fusion.Active) yield return null; // animação do merge
                    yield return Frames(3);
                    // o resultado é lançado direto no chefe
                    gm.Hand.Select(gm.Hand.Count - 1);
                }
                nextThrow = t + 0.4f;
            }

            if (t >= nextThrow && gm.CanAct && gm.Boss != null && !gm.Fusion.Active)
            {
                if (gm.Hand.IsEmpty) gm.Redraw();
                else
                {
                    gm.Hand.Select(PickCard(gm, throws));
                    Vector2 screen = Camera.main.WorldToScreenPoint(gm.Boss.transform.position);
                    if (cards.ThrowAtScreen(screen)) throws++;
                }
                nextThrow = t + Random.Range(0.55f, 0.85f);
            }
            yield return null;
        }
    }

    private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

    private static void PickFusionPair(GameManager gm, out int a, out int b)
    {
        int secret = gm.Boss.SecretNumber, best = int.MaxValue;
        a = 0; b = 1;
        for (int i = 0; i < gm.Hand.Count; i++)
            for (int j = i + 1; j < gm.Hand.Count; j++)
            {
                int d = Mathf.Abs(gm.Hand.Cards[i].value + gm.Hand.Cards[j].value - secret);
                if (d < best) { best = d; a = i; b = j; }
            }
    }

    // Primeiros arremessos meio aleatórios (pistas ▲▼ aparecem), depois cada vez mais perto do número.
    private static int PickCard(GameManager gm, int throws)
    {
        int secret = gm.Boss.SecretNumber, best = 0, bestDist = int.MaxValue;
        if (throws < 2) return Random.Range(0, gm.Hand.Count);
        for (int i = 0; i < gm.Hand.Count; i++)
        {
            int d = Mathf.Abs(gm.Hand.Cards[i].value - secret);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }
}
#endif
