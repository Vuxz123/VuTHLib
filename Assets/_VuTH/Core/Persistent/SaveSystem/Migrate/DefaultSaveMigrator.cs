using UnityEngine.Scripting.APIUpdating;
using System;
using UnityEngine;

namespace _VuTH.Core.Persistent.SaveSystem.Migrate
{
    /// <summary>
    /// Pass-through migrator: declares that the save format did not change between two schema versions.
    /// Versions are serialized, so it can be configured in the save adapter profile.
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Migrate", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class DefaultSaveMigrator : ISaveMigrator
    {
        [SerializeField] private int fromVersion;
        [SerializeField] private int toVersion = 1;

        public int FromVersion => fromVersion;
        public int ToVersion => toVersion;

        public DefaultSaveMigrator()
        {
        }

        public DefaultSaveMigrator(int fromVersion, int toVersion)
        {
            this.fromVersion = fromVersion;
            this.toVersion = toVersion;
        }

        public string Migrate(string rawPayload) => rawPayload;
    }
}
