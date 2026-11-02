using UnityEngine.Scripting.APIUpdating;
using JetBrains.Annotations;

namespace _VuTH.Core.Persistent.SaveSystem.Encrypt
{
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Encrypt", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class DefaultUnityObfuscationEncryptor : UnityObfuscationEncryptor
    {
        public DefaultUnityObfuscationEncryptor() : base("Default_Unity_Obfuscation_Key")
        {
        }
    }
}