#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace _VuTH.Core.Persistant.SaveSystem.Encrypt
{
    /// <summary>
    /// Composite encryptor - chains multiple encryptors together.
    /// Example: Base64 → XOR → AES for layered obfuscation + encryption.
    /// Encrypts in order, decrypts in reverse order.
    /// </summary>
    public abstract class CompositeEncryptor : IEncryptor
    {
        private readonly IEncryptor[] _encryptors;

        /// <summary>
        /// Creates composite encryptor from multiple encryptors.
        /// </summary>
        /// <param name="encryptors">Encryptors to chain (applied in order for encryption).</param>
        public CompositeEncryptor(params IEncryptor[] encryptors)
        {
            if (encryptors == null || encryptors.Length == 0)
                throw new ArgumentException("Must provide at least one encryptor", nameof(encryptors));

            _encryptors = encryptors;
        }

        /// <summary>
        /// Creates composite encryptor from list.
        /// </summary>
        public CompositeEncryptor(IEnumerable<IEncryptor> encryptors)
            : this(encryptors?.ToArray() ?? Array.Empty<IEncryptor>())
        {
        }

        public string Encrypt(string plainText)
        {
            var result = plainText;

            // Apply encryptors in order
            foreach (var encryptor in _encryptors)
            {
                result = encryptor.Encrypt(result);
            }

            return result;
        }

        public string Decrypt(string cipherText)
        {
            var result = cipherText;

            // Apply decryptors in reverse order
            for (int i = _encryptors.Length - 1; i >= 0; i--)
            {
                result = _encryptors[i].Decrypt(result);
            }

            return result;
        }
    }
}