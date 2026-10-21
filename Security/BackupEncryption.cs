using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using Digits;

namespace Digits.Security {
    [DataContract]
    [XmlRoot("DigitsEncryptedBackup")]
    public sealed class EncryptedBackupEnvelope {
        [DataMember(Order = 1)]
        public string Format { get; set; }

        [DataMember(Order = 2)]
        public int Version { get; set; }

        [DataMember(Order = 3)]
        public int KdfIterations { get; set; }

        [DataMember(Order = 4)]
        public string Salt { get; set; }

        [DataMember(Order = 5)]
        public string Iv { get; set; }

        [DataMember(Order = 6)]
        public string Ciphertext { get; set; }

        [DataMember(Order = 7)]
        public string AuthenticationTag { get; set; }
    }

    internal static class BackupEncryption {
        public const string FormatName = "Digits.EncryptedBackup";
        private const int CurrentVersion = 1;
        private const int Iterations = 300000;
        private const int MinimumAcceptedIterations = 100000;
        private const int MaximumAcceptedIterations = 1000000;
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int DerivedKeySize = 64;
        private const int MaximumCiphertextSize = 32 * 1024 * 1024;

        public static bool IsSupportedFormat(string format) {
            return !string.IsNullOrWhiteSpace(format) && (string.Equals(format, FormatName, StringComparison.Ordinal) || (format.Length <= 64 && format.EndsWith(".EncryptedBackup", StringComparison.Ordinal)));
        }

        public static Task<EncryptedBackupEnvelope> EncryptAsync(string plaintext, string password) {
            return Task.Run(() => Encrypt(plaintext, password));
        }

        public static Task<string> DecryptAsync(EncryptedBackupEnvelope envelope, string password) {
            return Task.Run(() => Decrypt(envelope, password));
        }

        private static EncryptedBackupEnvelope Encrypt(string plaintext, string password) {
            ValidatePassword(password);

            byte[] salt = ToByteArray(CryptographicBuffer.GenerateRandom(SaltSize));
            byte[] iv = ToByteArray(CryptographicBuffer.GenerateRandom(IvSize));

            byte[] keys = DeriveKeys(password, salt, Iterations);
            byte[] ciphertext;

            try {
                var aesProvider = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbcPkcs7);
                byte[] encryptionKey = CopyRange(keys, 0, 32);

                try {
                    var aesKey = aesProvider.CreateSymmetricKey(ToBuffer(encryptionKey));
                    byte[] clearBytes = Encoding.UTF8.GetBytes(plaintext);

                    try {
                        ciphertext = ToByteArray(CryptographicEngine.Encrypt(aesKey, ToBuffer(clearBytes), ToBuffer(iv)));
                    } finally {
                        Array.Clear(clearBytes, 0, clearBytes.Length);
                    }
                } finally {
                    Array.Clear(encryptionKey, 0, encryptionKey.Length);
                }

                var envelope = new EncryptedBackupEnvelope {
                    Format = FormatName,
                    Version = CurrentVersion,
                    KdfIterations = Iterations,
                    Salt = Convert.ToBase64String(salt),
                    Iv = Convert.ToBase64String(iv),
                    Ciphertext = Convert.ToBase64String(ciphertext)
                };

                envelope.AuthenticationTag = Convert.ToBase64String(ComputeTag(envelope, salt, iv, ciphertext, CopyRange(keys, 32, 32)));

                return envelope;
            } finally {
                Array.Clear(keys, 0, keys.Length);
            }
        }

