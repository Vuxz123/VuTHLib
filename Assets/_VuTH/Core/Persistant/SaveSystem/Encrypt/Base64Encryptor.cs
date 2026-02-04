#nullable enable

using System;
using System.Text;

namespace _VuTH.Core.Persistant.SaveSystem.Encrypt
{
    /// <summary>
    /// Base64 encoding - NOT encryption, just encoding.
    /// Only hides data from casual viewing. Very fast.
    /// Use when you just want to prevent accidental reading in plain text files.
    /// </summary>
    [Serializable]
    public class Base64Encryptor : IEncryptor
    {
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            var bytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(bytes);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                var bytes = Convert.FromBase64String(cipherText);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                // Invalid base64, return as-is
                return cipherText;
            }
        }
    }
}