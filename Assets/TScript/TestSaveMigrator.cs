using _VuTH.Core.Persistant.SaveSystem.Migrate;

namespace TScript
{
    public class TestSaveMigrator : ISaveMigrator
    {
        public int FromVersion => 0;
        public int ToVersion => 1;
        public string Migrate(string rawPayload)
        {
            return rawPayload;
        }
    }
}