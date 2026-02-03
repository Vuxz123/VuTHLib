#nullable enable
using _VuTH.Core.Persistant.SaveSystem.Encrypt;

namespace _VuTH.Core.Persistant.SaveSystem
{
    /// <summary>
    /// Simple XOR encryption implementation.
    /// Note: This is NOT secure encryption - use for obfuscation only.
    /// For production, use AES or similar encryption.
    /// </summary>
    public class XorEncryptor : IEncryptor
    {
        private readonly byte[] _key;

        public XorEncryptor() : this("VuTH_Save_System_Key_2024")
        {
        }

        public XorEncryptor(string key)
        {
            _key = System.Text.Encoding.UTF8.GetBytes(key);
        }

        public string Encrypt(string plainText)
        {
            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            byte[] outputBytes = new byte[inputBytes.Length];

            for (int i = 0; i < inputBytes.Length; i++)
            {
                outputBytes[i] = (byte)(inputBytes[i] ^ _key[i % _key.Length]);
            }

            return System.Convert.ToBase64String(outputBytes);
        }

        public string Decrypt(string cipherText)
        {
            try
            {
                byte[] inputBytes = System.Convert.FromBase64String(cipherText);
                byte[] outputBytes = new byte[inputBytes.Length];

                for (int i = 0; i < inputBytes.Length; i++)
                {
                    outputBytes[i] = (byte)(inputBytes[i] ^ _key[i % _key.Length]);
                }

                return System.Text.Encoding.UTF8.GetString(outputBytes);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[XorEncryptor] Decrypt failed: {ex.Message}");
                throw;
            }
        }
    }
}
