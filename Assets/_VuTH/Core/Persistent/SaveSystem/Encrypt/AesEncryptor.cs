using UnityEngine.Scripting.APIUpdating;
#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace _VuTH.Core.Persistent.SaveSystem.Encrypt
{
    /// <summary>
    /// AES-256-CBC with a random IV per save and an HMAC-SHA256 tag (encrypt-then-MAC): the same data never encrypts
    /// to the same text, and any modification of the save is detected (reported as corrupted).
    /// <para>
    /// The key ships inside the game build, so this is strong obfuscation against casual save editing, not protection
    /// against someone who extracts the key from the binary. Never rely on it for purchases or other server-trusted data.
    /// </para>
    /// <para>
    /// Saves written by the previous format (fixed IV, no tag) are still read; they are re-written in the new format on
    /// the next save.
    /// </para>
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Encrypt", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class AesEncryptor : IEncryptor
    {
        /// <summary>Marks the authenticated format. Legacy ciphertext is plain Base64, which never contains ':'.</summary>
        private const string FormatPrefix = "aes2:";

        private const int KeySize = 32;
        private const int IvSize = 16;
        private const int TagSize = 32;

        private readonly byte[] _key;
        private readonly byte[] _macKey;
        private readonly byte[] _legacyIv;

        /// <summary>
        /// Creates an encryptor with keys derived from a password (PBKDF2-SHA256).
        /// </summary>
        /// <param name="password">Password to derive the keys from.</param>
        /// <param name="salt">Salt for key derivation (should be unique per app).</param>
        public AesEncryptor(string password, string salt)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            using var deriveBytes = new Rfc2898DeriveBytes(
                password,
                Encoding.UTF8.GetBytes(salt ?? string.Empty),
                10000,
                HashAlgorithmName.SHA256);

            // Same first 48 bytes as the previous format (key, then IV), so its saves still decrypt.
            _key = deriveBytes.GetBytes(KeySize);
            _legacyIv = deriveBytes.GetBytes(IvSize);
            _macKey = deriveBytes.GetBytes(KeySize);
        }

        /// <summary>
        /// Creates an encryptor from an explicit 256-bit key. <paramref name="iv"/> is only used to read saves written
        /// by the previous format; new saves use a random IV.
        /// </summary>
        public AesEncryptor(byte[] key, byte[] iv)
        {
            if (key == null || key.Length != KeySize)
                throw new ArgumentException("Key must be 32 bytes (256-bit)", nameof(key));
            if (iv == null || iv.Length != IvSize)
                throw new ArgumentException("IV must be 16 bytes (128-bit)", nameof(iv));

            _key = (byte[])key.Clone();
            _legacyIv = (byte[])iv.Clone();
            using var hmac = new HMACSHA256(_key);
            _macKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("VuTH.AesEncryptor.mac"));
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using var aes = CreateAes();
            aes.GenerateIV();
            var iv = aes.IV;

            byte[] cipher;
            using (var encryptor = aes.CreateEncryptor())
            {
                var plain = Encoding.UTF8.GetBytes(plainText);
                cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
            }

            var output = new byte[IvSize + cipher.Length + TagSize];
            Buffer.BlockCopy(iv, 0, output, 0, IvSize);
            Buffer.BlockCopy(cipher, 0, output, IvSize, cipher.Length);
            var tag = ComputeTag(output, IvSize + cipher.Length);
            Buffer.BlockCopy(tag, 0, output, IvSize + cipher.Length, TagSize);

            return FormatPrefix + Convert.ToBase64String(output);
        }

        /// <summary>Throws on invalid or modified data; the save service reports it as corrupted.</summary>
        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            return cipherText.StartsWith(FormatPrefix, StringComparison.Ordinal)
                ? DecryptAuthenticated(cipherText.Substring(FormatPrefix.Length))
                : DecryptLegacy(cipherText);
        }

        private string DecryptAuthenticated(string base64)
        {
            var input = Convert.FromBase64String(base64);
            var cipherLength = input.Length - IvSize - TagSize;
            if (cipherLength <= 0 || cipherLength % IvSize != 0)
                throw new CryptographicException("Encrypted data has an invalid length.");

            var expectedTag = ComputeTag(input, IvSize + cipherLength);
            if (!FixedTimeEquals(expectedTag, input, IvSize + cipherLength))
                throw new CryptographicException("Encrypted data was modified or uses another key.");

            using var aes = CreateAes();
            var iv = new byte[IvSize];
            Buffer.BlockCopy(input, 0, iv, 0, IvSize);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(input, IvSize, cipherLength);
            return Encoding.UTF8.GetString(plain);
        }

        private string DecryptLegacy(string base64)
        {
            var buffer = Convert.FromBase64String(base64);

            using var aes = CreateAes();
            aes.IV = _legacyIv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(buffer);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }

        private Aes CreateAes()
        {
            var aes = Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes;
        }

        private byte[] ComputeTag(byte[] data, int count)
        {
            using var hmac = new HMACSHA256(_macKey);
            return hmac.ComputeHash(data, 0, count);
        }

        /// <summary>Constant-time comparison (CryptographicOperations is not in .NET Standard 2.1).</summary>
        private static bool FixedTimeEquals(byte[] expected, byte[] buffer, int offset)
        {
            if (buffer.Length - offset != expected.Length) return false;

            var diff = 0;
            for (var i = 0; i < expected.Length; i++)
            {
                diff |= expected[i] ^ buffer[offset + i];
            }

            return diff == 0;
        }
    }
}
