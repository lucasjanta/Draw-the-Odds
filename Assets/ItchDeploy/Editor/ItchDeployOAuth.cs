using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;

namespace ItchDeploy.Editor
{
    public static class ItchDeployOAuth
    {
        private const int LoginTimeoutMs = 5 * 60 * 1000;
        private static readonly object Gate = new();

        private static Action<bool, string> _callback;
        private static Task<string> _loginTask;
        private static bool _active;
        private static string _pendingCredentialPath;

        public static bool IsActive
        {
            get
            {
                lock (Gate)
                    return _active;
            }
        }

        public static void BeginLogin(Action<bool, string> callback)
        {
            var alreadyActive = false;
            lock (Gate)
            {
                if (_active)
                {
                    alreadyActive = true;
                }
                else
                {
                    _active = true;
                    _callback = callback;
                }
            }

            if (alreadyActive)
            {
                callback?.Invoke(false, ItchDeployLocale._(
                    "itch.io sign-in is already open in the browser.",
                    "Вхід до itch.io вже відкрито в браузері."));
                return;
            }

            if (ButlerBridge.IsButlerAvailable)
            {
                StartButlerLogin();
                return;
            }

            ButlerInstaller.TryAutoInstall();
            EditorApplication.update -= WaitForButlerInstallation;
            EditorApplication.update += WaitForButlerInstallation;
        }

        public static void BeginLogin(
            string unusedClientId,
            Action<bool, string> callback) => BeginLogin(callback);

        internal static string LoadButlerCredential() =>
            LoadManagedCredential() ?? LoadDefaultButlerCredential();

        internal static string LoadManagedCredential() =>
            LoadCredentialFile(GetManagedCredentialPath());

        internal static string LoadDefaultButlerCredential() =>
            LoadCredentialFile(GetDefaultButlerCredentialPath());

        private static string LoadCredentialFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                var info = new FileInfo(path);
                if (info.Length < 20 || info.Length > 512)
                    return null;

                var token = File.ReadAllText(path).Trim();
                if (token.Length < 20 || token.Length > 512 ||
                    token.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                    return null;

                return token;
            }
            catch
            {
                return null;
            }
        }

        internal static string GetManagedCredentialPath()
        {
#if UNITY_EDITOR_OSX
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library",
                "Application Support",
                "ItchDeploy",
                "butler_creds");
#elif UNITY_EDITOR_WIN
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ItchDeploy",
                "butler_creds");
#else
            var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var configRoot = string.IsNullOrWhiteSpace(xdgConfig)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                    ".config")
                : xdgConfig;
            return Path.Combine(configRoot, "ItchDeploy", "butler_creds");
#endif
        }

        internal static string GetDefaultButlerCredentialPath()
        {
#if UNITY_EDITOR_OSX
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library",
                "Application Support",
                "itch",
                "butler_creds");
#elif UNITY_EDITOR_WIN
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "itch",
                "butler_creds");
#else
            var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var configRoot = string.IsNullOrWhiteSpace(xdgConfig)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                    ".config")
                : xdgConfig;
            return Path.Combine(configRoot, "itch", "butler_creds");
#endif
        }

        internal static string GetButlerCredentialPath() =>
            GetDefaultButlerCredentialPath();

        private static void WaitForButlerInstallation()
        {
            if (ButlerBridge.IsButlerAvailable ||
                ButlerInstaller.CurrentStatus == ButlerInstaller.Status.Installed)
            {
                EditorApplication.update -= WaitForButlerInstallation;
                ButlerBridge.RefreshPath();
                StartButlerLogin();
                return;
            }

            if (ButlerInstaller.CurrentStatus == ButlerInstaller.Status.Error)
            {
                EditorApplication.update -= WaitForButlerInstallation;
                Complete(false, ItchDeployLocale._(
                    "Butler could not be installed. Check the Unity Console and try again.",
                    "Не вдалося встановити Butler. Перевірте Unity Console і повторіть спробу."));
            }
        }

        private static void StartButlerLogin()
        {
            if (!ButlerBridge.IsButlerAvailable)
            {
                Complete(false, ItchDeployLocale._(
                    "Butler is unavailable.",
                    "Butler недоступний."));
                return;
            }

            try
            {
                var managedPath = GetManagedCredentialPath();
                var managedDirectory = Path.GetDirectoryName(managedPath);
                if (string.IsNullOrWhiteSpace(managedDirectory))
                    throw new InvalidOperationException("Credential directory is unavailable.");

                Directory.CreateDirectory(managedDirectory);
                _pendingCredentialPath = Path.Combine(
                    managedDirectory,
                    $"login-{Guid.NewGuid():N}.tmp");
            }
            catch
            {
                Complete(false, ItchDeployLocale._(
                    "The local itch.io authorization folder could not be prepared.",
                    "Не вдалося підготувати локальну папку авторизації itch.io."));
                return;
            }

            var loginArguments = BuildLoginArguments(_pendingCredentialPath);
            _loginTask = Task.Run(() => ButlerBridge.RunButler(
                loginArguments,
                LoginTimeoutMs));
            EditorApplication.update -= PollButlerLogin;
            EditorApplication.update += PollButlerLogin;
        }

        private static void PollButlerLogin()
        {
            if (_loginTask == null || !_loginTask.IsCompleted)
                return;

            EditorApplication.update -= PollButlerLogin;
            var result = _loginTask.Status == TaskStatus.RanToCompletion
                ? _loginTask.Result
                : "ERROR: Butler login failed.";
            _loginTask = null;

            var processSucceeded = !result.StartsWith(
                                       "ERROR:",
                                       StringComparison.OrdinalIgnoreCase) &&
                                   result.IndexOf(
                                       "EXIT_CODE:",
                                       StringComparison.OrdinalIgnoreCase) < 0;
            var pendingCredential = LoadCredentialFile(_pendingCredentialPath);
            var credentialReady = !string.IsNullOrWhiteSpace(pendingCredential);

            if (processSucceeded && credentialReady)
                PromotePendingCredential();
            else
                DeletePendingCredential();

            Complete(
                processSucceeded && credentialReady,
                processSucceeded && credentialReady
                    ? ItchDeployLocale._(
                        "itch.io account connected through Butler.",
                        "Акаунт itch.io підключено через Butler.")
                    : ItchDeployLocale._(
                        "itch.io sign-in was not completed. Finish it in the browser and try again.",
                        "Вхід до itch.io не завершено. Завершіть його у браузері та повторіть спробу."));
        }

        internal static string BuildLoginArguments(string identityPath) =>
            $"--assume-yes --identity {ButlerBridge.QuoteArgument(identityPath)} login";

        private static void PromotePendingCredential()
        {
            var pendingPath = _pendingCredentialPath;
            _pendingCredentialPath = null;
            if (string.IsNullOrWhiteSpace(pendingPath) || !File.Exists(pendingPath))
                return;

            try
            {
                File.Copy(pendingPath, GetManagedCredentialPath(), overwrite: true);
                ItchDeployCredentials.Invalidate();
            }
            finally
            {
                try { File.Delete(pendingPath); }
                catch { }
            }
        }

        private static void DeletePendingCredential()
        {
            var pendingPath = _pendingCredentialPath;
            _pendingCredentialPath = null;
            if (string.IsNullOrWhiteSpace(pendingPath))
                return;

            try { File.Delete(pendingPath); }
            catch { }
        }

        private static void Complete(bool success, string message)
        {
            Action<bool, string> callback;
            lock (Gate)
            {
                if (!_active)
                    return;

                _active = false;
                callback = _callback;
                _callback = null;
            }

            callback?.Invoke(success, message);
        }
    }
}
