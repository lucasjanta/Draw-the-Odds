#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

// Ponte de automação do Editor (usada pelo Claude): lê comandos de Temp/ClaudeBridge/cmd.txt
// e escreve resultados/erros em arquivos na mesma pasta. Só existe no Editor, não vai para a build.
// Comandos (1 por linha): refresh | menu <caminho> | play | stop | shot <arquivo.png> | call <Tipo.Metodo> [arg] | save | clearlog
[InitializeOnLoad]
public static class ClaudeBridge
{
    private const string Dir = "Temp/ClaudeBridge";
    private static double nextPoll;
    private static readonly object fileLock = new object();

    static ClaudeBridge()
    {
        Directory.CreateDirectory(Dir);
        EditorApplication.update += Poll;
        CompilationPipeline.compilationStarted += _ => Write("compile.txt", "started " + DateTime.Now + "\n", false);
        CompilationPipeline.assemblyCompilationFinished += (asm, msgs) =>
        {
            var text = string.Join("\n", msgs.Select(m => $"{m.type}: {m.message}"));
            Write("compile.txt", $"[{Path.GetFileName(asm)}]\n{text}\n", true);
        };
        Application.logMessageReceivedThreaded += (cond, stack, type) =>
        {
            string extra = type == LogType.Exception || type == LogType.Error ? "\n" + stack : "";
            Write("console.txt", $"[{type}] {cond}{extra}\n", true);
        };
        Write("status.txt", "loaded " + DateTime.Now + " playing=" + EditorApplication.isPlaying + "\n", false);
    }

    private static void Write(string file, string text, bool append)
    {
        lock (fileLock)
        {
            try
            {
                var path = Path.Combine(Dir, file);
                if (append) File.AppendAllText(path, text); else File.WriteAllText(path, text);
            }
            catch { /* arquivo ocupado: ignora */ }
        }
    }

    // Quando true, força o loop do jogo a rodar mesmo com o Editor sem foco (para testes automáticos).
    private static bool forceLoop;

    private static void Poll()
    {
        if (forceLoop && EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.25;

        string cmdPath = Path.Combine(Dir, "cmd.txt");
        if (!File.Exists(cmdPath)) return;

        string[] lines;
        try { lines = File.ReadAllLines(cmdPath); File.Delete(cmdPath); }
        catch { return; }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            try { Write("out.txt", $"> {line}\n{Execute(line)}\n", true); }
            catch (Exception e) { Write("out.txt", $"> {line}\nERROR {e}\n", true); }
        }
    }

    private static string Execute(string line)
    {
        int sp = line.IndexOf(' ');
        string cmd = sp < 0 ? line : line.Substring(0, sp);
        string arg = sp < 0 ? "" : line.Substring(sp + 1).Trim();

        switch (cmd)
        {
            case "refresh":
                AssetDatabase.Refresh();
                return "ok";
            case "menu":
                return EditorApplication.ExecuteMenuItem(arg) ? "ok" : "menu not found";
            case "play":
                forceLoop = true;
                EditorApplication.isPlaying = true;
                return "ok";
            case "stop":
                forceLoop = false;
                EditorApplication.isPlaying = false;
                return "ok";
            case "shot":
                ScreenCapture.CaptureScreenshot(arg);
                return "ok";
            case "save":
                EditorSceneManager.SaveOpenScenes();
                return "ok";
            case "loop":
                forceLoop = arg != "off";
                return "forceLoop=" + forceLoop;
            case "clearlog":
                Write("console.txt", "", false);
                return "ok";
            case "call":
                return Call(arg);
            default:
                return "unknown command";
        }
    }

    // call Tipo.Metodo [argumento string]  — método estático, procurado em todos os assemblies.
    private static string Call(string arg)
    {
        int sp = arg.IndexOf(' ');
        string target = sp < 0 ? arg : arg.Substring(0, sp);
        string param = sp < 0 ? null : arg.Substring(sp + 1);
        int dot = target.LastIndexOf('.');
        string typeName = target.Substring(0, dot), methodName = target.Substring(dot + 1);

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = asm.GetType(typeName);
            if (type == null) continue;
            var m = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) return "method not found";
            var result = m.Invoke(null, m.GetParameters().Length == 0 ? null : new object[] { param });
            return "ok " + result;
        }
        return "type not found";
    }
}
#endif
