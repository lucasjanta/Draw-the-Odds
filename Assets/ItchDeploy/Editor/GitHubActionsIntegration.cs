using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ItchDeploy.Editor
{
    public static class GitHubActionsIntegration
    {
        private static readonly object CliLoginProcessLock = new();
        private static Process _cliLoginProcess;
        private static string _cliLoginDeviceCode = string.Empty;

        public const string GitHubDeviceLoginUrl =
            "https://github.com/login/device";

        public enum SecretCheckFailure
        {
            None,
            RepositoryNotConfigured,
            CliNotInstalled,
            CliNotAuthenticated,
            QueryFailed,
        }

        public sealed class SecretCheckResult
        {
            public bool verified;
            public IReadOnlyList<string> missingSecrets = Array.Empty<string>();
            public SecretCheckFailure failure;
        }

        public static readonly string[] UnityBuildSecrets =
        {
            "UNITY_EMAIL",
            "UNITY_PASSWORD",
        };

        public static readonly string[] UnityLicenseAlternatives =
        {
            "UNITY_LICENSE",
            "UNITY_SERIAL",
        };

        public static readonly string[] AndroidSigningSecrets =
        {
            "ANDROID_KEYSTORE_BASE64",
            "ANDROID_KEYSTORE_PASS",
            "ANDROID_KEYALIAS_NAME",
            "ANDROID_KEYALIAS_PASS",
        };

        public static bool TryGetRepository(out string slug, out string webUrl)
        {
            slug = string.Empty;
            webUrl = string.Empty;
            if (!TryRun("git", "remote get-url origin", out var remote, out _))
                return false;

            return TryParseRepositoryUrl(remote.Trim(), out slug, out webUrl);
        }

        internal static bool TryParseRepositoryUrl(
            string remote,
            out string slug,
            out string webUrl)
        {
            slug = string.Empty;
            webUrl = string.Empty;
            if (string.IsNullOrWhiteSpace(remote))
                return false;

            var normalized = StripUserInfo(remote.Trim().Replace('\\', '/'));
            if (normalized.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("git@github.com:".Length);
            else if (normalized.StartsWith("ssh://git@github.com/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("ssh://git@github.com/".Length);
            else if (normalized.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("https://github.com/".Length);
            else if (normalized.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("http://github.com/".Length);
            else
                return false;

            normalized = normalized.TrimEnd('/');
            if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - 4);

            var parts = normalized.Split('/');
            if (parts.Length != 2 ||
                !IsSafeRepositoryPart(parts[0]) ||
                !IsSafeRepositoryPart(parts[1]))
                return false;

            slug = parts[0] + "/" + parts[1];
            webUrl = "https://github.com/" + slug;
            return true;
        }

        private static string StripUserInfo(string remote)
        {
            var schemeEnd = remote.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd < 0)
                return remote;

            var hostStart = schemeEnd + 3;
            var slash = remote.IndexOf('/', hostStart);
            var authorityEnd = slash < 0 ? remote.Length : slash;
            var at = remote.LastIndexOf('@', authorityEnd - 1, authorityEnd - hostStart);
            if (at < 0)
                return remote;

            var userInfo = remote.Substring(hostStart, at - hostStart);
            if (string.Equals(userInfo, "git", StringComparison.OrdinalIgnoreCase))
                return remote;

            return remote.Substring(0, hostStart) + remote.Substring(at + 1);
        }

        public static bool IsCliAuthenticated(out string reason)
        {
            if (!TryGetCliExecutable(out var cliExecutable))
            {
                reason = "GitHub CLI is not installed.";
                return false;
            }

            if (!TryRun(
                    cliExecutable,
                    "auth status --hostname github.com",
                    5000,
                    out _,
                    out _))
            {
                reason = "GitHub CLI is not signed in or the session has expired.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static bool TryStartCliLogin(out string error)
        {
            error = string.Empty;
            if (!TryGetCliExecutable(out var cliExecutable))
            {
                error = "GitHub CLI is not installed.";
                return false;
            }

            lock (CliLoginProcessLock)
            {
                if (IsProcessRunning(_cliLoginProcess))
                    return true;
            }

            try
            {
                var startInfo = new ProcessStartInfo(cliExecutable)
                {
                    Arguments =
                        "auth login --hostname github.com " +
                        "--git-protocol https --web --clipboard",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                var process = new Process
                {
                    StartInfo = startInfo,
                };
                startInfo.EnvironmentVariables["GH_PROMPT_DISABLED"] = "1";
                startInfo.EnvironmentVariables["GH_SPINNER_DISABLED"] = "1";
                process.OutputDataReceived += (_, eventArgs) =>
                    CaptureCliLoginDeviceCode(eventArgs.Data);
                process.ErrorDataReceived += (_, eventArgs) =>
                    CaptureCliLoginDeviceCode(eventArgs.Data);
                process.Exited += (_, _) =>
                {
                    try { process.WaitForExit(); }
                    catch { }
                    lock (CliLoginProcessLock)
                    {
                        if (ReferenceEquals(_cliLoginProcess, process))
                        {
                            _cliLoginProcess = null;
                            _cliLoginDeviceCode = string.Empty;
                        }
                    }
                    process.Dispose();
                };
                if (!process.Start())
                {
                    process.Dispose();
                    error = "Could not start GitHub CLI sign-in.";
                    return false;
                }

                lock (CliLoginProcessLock)
                {
                    _cliLoginProcess = process;
                    _cliLoginDeviceCode = string.Empty;
                }
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.EnableRaisingEvents = true;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool IsCliLoginRunning
        {
            get
            {
                lock (CliLoginProcessLock)
                    return IsProcessRunning(_cliLoginProcess);
            }
        }

        public static string CliLoginDeviceCode
        {
            get
            {
                lock (CliLoginProcessLock)
                    return _cliLoginDeviceCode;
            }
        }

        private static void CaptureCliLoginDeviceCode(string outputLine)
        {
            if (string.IsNullOrWhiteSpace(outputLine))
                return;

            var match = Regex.Match(
                outputLine,
                @"\b[A-Z0-9]{4}-[A-Z0-9]{4}\b",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                return;

            lock (CliLoginProcessLock)
                _cliLoginDeviceCode = match.Value;
        }

        private static bool IsProcessRunning(Process process)
        {
            if (process == null)
                return false;
            try { return !process.HasExited; }
            catch { return false; }
        }

        public static IReadOnlyList<string> GetMissingSecrets(
            string repositorySlug,
            bool deployToItch,
            bool requireAndroidSigning,
            out bool verified)
        {
            var result = CheckSecrets(
                repositorySlug,
                deployToItch,
                requireAndroidSigning);
            verified = result.verified;
            return result.missingSecrets;
        }

        public static IReadOnlyList<string> GetMissingSecrets(
            string repositorySlug,
            bool deployToItch,
            out bool verified)
        {
            return GetMissingSecrets(
                repositorySlug,
                deployToItch,
                requireAndroidSigning: false,
                out verified);
        }

        public static SecretCheckResult CheckSecrets(
            string repositorySlug,
            bool deployToItch,
            bool requireAndroidSigning = false)
        {
            if (!IsSafeRepositorySlug(repositorySlug))
            {
                return new SecretCheckResult
                {
                    failure = SecretCheckFailure.RepositoryNotConfigured,
                };
            }

            if (!TryGetCliExecutable(out var cliExecutable))
            {
                return new SecretCheckResult
                {
                    failure = SecretCheckFailure.CliNotInstalled,
                };
            }

            if (!TryRun(
                    cliExecutable,
                    "auth status --hostname github.com",
                    5000,
                    out _,
                    out _))
            {
                return new SecretCheckResult
                {
                    failure = SecretCheckFailure.CliNotAuthenticated,
                };
            }

            if (!TryRun(
                    cliExecutable,
                    "secret list --repo " + repositorySlug,
                    15000,
                    out var output,
                    out _))
            {
                return new SecretCheckResult
                {
                    failure = SecretCheckFailure.QueryFailed,
                };
            }

            var existing = new HashSet<string>(
                ParseSecretNames(output),
                StringComparer.OrdinalIgnoreCase);
            return new SecretCheckResult
            {
                verified = true,
                failure = SecretCheckFailure.None,
                missingSecrets = GetMissingRequiredSecretNames(
                    existing,
                    deployToItch,
                    requireAndroidSigning),
            };
        }

        internal static IReadOnlyList<string> ParseSecretNames(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return Array.Empty<string>();

            var names = new List<string>();
            foreach (var line in output.Split(
                         new[] { '\r', '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var columns = line.Split(
                    (char[])null,
                    StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length > 0 && !string.IsNullOrWhiteSpace(columns[0]))
                    names.Add(columns[0].Trim());
            }

            return names;
        }

        internal static IReadOnlyList<string> GetRequiredSecretNames(
            bool deployToItch,
            bool requireAndroidSigning = false)
        {
            var required = new List<string>(UnityBuildSecrets);
            required.Insert(0, "UNITY_LICENSE / UNITY_SERIAL");
            if (deployToItch)
                required.Add("BUTLER_API_KEY");
            if (requireAndroidSigning)
                required.AddRange(AndroidSigningSecrets);
            return required;
        }

        internal static IReadOnlyList<string> GetMissingRequiredSecretNames(
            IEnumerable<string> existingSecretNames,
            bool deployToItch,
            bool requireAndroidSigning = false)
        {
            var existing = new HashSet<string>(
                existingSecretNames ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            var missing = UnityBuildSecrets
                .Where(secret => !existing.Contains(secret))
                .ToList();

            if (!UnityLicenseAlternatives.Any(existing.Contains))
                missing.Insert(0, "UNITY_LICENSE / UNITY_SERIAL");
            if (deployToItch && !existing.Contains("BUTLER_API_KEY"))
                missing.Add("BUTLER_API_KEY");
            if (requireAndroidSigning)
            {
                missing.AddRange(AndroidSigningSecrets.Where(secret =>
                    !existing.Contains(secret)));
            }
            return missing;
        }

        public static bool TrySetButlerSecret(
            string repositorySlug,
            string apiKey,
            out string error)
        {
            error = string.Empty;
            if (!IsSafeRepositorySlug(repositorySlug))
            {
                error = "GitHub repository is not configured.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                error = "The local itch.io authorization has no Butler API key.";
                return false;
            }
            if (!IsCliAuthenticated(out error))
                return false;

            if (!TryGetCliExecutable(out var cliExecutable))
            {
                error = "GitHub CLI is not installed.";
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo(cliExecutable)
                {
                    Arguments = "secret set BUTLER_API_KEY --repo " + repositorySlug,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    error = "Could not start GitHub CLI.";
                    return false;
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                process.StandardInput.Write(apiKey);
                process.StandardInput.Close();
                if (!process.WaitForExit(15000))
                {
                    TryKill(process);
                    error = "GitHub CLI timed out while saving BUTLER_API_KEY.";
                    return false;
                }

                outputTask.GetAwaiter().GetResult();
                var standardError = errorTask.GetAwaiter().GetResult();
                if (process.ExitCode == 0)
                    return true;

                error = string.IsNullOrWhiteSpace(standardError)
                    ? "GitHub CLI could not save BUTLER_API_KEY."
                    : standardError.Trim();
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryRun(
            string executable,
            string arguments,
            out string output,
            out string error)
        {
            return TryRun(executable, arguments, 2000, out output, out error);
        }

        private static bool TryRun(
            string executable,
            string arguments,
            int timeoutMilliseconds,
            out string output,
            out string error)
        {
            output = string.Empty;
            error = string.Empty;
            try
            {
                if (TryResolveExecutablePath(executable, out var resolvedExecutable))
                    executable = resolvedExecutable;

                var startInfo = new ProcessStartInfo(executable)
                {
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(startInfo);
                if (process == null)
                    return false;
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    TryKill(process);
                    return false;
                }

                output = outputTask.GetAwaiter().GetResult();
                error = errorTask.GetAwaiter().GetResult();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static bool TryResolveExecutablePath(
            string executable,
            out string resolvedPath)
        {
            return TrySelectExistingExecutable(
                GetExecutableCandidates(executable),
                File.Exists,
                out resolvedPath);
        }

        internal static bool TrySelectExistingExecutable(
            IEnumerable<string> candidates,
            Func<string, bool> fileExists,
            out string resolvedPath)
        {
            resolvedPath = string.Empty;
            if (candidates == null || fileExists == null)
                return false;

            var comparer = IsWindows
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var visited = new HashSet<string>(comparer);
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(candidate.Trim().Trim('"'));
                }
                catch
                {
                    continue;
                }

                if (!visited.Add(fullPath) || !fileExists(fullPath))
                    continue;

                resolvedPath = fullPath;
                return true;
            }

            return false;
        }

        private static bool TryGetCliExecutable(out string cliExecutable)
        {
            return TryResolveExecutablePath("gh", out cliExecutable) &&
                   TryRun(cliExecutable, "--version", 3000, out _, out _);
        }

        private static IEnumerable<string> GetExecutableCandidates(string executable)
        {
            if (string.IsNullOrWhiteSpace(executable))
                yield break;

            var trimmed = executable.Trim().Trim('"');
            var executableName = Path.GetFileName(trimmed);
            if (IsWindows && string.IsNullOrEmpty(Path.GetExtension(executableName)))
                executableName += ".exe";

            if (Path.IsPathRooted(trimmed) ||
                trimmed.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                trimmed.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                yield return trimmed;
                yield break;
            }

            foreach (var pathVariable in GetCurrentPathVariables())
            {
                foreach (var directory in pathVariable.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrWhiteSpace(directory))
                        yield return Path.Combine(directory.Trim().Trim('"'), executableName);
                }
            }

            if (!string.Equals(
                    Path.GetFileNameWithoutExtension(executableName),
                    "gh",
                    StringComparison.OrdinalIgnoreCase))
                yield break;

            if (IsWindows)
            {
                var programFiles = Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86);
                var localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                var userProfile = Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);
                var chocolateyInstall = Environment.GetEnvironmentVariable(
                    "ChocolateyInstall");

                yield return Path.Combine(programFiles, "GitHub CLI", "gh.exe");
                yield return Path.Combine(programFilesX86, "GitHub CLI", "gh.exe");
                yield return Path.Combine(
                    localAppData,
                    "Programs",
                    "GitHub CLI",
                    "gh.exe");
                yield return Path.Combine(
                    localAppData,
                    "Microsoft",
                    "WinGet",
                    "Links",
                    "gh.exe");
                yield return Path.Combine(userProfile, "scoop", "shims", "gh.exe");
                if (!string.IsNullOrWhiteSpace(chocolateyInstall))
                    yield return Path.Combine(chocolateyInstall, "bin", "gh.exe");
            }
            else
            {
                var userProfile = Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);
                yield return "/opt/homebrew/bin/gh";
                yield return "/usr/local/bin/gh";
                yield return "/usr/bin/gh";
                yield return Path.Combine(userProfile, ".local", "bin", "gh");
                yield return Path.Combine(userProfile, "homebrew", "bin", "gh");
            }
        }

        private static IEnumerable<string> GetCurrentPathVariables()
        {
            var processPath = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(processPath))
                yield return processPath;

            if (!IsWindows)
                yield break;

            string userPath = null;
            string machinePath = null;
            try
            {
                userPath = Environment.GetEnvironmentVariable(
                    "PATH",
                    EnvironmentVariableTarget.User);
                machinePath = Environment.GetEnvironmentVariable(
                    "PATH",
                    EnvironmentVariableTarget.Machine);
            }
            catch
            { }

            if (!string.IsNullOrWhiteSpace(userPath))
                yield return userPath;
            if (!string.IsNullOrWhiteSpace(machinePath))
                yield return machinePath;
        }

        private static bool IsWindows =>
            Environment.OSVersion.Platform == PlatformID.Win32NT;

        private static bool IsSafeRepositorySlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            var parts = value.Split('/');
            return parts.Length == 2 &&
                   IsSafeRepositoryPart(parts[0]) &&
                   IsSafeRepositoryPart(parts[1]);
        }

        private static bool IsSafeRepositoryPart(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.');
        }

        private static void TryKill(Process process)
        {
            try { process.Kill(); }
            catch { }
        }
    }
}
