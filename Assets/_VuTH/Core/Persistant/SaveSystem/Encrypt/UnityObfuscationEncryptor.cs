#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace _VuTH.Core.Persistant.SaveSystem.Encrypt
{
    /// <summary>
    /// Unity device-specific obfuscation.
    /// Uses SystemInfo.deviceUniqueIdentifier to create device-specific encryption.
    /// Save files encrypted on one device won't work on another device.
    /// Good for preventing save file sharing between players.
    /// </summary>
    [Serializable]
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

            try
            {
                var bytes = Convert.FromBase64String(cipherText);
                var result = new byte[bytes.Length];

                // XOR with device key (same operation for encrypt/decrypt)
                for (int i = 0; i < bytes.Length; i++)
                {
                    result[i] = (byte)(bytes[i] ^ _deviceKey[i % _deviceKey.Length]);
                }

                return Encoding.UTF8.GetString(result);
            }
            catch (FormatException)
            {
                Debug.LogWarning("[UnityObfuscation] Invalid encrypted data");
                return cipherText;
            }
        }
    }
}