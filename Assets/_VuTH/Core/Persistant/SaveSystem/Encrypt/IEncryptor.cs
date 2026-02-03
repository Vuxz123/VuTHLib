#nullable enable

namespace _VuTH.Core.Persistant.SaveSystem.Encrypt
{
    /// <summary>
    /// Encryption contract - pluggable module.
    /// </summary>
    public interface IEncryptor
    {
        /// <summary>
        /// Encrypts a string.
        /// </summary>
        string Encrypt(string plainText);

        /// <summary>
        /// Decrypts a string.
        /// </summary>
        string Decrypt(string cipherText);
    }
}
