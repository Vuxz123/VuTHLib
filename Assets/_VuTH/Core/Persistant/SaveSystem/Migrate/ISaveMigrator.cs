namespace _VuTH.Core.Persistant.SaveSystem.Migrate
{
    /// <summary>
    /// Migration contract for handling schema version changes.
    /// Each migrator handles a specific version range.
    /// </summary>
    public interface ISaveMigrator
    {
        /// <summary>
        /// The source schema version this migrator handles (from version).
        /// </summary>
        int FromVersion { get; }

        /// <summary>
        /// The target schema version this migrator produces (to version).
        /// </summary>
        int ToVersion { get; }

        /// <summary>
        /// Migrates raw payload from FromVersion to ToVersion.
        /// Returns the migrated raw payload string.
        /// </summary>
        string Migrate(string rawPayload);
    }
}
