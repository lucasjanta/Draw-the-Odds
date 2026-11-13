using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    [InitializeOnLoad]
    public static class WebhookReceiver
    {
        private const int MaxPayloadBytes = 1024 * 1024;
        private const string EnabledKey = "ItchDeploy_Webhook_Enabled";
        private const string PortKey = "ItchDeploy_Webhook_Port";
        private const string RemoteKey = "ItchDeploy_Webhook_AllowRemote";
        private const string LegacySecretKey = "ItchDeploy_Webhook_Secret";
        private const string LastEventKey = "ItchDeploy_Webhook_LastEvent";

        private static HttpListener _listener;
        private static Thread _listenerThread;
        private static SynchronizationContext _unityContext;
        private static string _activeSecret;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set
            {
                if (value && !HasValidSecret())
                {
                    EditorPrefs.SetBool(EnabledKey, false);
                    Debug.LogError("[ItchDeploy] Webhook receiver requires a secret of at least 16 characters.");
                    return;
                }

                EditorPrefs.SetBool(EnabledKey, value);
                if (value) Start();
                else Stop();
            }
        }

        public static int Port
        {
            get => EditorPrefs.GetInt(PortKey, 9876);
            set
            {
                var clamped = Mathf.Clamp(value, 1024, 65535);
                if (clamped == Port) return;
                EditorPrefs.SetInt(PortKey, clamped);
                if (Enabled) Start();
            }
        }

        public static bool AllowRemote
        {
            get => EditorPrefs.GetBool(RemoteKey, false);
            set
            {
                if (value == AllowRemote) return;
                EditorPrefs.SetBool(RemoteKey, value);
                if (Enabled) Start();
            }
        }

        public static string Secret
        {
            get
            {
                var secret = ItchDeploySecureStorage.LoadWebhookReceiverSecret();
                if (!string.IsNullOrEmpty(secret) || !EditorPrefs.HasKey(LegacySecretKey))
                    return secret ?? string.Empty;

                secret = EditorPrefs.GetString(LegacySecretKey, string.Empty);
                EditorPrefs.DeleteKey(LegacySecretKey);
                ItchDeploySecureStorage.SaveWebhookReceiverSecret(secret);
                return secret;
            }
            set
            {
                EditorPrefs.DeleteKey(LegacySecretKey);
                ItchDeploySecureStorage.SaveWebhookReceiverSecret(value);
            }
        }

        public static string LastEvent
        {
            get => EditorPrefs.GetString(LastEventKey, string.Empty);
            private set => EditorPrefs.SetString(LastEventKey, value);
        }

        static WebhookReceiver()
        {
            _unityContext = SynchronizationContext.Current;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            if (Enabled)
                Start();
        }

        public static void Start()
        {
            Stop();
            if (!HasValidSecret())
            {
                EditorPrefs.SetBool(EnabledKey, false);
                Debug.LogError("[ItchDeploy] Webhook receiver was not started: configure a strong secret first.");
                return;
            }

            try
            {
                _activeSecret = Secret;
                var host = AllowRemote ? "+" : "127.0.0.1";
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://{host}:{Port}/");
                _listener.Start();

                _listenerThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "ItchDeploy Webhook Receiver"
                };
                _listenerThread.Start();
                Debug.Log($"[ItchDeploy] Authenticated webhook receiver listening on {host}:{Port}.");
            }
            catch (Exception ex)
            {
                EditorPrefs.SetBool(EnabledKey, false);
                Stop();
                Debug.LogError($"[ItchDeploy] Could not start webhook receiver: {ex.Message}");
            }
        }

        public static void Stop()
        {
            var listener = _listener;
            var thread = _listenerThread;
            _listener = null;
            _listenerThread = null;
            _activeSecret = null;

            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch
            { }

            if (thread != null && thread != Thread.CurrentThread && thread.IsAlive)
                thread.Join(500);
        }

        private static void ListenLoop()
        {
            var listener = _listener;
            if (listener == null) return;

            while (listener.IsListening)
            {
                try
                {
                    HandleRequest(listener.GetContext());
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ItchDeploy] Webhook request failed: {ex.Message}");
                }
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(request.Url?.AbsolutePath, "/webhook", StringComparison.Ordinal))
            {
                WriteResponse(context.Response, 404, "{\"error\":\"not_found\"}");
                return;
            }

            if (request.ContentLength64 > MaxPayloadBytes)
            {
                WriteResponse(context.Response, 413, "{\"error\":\"payload_too_large\"}");
                return;
            }

            byte[] bodyBytes;
            try
            {
                bodyBytes = ReadPayload(request.InputStream);
            }
            catch (InvalidDataException)
            {
                WriteResponse(context.Response, 413, "{\"error\":\"payload_too_large\"}");
                return;
            }

            if (!IsAuthorized(request, bodyBytes))
            {
                WriteResponse(context.Response, 401, "{\"error\":\"invalid_signature\"}");
                return;
            }

            var eventType = request.Headers["X-GitHub-Event"] ??
                            request.Headers["X-Gitlab-Event"] ?? string.Empty;
            var body = Encoding.UTF8.GetString(bodyBytes);
            var branch = ExtractBranch(body);
            var isPush = string.Equals(eventType, "push", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(eventType, "Push Hook", StringComparison.OrdinalIgnoreCase);
            var isReleaseBranch = string.Equals(branch, "main", StringComparison.Ordinal) ||
                                  string.Equals(branch, "master", StringComparison.Ordinal);

            RunOnMainThread(() =>
            {
                LastEvent = $"{eventType} at {DateTime.Now:HH:mm} ({branch})";
                if (!isPush || !isReleaseBranch)
                    return;

                if (BuildDeployer.IsBuilding || BuildDeployer.IsDeploying)
                {
                    Debug.LogWarning("[ItchDeploy] Authenticated webhook ignored because a pipeline is already running.");
                    return;
                }

                var settings = ItchDeploySettings.Load();
                var target = EditorUserBuildSettings.activeBuildTarget;
                var channel = PlatformChannelMap.ResolveChannel(settings, target);

                Debug.Log($"[ItchDeploy] Authenticated webhook triggered {target} -> {channel}.");
                BuildDeployer.BuildAndDeploy(
                    target,
                    channel,
                    null,
                    settings,
                    deployAfterBuild: true);
            });

            WriteResponse(context.Response, 202, "{\"status\":\"accepted\"}");
        }

        private static bool IsAuthorized(HttpListenerRequest request, byte[] body)
        {
            var secret = _activeSecret;
            if (string.IsNullOrEmpty(secret))
                return false;

            var githubSignature = request.Headers["X-Hub-Signature-256"];
            if (!string.IsNullOrEmpty(githubSignature))
                return ValidateGitHubSignature(secret, body, githubSignature);

            var gitlabToken = request.Headers["X-Gitlab-Token"];
            return ValidateGitLabToken(secret, gitlabToken);
        }

        internal static bool ValidateGitHubSignature(string secret, byte[] body, string signature)
        {
            if (string.IsNullOrEmpty(secret) || body == null || string.IsNullOrEmpty(signature))
                return false;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(body);
            var expected = "sha256=" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            return FixedTimeEquals(signature, expected);
        }

        internal static bool ValidateGitLabToken(string secret, string token) =>
            !string.IsNullOrEmpty(secret) &&
            !string.IsNullOrEmpty(token) &&
            FixedTimeEquals(token, secret);

        private static byte[] ReadPayload(Stream stream)
        {
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaxPayloadBytes)
                    throw new InvalidDataException("Webhook payload exceeds the configured limit.");
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
            var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
            var difference = leftBytes.Length ^ rightBytes.Length;
            var length = Math.Max(leftBytes.Length, rightBytes.Length);
            for (var i = 0; i < length; i++)
            {
                var a = i < leftBytes.Length ? leftBytes[i] : (byte)0;
                var b = i < rightBytes.Length ? rightBytes[i] : (byte)0;
                difference |= a ^ b;
            }
            return difference == 0;
        }

        private static void WriteResponse(HttpListenerResponse response, int statusCode, string json)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                response.StatusCode = statusCode;
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        private static string ExtractBranch(string body)
        {
            try
            {
                var data = JsonUtility.FromJson<WebhookPayload>(body);
                return data?.@ref?.Replace("refs/heads/", string.Empty) ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static bool HasValidSecret() => Secret.Length >= 16;

        private static void RunOnMainThread(Action action)
        {
            if (_unityContext != null)
                _unityContext.Post(_ => action(), null);
            else
                EditorApplication.delayCall += () => action();
        }

        [Serializable]
        private class WebhookPayload
        {
            public string @ref;
        }
    }
}
