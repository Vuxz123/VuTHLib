#nullable enable

using System;
using System.Text;

namespace _VuTH.Core.Persistant.SaveSystem.Encrypt
{
    /// <summary>
    /// ROT cipher - simple character rotation (like ROT13, but configurable).
    /// Very fast, zero allocation, but NOT secure.
    /// Good for preventing casual snooping in save files.
    /// </summary>
    [Serializable]
    public class RotEncryptor : IEncryptor
    {
        private readonly int _rotation;

        /// <summary>
        /// Creates ROT cipher with custom rotation.
        /// </summary>
        /// <param name="rotation">Number of positions to rotate (default 13 for ROT13).</param>
        public RotEncryptor(int rotation)
        {
            _rotation = rotation;
        }

        public string Encrypt(string plainText)
        {
            return Rotate(plainText, _rotation);
        }

        public string Decrypt(string cipherText)
        {
            // Decryption is just rotating back
            return Rotate(cipherText, -_rotation);
        }

        private string Rotate(string input, int rotation)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = new StringBuilder(input.Length);

            foreach (char c in input)
            {
                if (char.IsLetter(c))
                {
                    // Rotate letters
                    char basis = char.IsUpper(c) ? 'A' : 'a';
                    int offset = c - basis;
                    offset = (offset + rotation) % 26;
                    if (offset < 0) offset += 26;
                    result.Append((char)(basis + offset));
                }
                else if (char.IsDigit(c))
                {
                    // Rotate digits
                    int offset = c - '0';
                    offset = (offset + rotation) % 10;
                    if (offset < 0) offset += 10;
                    result.Append((char)('0' + offset));
                }
                else
                {
                    // Keep other characters as-is
                    result.Append(c);
                }
            }

            return result.ToString();
        }
    }
}