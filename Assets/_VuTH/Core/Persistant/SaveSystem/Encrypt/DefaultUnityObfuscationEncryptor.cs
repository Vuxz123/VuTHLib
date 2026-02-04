using JetBrains.Annotations;

namespace _VuTH.Core.Persistant.SaveSystem.Encrypt
{
    public class DefaultUnityObfuscationEncryptor : UnityObfuscationEncryptor
    {
        public DefaultUnityObfuscationEncryptor() : base("Default_Unity_Obfuscation_Key")
        {
        }
    }
}