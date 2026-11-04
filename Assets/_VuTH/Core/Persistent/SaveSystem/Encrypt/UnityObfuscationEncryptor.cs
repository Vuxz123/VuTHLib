using UnityEngine.Scripting.APIUpdating;
#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace _VuTH.Core.Persistent.SaveSystem.Encrypt
{
    /// <summary>
    /// Device-bound XOR obfuscation keyed by <see cref="SystemInfo.deviceUniqueIdentifier"/>: a save copied to another
    /// device cannot be read. NOT encryption.
    /// <para>
    /// WARNING: the identifier is not guaranteed to be stable. It can change after an OS reset, and on iOS when every
    /// app of the vendor is uninstalled; saves become unreadable then (reported as corrupted and backed up). Do not
    /// use it for data the player cannot afford to lose, or for saves synced to the cloud.
    /// </para>
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Encrypt", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class UnityObfuscationEncryptor : IEncryptor
    {
        private readonly byte[] _deviceKey;
        private readonly string _appSalt;

        /// <summary>
        /// Creates Unity device-specific encryptor.
        /// </summary>
        /// <param name="appSalt">Application-specific salt (change this per app!).</param>
        public UnityObfuscationEncryptor(string appSalt)
        {
            _appSalt = appSalt;

            // Generate device-specific key from device ID + app salt
            var deviceId = SystemInfo.deviceUniqueIdentifier;
            var combined = deviceId + _appSalt;

            using var sha256 = SHA256.Create();
            _deviceKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            var bytes = Encoding.UTF8.GetBytes(plainText);
            var result = new byte[bytes.Length];

            // XOR with device key
            for (int i = 0; i < bytes.Length; i++)
            {
                result[i] = (byte)(bytes[i] ^ _deviceKey[i % _deviceKey.Length]);
            }

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            // Throws FormatException on invalid data: the save service reports it as corrupted.
            var bytes = Convert.FromBase64String(cipherText);
            var result = new byte[bytes.Length];

            // XOR with device key (same operation for encrypt/decrypt)
            for (int i = 0; i < bytes.Length; i++)
            {
                result[i] = (byte)(bytes[i] ^ _deviceKey[i % _deviceKey.Length]);
            }

            return Encoding.UTF8.GetString(result);
        }
    }
}