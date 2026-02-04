using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using _VuTH.Core.Persistant.SaveSystem.Backend;
using _VuTH.Core.Persistant.SaveSystem.Encrypt;
using _VuTH.Core.Persistant.SaveSystem.Serialize;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace _VuTH.Core.Persistant.SaveSystem.Tests.EditMode
{
    /// <summary>
    /// EditMode integration tests for JsonFileSaveBackend.
    /// Uses temporary directory to avoid polluting project.
    /// </summary>
    [TestFixture]
    public class JsonFileBackendIntegrationTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"VuTH_SaveTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public async Task SaveAndLoad_RoundTripsData()
        {
            // Arrange
            var backend = new JsonFileSaveBackend(Path.Combine(_tempDir, "test_save.json"));
            var testData = "{\"playerName\":\"TestPlayer\",\"level\":42}";

            // Act
            CancellationToken ct = CancellationToken.None;
            await backend.SaveRawAsync("key1", testData, ct);
            CancellationToken ct2 = CancellationToken.None;
            var loaded = await backend.LoadRawAsync("key1", ct2);

            // Assert
            Assert.AreEqual(testData, loaded, "Loaded data should match saved data");
        }

        [Test]
        public async Task Load_KeyNotFound_ReturnsNull()
        {
            // Arrange
            var backend = new JsonFileSaveBackend(Path.Combine(_tempDir, "test_save.json"));

            // Act
            CancellationToken ct = CancellationToken.None;
            var result = await backend.LoadRawAsync("missing_key", ct);

            // Assert
            Assert.IsNull(result, "Missing key should return null");
        }

        [Test]
        public async Task Delete_RemovesFile()
        {
            // Arrange
            var backend = new JsonFileSaveBackend(Path.Combine(_tempDir, "delete_test.json"));
            CancellationToken ct = CancellationToken.None;
            await backend.SaveRawAsync("to_delete", "data", ct);
            CancellationToken ct2 = CancellationToken.None;
            Assert.IsTrue(await backend.Exists("to_delete", ct2));

            // Act
            CancellationToken ct3 = CancellationToken.None;
            await backend.DeleteAsync("to_delete", ct3);

            // Assert
            CancellationToken ct4 = CancellationToken.None;
            Assert.IsFalse(await backend.Exists("to_delete", ct4), "Key should be deleted");
        }

        [Test]
        public async Task Exists_ReturnsTrue_AfterSave()
        {
            // Arrange
            var backend = new JsonFileSaveBackend(Path.Combine(_tempDir, "exists_test.json"));

            // Act & Assert
            CancellationToken ct = CancellationToken.None;
            Assert.IsFalse(await backend.Exists("new_key", ct), "Should not exist before save");
            CancellationToken ct2 = CancellationToken.None;
            await backend.SaveRawAsync("new_key", "data", ct2);
            CancellationToken ct3 = CancellationToken.None;
            Assert.IsTrue(await backend.Exists("new_key", ct3), "Should exist after save");
        }
    }

    /// <summary>
    /// EditMode integration tests for XorEncryptor.
    /// </summary>
    [TestFixture]
    public class XorEncryptorTests
    {
        [Test]
        public void Encrypt_Decrypt_RoundTrips()
        {
            // Arrange
            var encryptor = new XorEncryptor();
            var original = "This is secret data!";

            // Act
            var encrypted = encryptor.Encrypt(original);
            var decrypted = encryptor.Decrypt(encrypted);

            // Assert
            Assert.AreEqual(original, decrypted, "Decrypted data should match original");
        }

        [Test]
        public void Encrypt_ProducesDifferentOutput()
        {
            // Arrange
            var encryptor = new XorEncryptor();
            var original = "Sensitive data";

            // Act
            var encrypted = encryptor.Encrypt(original);

            // Assert
            Assert.AreNotEqual(original, encrypted, "Encrypted data should differ from original");
        }
    }

    /// <summary>
    /// EditMode integration tests for NewtonsoftJsonSerializer.
    /// </summary>
    [TestFixture]
    public class NewtonsoftJsonSerializerTests
    {
        private NewtonsoftJsonSerializer _serializer;

        [SetUp]
        public void SetUp()
        {
            _serializer = new NewtonsoftJsonSerializer();
        }

        [Test]
        public void Serialize_Deserialize_RoundTrips()
        {
            // Arrange
            var testObject = new TestPayload { Name = "Test", Value = 42 };

            // Act
            var json = _serializer.Serialize(testObject);
            var result = _serializer.Deserialize<TestPayload>(json);

            // Assert
            Assert.AreEqual(testObject.Name, result.Name);
            Assert.AreEqual(testObject.Value, result.Value);
        }

        [Test]
        public void Serialize_SpecialCharacters_Escaped()
        {
            // Arrange
            var testObject = new TestPayload { Name = "Test\"With\\Quotes", Value = 1 };

            // Act
            var json = _serializer.Serialize(testObject);
            var result = _serializer.Deserialize<TestPayload>(json);

            // Assert
            Assert.AreEqual(testObject.Name, result.Name);
        }
    }

    #region Test Fixtures

    [System.Serializable]
    public class TestPayload
    {
        public string Name;
        public int Value;
    }

    #endregion
}
