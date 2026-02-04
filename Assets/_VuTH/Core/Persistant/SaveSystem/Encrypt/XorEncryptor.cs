using System;
using System.Text;

namespace _VuTH.Core.Persistant.SaveSystem.Encrypt
{
    /// <summary>
    /// Simple XOR-based encryptor for demonstration.
    /// WARNING: This is NOT secure encryption - suitable for basic obfuscation only.
    /// For production, use AES or other strong encryption.
    /// Pluggable implementation of IEncryptor.
    /// </summary>
    [Serializable]
    public class XorEncryptor : IEncryptor
    {
        private readonly byte[] _key;

        public XorEncryptor() : this("DefaultSaveKey2024")
        {
        }

        public XorEncryptor(string keyString)
        {
            // Generate a consistent key from the string
            _key = GenerateKey(keyString);
        }

        private byte[] GenerateKey(string keyString)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(keyString);
            byte[] result = new byte[256];

            for (int i = 0; i < 256; i++)
            {
                result[i] = keyBytes[i % keyBytes.Length];
            }

            return result;
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return plainText;
            }

            byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] outputBytes = new byte[inputBytes.Length];

            for (int i = 0; i < inputBytes.Length; i++)
            {
                outputBytes[i] = (byte)(inputBytes[i] ^ _key[i % _key.Length]);
            }

            return System.Convert.ToBase64String(outputBytes);
        }

        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
            {
                return encryptedText;
            }

            try
            {
                byte[] inputBytes = System.Convert.FromBase64String(encryptedText);
                byte[] outputBytes = new byte[inputBytes.Length];

                for (int i = 0; i < inputBytes.Length; i++)
                {
                    outputBytes[i] = (byte)(inputBytes[i] ^ _key[i % _key.Length]);
                }

                return Encoding.UTF8.GetString(outputBytes);
            }
            catch
            {
                // Return original if decryption fails (not base64 or invalid)
                return encryptedText;
            }
        }
    }
}
