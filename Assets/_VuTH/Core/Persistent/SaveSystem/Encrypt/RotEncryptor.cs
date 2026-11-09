using UnityEngine.Scripting.APIUpdating;
#nullable enable

using System;
using System.Text;

namespace _VuTH.Core.Persistent.SaveSystem.Encrypt
{
    /// <summary>
    /// ROT cipher - simple character rotation (like ROT13, but configurable).
    /// Very fast, zero allocation, but NOT secure.
    /// Good for preventing casual snooping in save files.
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Encrypt", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class RotEncryptor : IThreadSafeEncryptor
    {
        private readonly int _rotation;

        /// <summary>
        /// Creates ROT cipher with custom rotation.
        /// </summary>
        /// <param name="rotation">Number of positions to rotate (default 13 for ROT13).</param>
        public RotEncryptor(int rotation)
        {
            // 130 is the common period of letters (26) and digits (10); normalization also avoids overflow.
            _rotation = rotation % 130;
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
                if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                {
                    // Rotate letters
                    char basis = c <= 'Z' ? 'A' : 'a';
                    int offset = c - basis;
                    offset = (offset + rotation) % 26;
                    if (offset < 0) offset += 26;
                    result.Append((char)(basis + offset));
                }
                else if (c is >= '0' and <= '9')
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
