using UnityEngine.Scripting.APIUpdating;
using System;
using _VuTH.Core.Persistent.SaveSystem.Serialize;

namespace _VuTH.Core.Persistent.SaveSystem.Migrate
{
    /// <summary>
    /// Default migrator that passes through data unchanged.
    /// Use when no migration is needed between versions.
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Migrate", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class DefaultSaveMigrator : ISaveMigrator
    {
        public int FromVersion { get; }
        public int ToVersion { get; }
        private readonly ISerializer _serializer;

        public DefaultSaveMigrator(int fromVersion, int toVersion, ISerializer serializer)
        {
            FromVersion = fromVersion;
            ToVersion = toVersion;
            _serializer = serializer;
        }

        public string Migrate(string rawPayload)
        {
            // No migration needed - just pass through
            // In a real scenario, you might deserialize, transform, and reserialize
            return rawPayload;
        }
    }
}
