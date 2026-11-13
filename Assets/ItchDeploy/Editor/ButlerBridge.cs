using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class ButlerBridge
    {
        private static string _butlerPath;
        private static bool _searched;
        private static System.Diagnostics.Process _currentProcess;
        private static readonly object _procLock = new();
        private static volatile bool _cancelRequested;

        public static void Kill()
        {
            lock (_procLock)
            {
                if (_currentProcess == null)
                    return;

                _cancelRequested = true;

                try
                {
                    if (!_currentProcess.HasExited)
                    {
                        _currentProcess.Kill();
                        _currentProcess.WaitForExit(5000);
                    }
                }
                catch (InvalidOperationException) { }
                finally { _currentProcess = null; }
            }
        }

        public static string ButlerPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_butlerPath) && File.Exists(_butlerPath))
                    return _butlerPath;

                if (_searched) return _butlerPath;
                _searched = true;

                var platform = GetPlatform();
                var exeName = platform == "windows" ? "butler.exe" : "butler";

                _butlerPath = FindLocalButler(platform, exeName);
                if (!string.IsNullOrEmpty(_butlerPath)) return _butlerPath;

                _butlerPath = FindGlobalButler(platform, exeName);
                if (!string.IsNullOrEmpty(_butlerPath)) return _butlerPath;

                return null;
            }
        }

        public static bool IsButlerAvailable => !string.IsNullOrEmpty(ButlerPath) && File.Exists(ButlerPath);

        public static void RefreshPath()
        {
            _butlerPath = null;
            _searched = false;
            _ = ButlerPath;
        }

        public static bool TryAutoInstall()
        {
            if (IsButlerAvailable) return true;

            ButlerInstaller.TryAutoInstall();
            RefreshPath();
            return IsButlerAvailable;
        }

        private static string GetPlatform()
        {
#if UNITY_EDITOR_WIN
            return "windows";
#elif UNITY_EDITOR_OSX
            return "darwin";
#else
            return "linux";
#endif
        }

        private static string FindLocalButler(string platform, string exeName)
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ItchDeploy", "butler", platform, exeName);
            if (File.Exists(appData)) return appData;

            var local = Path.Combine(Application.dataPath, "ItchDeploy", "butler", platform, exeName);
            if (File.Exists(local)) return local;

            var localFlat = Path.Combine(Application.dataPath, "ItchDeploy", "butler", exeName);
            if (File.Exists(localFlat)) return localFlat;

            return null;
        }

        private static string FindGlobalButler(string platform, string exeName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = platform == "windows" ? "where.exe" : "which",
                    Arguments = exeName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd().Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    proc.WaitForExit(3000);
                    if (proc.ExitCode == 0 && output.Length > 0 && File.Exists(output[0]))
                        return output[0];
                }
            }
            catch { }

            return null;
        }

        public sealed class ButlerResult
        {
            public int ExitCode = -1;
            public string Output = string.Empty;
            public string Error = string.Empty;
            public bool TimedOut;
            public bool Cancelled;
            public bool StartFailed;
            public string StartError = string.Empty;

            public bool Succeeded =>
                !StartFailed && !TimedOut && !Cancelled && ExitCode == 0;

            public override string ToString()
            {
                if (StartFailed)
                    return "ERROR: " + StartError;
                if (Cancelled)
                    return "ERROR: butler was cancelled";
                if (TimedOut)
                    return $"ERROR: butler timed out\n{Output}\n{Error}";
                if (ExitCode != 0)
                    return $"{Output}\nEXIT_CODE: {ExitCode}\nSTDERR: {Error}";
                return Output;
            }
        }

        public static string RunButler(string arguments, int timeoutMs = 120000, string apiKey = null)
            => Run(arguments, timeoutMs, apiKey).ToString();

        public static ButlerResult Run(
            string arguments,
            int timeoutMs = 120000,
            string apiKey = null,
            Action<string> onOutputLine = null)
        {
            var result = new ButlerResult();
            if (!IsButlerAvailable)
            {
                result.StartFailed = true;
                result.StartError = "butler not found";
                return result;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ButlerPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                if (!string.IsNullOrWhiteSpace(apiKey))
                    psi.EnvironmentVariables["BUTLER_API_KEY"] = apiKey;

                var proc = Process.Start(psi);
                if (proc == null)
                {
                    result.StartFailed = true;
                    result.StartError = "failed to start butler process";
                    return result;
                }

                using (proc)
                {
                    lock (_procLock) { _currentProcess = proc; }

                    try
                    {
                        var output = new StringBuilder();
                        var error = new StringBuilder();
                        proc.OutputDataReceived += (_, args) =>
                        {
                            if (args.Data == null) return;
                            lock (output) output.AppendLine(args.Data);
                            if (!string.IsNullOrWhiteSpace(args.Data))
                                onOutputLine?.Invoke(args.Data.Trim());
                        };
                        proc.ErrorDataReceived += (_, args) =>
                        {
                            if (args.Data == null) return;
                            lock (error) error.AppendLine(args.Data);
                        };
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();

                        var exited = proc.WaitForExit(timeoutMs);
                        if (!exited)
                        {
                            try { proc.Kill(); }
                            catch (InvalidOperationException) { }
                            proc.WaitForExit(5000);
                            result.TimedOut = true;
                        }
                        else
                        {
                            proc.WaitForExit();
                            result.ExitCode = proc.ExitCode;
                        }

                        lock (output) result.Output = output.ToString().TrimEnd();
                        lock (error) result.Error = error.ToString().TrimEnd();
                        if (_cancelRequested)
                        {
                            result.Cancelled = true;
                            _cancelRequested = false;
                        }

                        return result;
                    }
                    finally
                    {
                        lock (_procLock)
                        {
                            if (ReferenceEquals(_currentProcess, proc))
                                _currentProcess = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.StartFailed = true;
                result.StartError = ex.Message;
                return result;
            }
        }

        public static ButlerResult RunWithEditorProgress(
            string arguments,
            string progressTitle,
            int timeoutMs = 120000,
            string apiKey = null)
        {
            if (Application.isBatchMode)
                return Run(arguments, timeoutMs, apiKey);

            var status = "Starting butler...";
            var task = Task.Run(() => Run(
                arguments,
                timeoutMs,
                apiKey,
                line => Volatile.Write(ref status, line)));

            try
            {
                var startedAt = DateTime.UtcNow;
                while (!task.IsCompleted)
                {
                    var elapsed = DateTime.UtcNow - startedAt;

                    var pulse = (float)((elapsed.TotalSeconds % 10d) / 10d);
                    if (EditorUtility.DisplayCancelableProgressBar(
                            progressTitle,
                            $"{Volatile.Read(ref status)}  ({elapsed:mm\\:ss})",
                            pulse))
                    {
                        Kill();
                        break;
                    }

                    Thread.Sleep(100);
                }

                task.Wait();
                return task.Result;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        internal static string BuildPushArguments(
            string sourceDir,
            string target,
            string version,
            bool ifChanged)
        {
            var ignoreArgs = ItchIgnore.GetButlerIgnoreArguments();
            var args = "push";
            if (!string.IsNullOrEmpty(ignoreArgs))
                args += " " + ignoreArgs;
            args += $" {QuoteArgument(sourceDir)} {QuoteArgument(target)} " +
                    $"--userversion {QuoteArgument(version)}";

            args += " --fix-permissions";
            if (ifChanged) args += " --if-changed";
            return args;
        }

        public static string Push(
            string sourceDir,
            string target,
            string version,
            bool ifChanged = false,
            string apiKey = null)
            => PushWithProgress(sourceDir, target, version, ifChanged, apiKey).ToString();

        public static ButlerResult PushWithProgress(
            string sourceDir,
            string target,
            string version,
            bool ifChanged = false,
            string apiKey = null)
        {
            return RunWithEditorProgress(
                BuildPushArguments(sourceDir, target, version, ifChanged),
                $"Publishing to {target}",
                30 * 60 * 1000,
                apiKey);
        }

        public static string Status(string target, string apiKey = null)
        {
            return RunButler($"status {QuoteArgument(target)}", apiKey: apiKey);
        }

        public static bool IsSuccessfulResult(string result)
        {
            return !string.IsNullOrWhiteSpace(result) &&
                   !result.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase) &&
                   result.IndexOf("EXIT_CODE:", StringComparison.OrdinalIgnoreCase) < 0;
        }

        public static string Version()
        {
            return RunButler("version");
        }

        internal static string QuoteArgument(string value)
        {
            value ??= string.Empty;
            var quoted = new StringBuilder(value.Length + 2);
            quoted.Append('"');
            var backslashes = 0;

            foreach (var character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    quoted.Append('\\', backslashes * 2 + 1);
                    quoted.Append('"');
                }
                else
                {
                    quoted.Append('\\', backslashes);
                    quoted.Append(character);
                }

                backslashes = 0;
            }

            quoted.Append('\\', backslashes * 2);
            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
