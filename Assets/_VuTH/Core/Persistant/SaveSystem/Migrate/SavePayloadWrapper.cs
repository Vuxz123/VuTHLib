#nullable enable

namespace _VuTH.Core.Persistant.SaveSystem.Migrate
{
    /// <summary>
    /// Wrapper for save payload with schema versioning.
    /// </summary>
    public class SavePayloadWrapper
    {
        public int SchemaVersion;
        public string Payload;

        public SavePayloadWrapper()
        {
            // Schema version 2: Migrated from System.Text.Json to Newtonsoft.Json
            SchemaVersion = 2;
            Payload = string.Empty;
        }

        public SavePayloadWrapper(int version, string data)
        {
            SchemaVersion = version;
            Payload = data;
        }
    }
}
