using JetBrains.Annotations;

namespace _VuTH.Core.Persistant.SaveSystem.Encrypt
{
    public class DefaultAesEncryptor : AesEncryptor
    {
        public DefaultAesEncryptor() : base("Default_Key_Change_Me", "Default_Salt_Change_Me")
        {
        }
    }
}