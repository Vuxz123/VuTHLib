#nullable enable

namespace _VuTH.Core.Persistent.SaveSystem.Migrate
{
    /// <summary>
    /// Stored envelope: the serialized data plus the schema version it was written with.
    /// </summary>
    public class SavePayloadWrapper
    {
        /// <summary>0 when missing from the stored data.</summary>
        public int SchemaVersion;

        /// <summary>Null when missing from the stored data (treated as corrupted).</summary>
        public string? Payload;

        public SavePayloadWrapper()
        {
        }

        public SavePayloadWrapper(int version, string data)
        {
            SchemaVersion = version;
            Payload = data;
        }
    }
}
