#nullable enable
using System;
using System.Collections.Generic;

namespace _VuTH.Core.Persistent.SaveSystem.Migrate
{
    /// <summary>
    /// The schema of one kind of saved data: the version it is written with and the migrators that upgrade older
    /// saves of that same data. Pass it to <see cref="IVersionedSaveService"/> so the data is versioned on its own,
    /// independently of the service-wide schema version (which every other key shares).
    /// <para>
    /// The stored version continues from the one already in the save: data written before it had its own schema
    /// carries the service-wide version (1 by default), so start the own version there.
    /// </para>
    /// </summary>
    public sealed class SaveSchema
    {
        /// <summary>Version written with the data, and the version older saves are migrated to.</summary>
        public int Version { get; }

        /// <summary>Migrators of this data only.</summary>
        public SaveMigrationChain Migrations { get; }

        /// <summary>Throws if a migrator is invalid or two start at the same version.</summary>
        public SaveSchema(int version, params ISaveMigrator[] migrators)
            : this(version, (IEnumerable<ISaveMigrator>)migrators)
        {
        }

        /// <summary>Throws if a migrator is invalid or two start at the same version.</summary>
        public SaveSchema(int version, IEnumerable<ISaveMigrator>? migrators)
        {
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version), "Schema version cannot be negative.");

            Version = version;
            Migrations = new SaveMigrationChain();
            if (migrators == null) return;

            foreach (var migrator in migrators)
            {
                Migrations.AddMigrator(migrator);
            }
        }
    }
}
