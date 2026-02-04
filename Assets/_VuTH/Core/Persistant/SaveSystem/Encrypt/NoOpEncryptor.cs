#nullable enable

using System;

namespace _VuTH.Core.Persistant.SaveSystem.Encrypt
{
    /// <summary>
    /// No-op encryptor - passes through data unchanged.
    /// Use for development/debugging only.
    /// </summary>
    [Serializable]
    public class NoOpEncryptor : IEncryptor
    {
        public string Encrypt(string plainText)
        {
            return plainText;
        }

        public string Decrypt(string cipherText)
        {
            return cipherText;
        }
    }
}