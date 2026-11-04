using _VuTH.Core.Persistent.SaveSystem.Migrate;
using _VuTH.Core.Persistent.SaveSystem.Serialize;
using NUnit.Framework;

namespace _VuTH.Core.Persistent.SaveSystem.Tests.Runtime
{
    /// <summary>
    /// Unit tests for SaveMigrationChain.
    /// </summary>
    [TestFixture]
    public class SaveMigrationChainTests
    {
        private DeterministicSerializer _serializer;

        [SetUp]
        public void SetUp()
        {
            _serializer = new DeterministicSerializer();
        }

        [Test]
        public void Migrate_WithSpecificMigrator_AppliesTransform()
        {
            // Arrange
            var chain = new SaveMigrationChain();
            chain.AddMigrator(new VersionMigrator(1, 2, _serializer));
            var rawPayload = "v1_data";

            // Act
            var result = chain.Migrate(rawPayload, 1, 2);

            // Assert
            Assert.AreEqual("migrated_from_v1_to_v2", result, "Migrator should transform payload");
        }

        [Test]
        public void Migrate_MissingMigrator_Throws()
        {
            // A missing step used to silently pass the old payload through as if it were the new format.
            var chain = new SaveMigrationChain();

            var error = Assert.Throws<SaveMigrationException>(() => chain.Migrate("unchanged_data", 1, 2));
            StringAssert.Contains("No migrator from schema version 1", error.Message);
        }

        [Test]
        public void Migrate_DefaultSaveMigrator_PassesThrough()
        {
            var chain = new SaveMigrationChain();
            chain.AddMigrator(new DefaultSaveMigrator(1, 2));

            Assert.AreEqual("unchanged_data", chain.Migrate("unchanged_data", 1, 2));
        }

        [Test]
        public void Migrate_HonorsToVersion_ForMultiVersionSteps()
        {
            var chain = new SaveMigrationChain();
            chain.AddMigrator(new VersionMigrator(1, 3, _serializer));
            chain.AddMigrator(new VersionMigrator(3, 4, _serializer));

            Assert.AreEqual("migrated_from_v3_to_v4", chain.Migrate("v1", 1, 4));
        }

        [Test]
        public void Migrate_Downgrade_Throws()
        {
            Assert.Throws<SaveMigrationException>(() => new SaveMigrationChain().Migrate("x", 3, 2));
        }

        [Test]
        public void AddMigrator_DuplicateStartVersion_Throws()
        {
            var chain = new SaveMigrationChain();
            chain.AddMigrator(new DefaultSaveMigrator(1, 2));

            Assert.Throws<System.ArgumentException>(() => chain.AddMigrator(new VersionMigrator(1, 2, _serializer)));
        }

        [Test]
        public void AddMigrator_NotIncreasingVersion_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new SaveMigrationChain().AddMigrator(new DefaultSaveMigrator(2, 2)));
        }

        [Test]
        public void Migrate_MultiStep_AppliesInSequence()
        {
            // Arrange
            var chain = new SaveMigrationChain();
            chain.AddMigrator(new VersionMigrator(1, 2, _serializer));
            chain.AddMigrator(new VersionMigrator(2, 3, _serializer));
            var rawPayload = "v1";

            // Act
            var result = chain.Migrate(rawPayload, 1, 3);

            // Assert
            Assert.AreEqual("migrated_from_v2_to_v3", result, "Should apply both migrators");
        }
    }

    #region Test Fixtures

    /// <summary>
    /// Test migrator that adds a prefix indicating the transformation.
    /// </summary>
    public class VersionMigrator : ISaveMigrator
    {
        public int FromVersion { get; }
        public int ToVersion { get; }
        private readonly ISerializer _serializer;

        public VersionMigrator(int fromVersion, int toVersion, ISerializer serializer)
        {
            FromVersion = fromVersion;
            ToVersion = toVersion;
            _serializer = serializer;
        }

        public string Migrate(string rawPayload)
        {
            // Simple transformation for testing
            return $"migrated_from_v{FromVersion}_to_v{ToVersion}";
        }
    }

    #endregion
}
