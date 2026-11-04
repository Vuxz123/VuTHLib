using System;
using UnityEngine.Scripting.APIUpdating;

namespace _VuTH.Core.Persistent.SaveSystem.Encrypt
{
    /// <summary>
    /// <see cref="AesEncryptor"/> with a built-in password, selectable in the save adapter profile.
    /// The password is public (it is in this library's source): derive your own <see cref="AesEncryptor"/> subclass
    /// with an app-specific password and salt before shipping.
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Encrypt", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class DefaultAesEncryptor : AesEncryptor
    {
        public DefaultAesEncryptor() : base("Default_Key_Change_Me", "Default_Salt_Change_Me")
        {
        }
    }
}
