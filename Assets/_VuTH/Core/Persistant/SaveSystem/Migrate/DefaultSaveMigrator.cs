using System.Collections.Generic;
using System.Linq;
using _VuTH.Core.Persistant.SaveSystem.Serialize;

namespace _VuTH.Core.Persistant.SaveSystem.Migrate
{
    /// <summary>
    /// Default migrator that passes through data unchanged.
    /// Use when no migration is needed between versions.
    /// </summary>
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

    /// <summary>
    /// Manages a chain of migrators to handle version upgrades.
    /// </summary>
    public class SaveMigrationChain
    {
        private readonly List<ISaveMigrator> _migrators = new();
        private readonly ISerializer _serializer;

        public SaveMigrationChain(ISerializer serializer)
        {
            _serializer = serializer;
        }

        public void AddMigrator(ISaveMigrator migrator)
        {
            _migrators.Add(migrator);
            // Sort by FromVersion to ensure correct order
            _migrators.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
        }

        /// <summary>
        /// Migrates raw payload from its current version to the target version.
        /// </summary>
        public string Migrate(string rawPayload, int currentVersion, int targetVersion)
        {
            if (currentVersion == targetVersion)
            {
                return rawPayload;
            }

            string migratedPayload = rawPayload;
            int fromVersion = currentVersion;

            // Find and apply migrators in sequence
            while (fromVersion < targetVersion)
            {
                var migrator = _migrators.FirstOrDefault(m => m.FromVersion == fromVersion);
                if (migrator == null)
                {
                    // No migrator found - use default passthrough
                    var defaultMigrator = new DefaultSaveMigrator(fromVersion, fromVersion + 1, _serializer);
                    migratedPayload = defaultMigrator.Migrate(migratedPayload);
                }
                else
                {
                    migratedPayload = migrator.Migrate(migratedPayload);
                }

                fromVersion++;
            }

            return migratedPayload;
        }
    }
}
