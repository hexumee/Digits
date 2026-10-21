using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage.Streams;
using Digits;

namespace Digits.Security {
    internal static class TokenProtection {
        private const string Prefix = "dpapi:user:v1:";

        public static async Task<string> ProtectSecretAsync(string secret) {
            if (string.IsNullOrWhiteSpace(secret)) {
                throw new ArgumentException(AppText.Get("TokenSecretRequired"), nameof(secret));
            }

            var provider = new DataProtectionProvider("LOCAL=user");
            IBuffer clearBuffer = CryptographicBuffer.ConvertStringToBinary(secret, BinaryStringEncoding.Utf8);
            IBuffer protectedBuffer = await provider.ProtectAsync(clearBuffer);
            return Prefix + CryptographicBuffer.EncodeToBase64String(protectedBuffer);
        }

        public static async Task<string> UnprotectSecretAsync(string protectedSecret) {
            if (string.IsNullOrWhiteSpace(protectedSecret) || !protectedSecret.StartsWith(Prefix, StringComparison.Ordinal)) {
                throw new InvalidDataException(AppText.Get("TokenSecretNotProtected"));
            }

            try {
                IBuffer protectedBuffer = CryptographicBuffer.DecodeFromBase64String(protectedSecret.Substring(Prefix.Length));
                var provider = new DataProtectionProvider();
                IBuffer clearBuffer = await provider.UnprotectAsync(protectedBuffer);

                return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, clearBuffer);
            } catch (Exception ex) {
                throw new InvalidDataException(AppText.Get("TokenSecretDecryptFailed"), ex);
            }
        }

        public static string GetIdentity(DatabaseElement item) {
            string[] parts = {
                item.Type?.Trim().ToLowerInvariant() ?? string.Empty,
                item.Secret?.Replace(" ", string.Empty).Trim().TrimEnd('=').ToUpperInvariant() ?? string.Empty,
                string.IsNullOrWhiteSpace(item.Algorithm) ? "SHA1" : item.Algorithm.Trim().ToUpperInvariant(),
                (item.Digits > 0 ? item.Digits : 6).ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Equals(item.Type, "hotp", StringComparison.OrdinalIgnoreCase)
                    ? Math.Max(0, item.Counter).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : (item.Period > 0 ? item.Period : 30).ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            return string.Join("|", parts.Select(part => part.Length + ":" + part));
        }

    }
}
