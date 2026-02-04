#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace _VuTH.Core.Persistant.SaveSystem.Encrypt
{
    /// <summary>
    /// AES-256 encryption - industry standard, cryptographically secure.
    /// Slower than XOR but provides real security.
    /// Use for sensitive data like player credentials, purchases, etc.
    /// </summary>
    [Serializable]
    public class AesEncryptor : IEncryptor
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        /// <summary>
        /// Creates AES encryptor with password-derived key.
        /// </summary>
        /// <param name="password">Password to derive encryption key from.</param>
        /// <param name="salt">Salt for key derivation (should be unique per app).</param>
        public AesEncryptor(string password, string salt)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            // Derive key from password using PBKDF2
            using var deriveBytes = new Rfc2898DeriveBytes(
                password,
                Encoding.UTF8.GetBytes(salt),
                10000, // iterations
                HashAlgorithmName.SHA256
            );

            _key = deriveBytes.GetBytes(32); // 256-bit key
            _iv = deriveBytes.GetBytes(16);  // 128-bit IV
        }

        /// <summary>
        /// Creates AES encryptor with explicit key and IV.
        /// </summary>
        public AesEncryptor(byte[] key, byte[] iv)
        {
            if (key == null || key.Length != 32)
                throw new ArgumentException("Key must be 32 bytes (256-bit)", nameof(key));
            if (iv == null || iv.Length != 16)
                throw new ArgumentException("IV must be 16 bytes (128-bit)", nameof(iv));

            _key = key;
            _iv = iv;
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                var buffer = Convert.FromBase64String(cipherText);

                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream(buffer);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);

                return sr.ReadToEnd();
            }
            catch (Exception ex) when (ex is FormatException || ex is CryptographicException)
            {
                // Invalid data - return as-is or handle error
                UnityEngine.Debug.LogWarning($"[AesEncryptor] Decryption failed: {ex.Message}");
                return cipherText;
            }
        }
    }
}