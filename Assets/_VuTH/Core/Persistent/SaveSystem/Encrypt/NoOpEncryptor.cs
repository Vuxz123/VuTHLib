using UnityEngine.Scripting.APIUpdating;
#nullable enable

using System;

namespace _VuTH.Core.Persistent.SaveSystem.Encrypt
{
    /// <summary>
    /// No-op encryptor - passes through data unchanged.
    /// Use for development/debugging only.
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Encrypt", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class NoOpEncryptor : IThreadSafeEncryptor
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