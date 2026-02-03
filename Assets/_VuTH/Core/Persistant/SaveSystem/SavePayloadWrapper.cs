#nullable enable

using _VuTH.Core.Persistant.SaveSystem.Serialize;

namespace _VuTH.Core.Persistant.SaveSystem
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
            SchemaVersion = 1;
            Payload = string.Empty;
        }

        public SavePayloadWrapper(int version, string data)
        {
            SchemaVersion = version;
            Payload = data;
        }
    }

    /// <summary>
    /// Migration chain for handling schema versions.
    /// </summary>
    public class SaveMigrationChain
    {
        private readonly ISerializer _serializer;

        public SaveMigrationChain(ISerializer serializer)
        {
            _serializer = serializer;
        }

        public string Migrate(string payload, int fromVersion, int toVersion)
        {
            // TODO: Implement actual migration logic
            // For now, just return the payload as-is
            return payload;
        }
    }
}
