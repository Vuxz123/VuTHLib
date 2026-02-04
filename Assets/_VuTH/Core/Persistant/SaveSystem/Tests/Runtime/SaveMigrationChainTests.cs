using _VuTH.Core.Persistant.SaveSystem.Migrate;
using _VuTH.Core.Persistant.SaveSystem.Serialize;
using NUnit.Framework;

namespace _VuTH.Core.Persistant.SaveSystem.Tests.Runtime
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
            var chain = new SaveMigrationChain(_serializer);
            chain.AddMigrator(new VersionMigrator(1, 2, _serializer));
            var rawPayload = "v1_data";

            // Act
            var result = chain.Migrate(rawPayload, 1, 2);

            // Assert
            Assert.AreEqual("migrated_from_v1_to_v2", result, "Migrator should transform payload");
        }

        [Test]
        public void Migrate_MissingMigrator_UsesDefaultPassthrough()
        {
            // Arrange
            var chain = new SaveMigrationChain(_serializer);
            // No migrators added
            var rawPayload = "unchanged_data";

            // Act
            var result = chain.Migrate(rawPayload, 1, 2);

            // Assert - DefaultSaveMigrator should pass through
            Assert.AreEqual(rawPayload, result, "Default migrator should pass through");
        }

        [Test]
        public void Migrate_MultiStep_AppliesInSequence()
        {
            // Arrange
            var chain = new SaveMigrationChain(_serializer);
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
