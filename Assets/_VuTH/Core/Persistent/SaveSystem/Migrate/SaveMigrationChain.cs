using System;
using System.Collections.Generic;

namespace _VuTH.Core.Persistent.SaveSystem.Migrate
{
    /// <summary>
    /// Ordered set of migrators that upgrades a payload from its stored schema version to the current one.
    /// Every step must be covered: a missing migrator is an error, never a silent pass-through. When a version bump
    /// does not change the format, register a <see cref="DefaultSaveMigrator"/> for that step.
    /// </summary>
    public class SaveMigrationChain
    {
        private readonly SortedDictionary<int, ISaveMigrator> _migrators = new();

        public int Count => _migrators.Count;

        /// <summary>Adds a migrator. Throws if it is invalid or another one already starts at its version.</summary>
        public void AddMigrator(ISaveMigrator migrator)
        {
            if (migrator == null) throw new ArgumentNullException(nameof(migrator));

            if (migrator.ToVersion <= migrator.FromVersion)
            {
                throw new ArgumentException(
                    $"{migrator.GetType().Name} must migrate to a higher version " +
                    $"(from {migrator.FromVersion} to {migrator.ToVersion}).", nameof(migrator));
            }

            if (_migrators.TryGetValue(migrator.FromVersion, out var existing))
            {
                throw new ArgumentException(
                    $"Two migrators start at schema version {migrator.FromVersion}: " +
                    $"{existing.GetType().Name} and {migrator.GetType().Name}.", nameof(migrator));
            }

            _migrators.Add(migrator.FromVersion, migrator);
        }

        /// <summary>
        /// Migrates <paramref name="rawPayload"/> from <paramref name="currentVersion"/> to
        /// <paramref name="targetVersion"/>. Throws <see cref="SaveMigrationException"/> when a step is missing.
        /// </summary>
        public string Migrate(string rawPayload, int currentVersion, int targetVersion)
        {
            if (currentVersion > targetVersion)
            {
                throw new SaveMigrationException(
                    $"Cannot migrate down from schema version {currentVersion} to {targetVersion}.");
            }

            var payload = rawPayload;
            var version = currentVersion;

            while (version < targetVersion)
            {
                if (!_migrators.TryGetValue(version, out var migrator))
                {
                    throw new SaveMigrationException(
                        $"No migrator from schema version {version} (target {targetVersion}). Register one — " +
                        $"a DefaultSaveMigrator({version}, {version + 1}) if the format did not change.");
                }

                if (migrator.ToVersion > targetVersion)
                {
                    throw new SaveMigrationException(
                        $"{migrator.GetType().Name} migrates to schema version {migrator.ToVersion}, " +
                        $"past the target {targetVersion}.");
                }

                payload = migrator.Migrate(payload) ?? throw new SaveMigrationException(
                    $"{migrator.GetType().Name} returned no payload (from schema version {version}).");
                version = migrator.ToVersion;
            }

            return payload;
        }
    }

    /// <summary>Thrown when a save cannot be migrated to the current schema version.</summary>
    public class SaveMigrationException : Exception
    {
        public SaveMigrationException(string message) : base(message)
        {
        }
    }
}
