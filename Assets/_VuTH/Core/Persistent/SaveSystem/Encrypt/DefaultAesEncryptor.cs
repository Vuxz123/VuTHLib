using UnityEngine.Scripting.APIUpdating;
using JetBrains.Annotations;

namespace _VuTH.Core.Persistent.SaveSystem.Encrypt
{
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Encrypt", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class DefaultAesEncryptor : AesEncryptor
    {
        public DefaultAesEncryptor() : base("Default_Key_Change_Me", "Default_Salt_Change_Me")
        {
        }
    }
}