using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace ItchDeploy.Editor
{
    public static class ItchDeploySecureStorage
    {
        private const string KeyPrefix = "ItchDeploy_Secure_";
        private const string ApiKeySlot = "ApiKey";
        private const string PayloadVersion = "v2:";

        public static void SaveApiKey(string apiKey) => SaveSecret(ApiKeySlot, apiKey);
        public static string LoadApiKey() => LoadSecret(ApiKeySlot);
        public static bool HasApiKey => !string.IsNullOrEmpty(LoadApiKey());
        public static void DeleteApiKey() => DeleteSecret(ApiKeySlot);

        internal static void SaveAccountApiKey(string credentialId, string apiKey) =>
            SaveSecret("Account_" + HashId(credentialId), apiKey);

        internal static string LoadAccountApiKey(string credentialId) =>
            LoadSecret("Account_" + HashId(credentialId));

        internal static void DeleteAccountApiKey(string credentialId) =>
            DeleteSecret("Account_" + HashId(credentialId));

        internal static void SaveWebhookReceiverSecret(string secret) =>
            SaveSecret("WebhookReceiver", secret);

        internal static string LoadWebhookReceiverSecret() => LoadSecret("WebhookReceiver");

        internal static void SaveNotificationSecret(string name, string value) =>
            SaveSecret("Notification_" + HashId(name), value);

        internal static string LoadNotificationSecret(string name) =>
            LoadSecret("Notification_" + HashId(name));

        private static void SaveSecret(string slot, string value)
        {
            var prefKey = KeyPrefix + slot;
            if (string.IsNullOrEmpty(value))
            {
                EditorPrefs.DeleteKey(prefKey);
                return;
            }

            EditorPrefs.SetString(prefKey, Encrypt(value));
        }

        private static string LoadSecret(string slot)
        {
            var prefKey = KeyPrefix + slot;
            if (!EditorPrefs.HasKey(prefKey))
            {
                if (slot == ApiKeySlot && EditorPrefs.HasKey("ItchDeploy_ApiKey"))
                {
                    var legacy = EditorPrefs.GetString("ItchDeploy_ApiKey", "");
                    EditorPrefs.DeleteKey("ItchDeploy_ApiKey");
                    if (!string.IsNullOrEmpty(legacy))
                    {
                        try
                        {
                            var value = DecryptLegacy(legacy);
                            SaveSecret(slot, value);
                            return value;
                        }
                        catch
                        {
                            return null;
                        }
                    }
                }

                return null;
            }

            var encrypted = EditorPrefs.GetString(prefKey, "");
            if (string.IsNullOrEmpty(encrypted))
                return null;

            try
            {
                var value = encrypted.StartsWith(PayloadVersion, StringComparison.Ordinal)
                    ? DecryptV2(encrypted.Substring(PayloadVersion.Length))
                    : DecryptLegacy(encrypted);

                if (!encrypted.StartsWith(PayloadVersion, StringComparison.Ordinal))
                    SaveSecret(slot, value);

                return value;
            }
            catch
            {
                EditorPrefs.DeleteKey(prefKey);
                return null;
            }
        }

        private static void DeleteSecret(string slot)
        {
            EditorPrefs.DeleteKey(KeyPrefix + slot);
            if (slot == ApiKeySlot)
                EditorPrefs.DeleteKey("ItchDeploy_ApiKey");
        }

        private static string Encrypt(string plaintext)
        {
            var keys = GetKeyMaterial();
            using var aes = Aes.Create();
            aes.Key = Slice(keys, 0, 32);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            byte[] ciphertext;
            using (var encryptor = aes.CreateEncryptor())
            {
                var bytes = Encoding.UTF8.GetBytes(plaintext);
                ciphertext = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
            }

            var payload = new byte[aes.IV.Length + ciphertext.Length];
            Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
            Buffer.BlockCopy(ciphertext, 0, payload, aes.IV.Length, ciphertext.Length);

            byte[] signature;
            using (var hmac = new HMACSHA256(Slice(keys, 32, 32)))
                signature = hmac.ComputeHash(payload);

            var authenticated = new byte[payload.Length + signature.Length];
            Buffer.BlockCopy(payload, 0, authenticated, 0, payload.Length);
            Buffer.BlockCopy(signature, 0, authenticated, payload.Length, signature.Length);
            return PayloadVersion + Convert.ToBase64String(authenticated);
        }

        private static string DecryptV2(string encoded)
        {
            var authenticated = Convert.FromBase64String(encoded);
            const int ivLength = 16;
            const int signatureLength = 32;
            if (authenticated.Length <= ivLength + signatureLength)
                throw new CryptographicException("Credential payload is incomplete.");

            var payloadLength = authenticated.Length - signatureLength;
            var payload = Slice(authenticated, 0, payloadLength);
            var signature = Slice(authenticated, payloadLength, signatureLength);
            var keys = GetKeyMaterial();

            byte[] expected;
            using (var hmac = new HMACSHA256(Slice(keys, 32, 32)))
                expected = hmac.ComputeHash(payload);

            if (!FixedTimeEquals(signature, expected))
                throw new CryptographicException("Credential payload authentication failed.");

            using var aes = Aes.Create();
            aes.Key = Slice(keys, 0, 32);
            aes.IV = Slice(payload, 0, ivLength);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var ciphertext = Slice(payload, ivLength, payloadLength - ivLength);
            var plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            return Encoding.UTF8.GetString(plaintext);
        }

        private static string DecryptLegacy(string ciphertext)
        {
            var bytes = Convert.FromBase64String(ciphertext);
            var key = GetLegacyKey();
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] ^= key[i % key.Length];
            return Encoding.UTF8.GetString(bytes);
        }

        private static byte[] GetKeyMaterial()
        {
            var machine = Environment.MachineName ?? "default";
            var user = Environment.UserName ?? "user";
            var seed = $"{machine}\n{user}\nItchDeploy/local-credentials/v2";
            using var sha = SHA512.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
        }

        private static byte[] GetLegacyKey()
        {
            var machine = Environment.MachineName ?? "default";
            var user = Environment.UserName ?? "user";
            var seed = $"{machine}-{user}-ItchDeploy!2024";
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
        }

        private static string HashId(string value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return BitConverter.ToString(bytes, 0, 12).Replace("-", string.Empty);
        }

        private static byte[] Slice(byte[] source, int offset, int length)
        {
            var result = new byte[length];
            Buffer.BlockCopy(source, offset, result, 0, length);
            return result;
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            var difference = 0;
            for (var i = 0; i < left.Length; i++)
                difference |= left[i] ^ right[i];
            return difference == 0;
        }
    }
}