        private static string Decrypt(EncryptedBackupEnvelope envelope, string password) {
            ValidatePassword(password);

            if (envelope == null || !IsSupportedFormat(envelope.Format) || envelope.Version != CurrentVersion || envelope.KdfIterations < MinimumAcceptedIterations || envelope.KdfIterations > MaximumAcceptedIterations) {
                throw new InvalidDataException(AppText.Get("UnsupportedEncryptedBackupFormat"));
            }

            byte[] salt = Decode(envelope.Salt, SaltSize, SaltSize);
            byte[] iv = Decode(envelope.Iv, IvSize, IvSize);
            byte[] ciphertext = Decode(envelope.Ciphertext, 1, MaximumCiphertextSize);
            byte[] authenticationTag = Decode(envelope.AuthenticationTag, 32, 32);

            if (ciphertext.Length % 16 != 0) {
                throw new InvalidDataException(AppText.Get("EncryptedBackupDamaged"));
            }

            byte[] keys = DeriveKeys(password, salt, envelope.KdfIterations);
            try {
                byte[] expectedTag = ComputeTag(envelope, salt, iv, ciphertext, CopyRange(keys, 32, 32));
                if (!FixedTimeEquals(authenticationTag, expectedTag)) {
                    Array.Clear(expectedTag, 0, expectedTag.Length);
                    throw new InvalidDataException(AppText.Get("BackupPasswordIncorrectOrChanged"));
                }

                Array.Clear(expectedTag, 0, expectedTag.Length);

                var aesProvider = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbcPkcs7);
                byte[] encryptionKey = CopyRange(keys, 0, 32);

                try {
                    var aesKey = aesProvider.CreateSymmetricKey(ToBuffer(encryptionKey));
                    byte[] clearBytes = ToByteArray(CryptographicEngine.Decrypt(aesKey, ToBuffer(ciphertext), ToBuffer(iv)));

                    try {
                        return new UTF8Encoding(false, true).GetString(clearBytes);
                    } finally {
                        Array.Clear(clearBytes, 0, clearBytes.Length);
                    }
                } finally {
                    Array.Clear(encryptionKey, 0, encryptionKey.Length);
                }
            } finally {
                Array.Clear(keys, 0, keys.Length);
            }
        }

        private static byte[] DeriveKeys(string password, byte[] salt, int iterations) {
            var provider = KeyDerivationAlgorithmProvider.OpenAlgorithm(KeyDerivationAlgorithmNames.Pbkdf2Sha256);
            IBuffer passwordBuffer = CryptographicBuffer.ConvertStringToBinary(password, BinaryStringEncoding.Utf8);
            IBuffer saltBuffer = ToBuffer(salt);
            KeyDerivationParameters parameters = KeyDerivationParameters.BuildForPbkdf2(saltBuffer, (uint)iterations);
            CryptographicKey passwordKey = provider.CreateKey(passwordBuffer);
            IBuffer derivedKey = CryptographicEngine.DeriveKeyMaterial(passwordKey, parameters, (uint)DerivedKeySize);

            return ToByteArray(derivedKey);
        }

        private static byte[] ComputeTag(EncryptedBackupEnvelope envelope, byte[] salt, byte[] iv, byte[] ciphertext, byte[] macKey) {
            try {
                using (var payload = new MemoryStream()) {
                    using (var writer = new BinaryWriter(payload, Encoding.UTF8)) {
                        writer.Write(envelope.Format ?? string.Empty);
                        writer.Write(envelope.Version);
                        writer.Write(envelope.KdfIterations);
                        WriteBytes(writer, salt);
                        WriteBytes(writer, iv);
                        WriteBytes(writer, ciphertext);
                        writer.Flush();

                        var provider = MacAlgorithmProvider.OpenAlgorithm(MacAlgorithmNames.HmacSha256);
                        var key = provider.CreateKey(ToBuffer(macKey));
                        IBuffer signature = CryptographicEngine.Sign(key, ToBuffer(payload.ToArray()));

                        return ToByteArray(signature);
                    }
                }
            } finally {
                Array.Clear(macKey, 0, macKey.Length);
            }
        }

        private static void WriteBytes(BinaryWriter writer, byte[] value) {
            writer.Write(value.Length);
            writer.Write(value);
        }

        private static byte[] Decode(string value, int minimumLength, int maximumLength) {
            try {
                byte[] decoded = Convert.FromBase64String(value ?? string.Empty);

                if (decoded.Length < minimumLength || decoded.Length > maximumLength) {
                    throw new InvalidDataException(AppText.Get("EncryptedBackupDamaged"));
                }

                return decoded;
            } catch (FormatException ex) {
                throw new InvalidDataException(AppText.Get("EncryptedBackupDamaged"), ex);
            }
        }

        private static byte[] CopyRange(byte[] source, int offset, int length) {
            var result = new byte[length];
            System.Buffer.BlockCopy(source, offset, result, 0, length);

            return result;
        }

        private static IBuffer ToBuffer(byte[] value) {
            return CryptographicBuffer.CreateFromByteArray(value);
        }

        private static byte[] ToByteArray(IBuffer value) {
            CryptographicBuffer.CopyToByteArray(value, out byte[] result);
            return result;
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right) {
            if (left == null || right == null || left.Length != right.Length) {
                return false;
            }

            int difference = 0;
            for (int i = 0; i < left.Length; i++) {
                difference |= left[i] ^ right[i];
            }

            return difference == 0;
        }

        private static void ValidatePassword(string password) {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 12) {
                throw new ArgumentException(AppText.Get("BackupPassphraseMinLength"), nameof(password));
            }
        }
    }
}
