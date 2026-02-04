using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using _VuTH.Core.Persistant.SaveSystem.Backend;
using _VuTH.Core.Persistant.SaveSystem.Encrypt;
using _VuTH.Core.Persistant.SaveSystem.Events;
using _VuTH.Core.Persistant.SaveSystem.Serialize;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace _VuTH.Core.Persistant.SaveSystem.Tests.Runtime
{
    #region Test Doubles

    /// <summary>
    /// In-memory implementation of ISaveBackend for fast, deterministic tests.
    /// </summary>
    public class InMemorySaveBackend : ISaveBackend
    {
        private readonly Dictionary<string, string> _storage = new();

        public UniTask SaveRawAsync(string key, string data, CancellationToken cancellationToken = default)
        {
            _storage[key] = data;
            return UniTask.CompletedTask;
        }

        public UniTask<string?> LoadRawAsync(string key, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(_storage.TryGetValue(key, out var value) ? value : null);
        }

        public UniTask<bool> Exists(string key, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(_storage.ContainsKey(key));
        }

        public UniTask DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _storage.Remove(key);
            return UniTask.CompletedTask;
        }

        public void Clear()
        {
            _storage.Clear();
        }
    }

    /// <summary>
    /// Pass-through IEncryptor for tests that don't need encryption.
    /// </summary>
    public class PassThroughEncryptor : IEncryptor
    {
        public string Encrypt(string plainText) => plainText;
        public string Decrypt(string encryptedText) => encryptedText;
    }

    /// <summary>
    /// Failing IEncryptor for testing error handling.
    /// </summary>
    public class FailingEncryptor : IEncryptor
    {
        public string Encrypt(string plainText) => throw new InvalidOperationException("Encrypt failed");
        public string Decrypt(string encryptedText) => throw new InvalidOperationException("Decrypt failed");
    }

    /// <summary>
    /// Deterministic ISerializer using simple JSON for tests.
    /// </summary>
    public class DeterministicSerializer : ISerializer
    {
        public string Serialize<T>(T data)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(data);
        }

        public T Deserialize<T>(string json)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json) ?? throw new InvalidOperationException("Deserialize returned null");
        }
    }

    /// <summary>
    /// Spy ISaveEventPublisher to verify event calls.
    /// </summary>
    public class SpySaveEventPublisher : ISaveEventPublisher
    {
        public int OnSaveSuccessCallCount;
        public int OnSaveFailedCallCount;
        public int OnLoadSuccessCallCount;
        public int OnLoadFailedCallCount;
        public List<(string Key, string Type, string Message)> FailedCalls = new();

        public void OnSaveSuccess(string key, string typeName)
        {
            OnSaveSuccessCallCount++;
        }

        public void OnSaveFailed(string key, string typeName, string errorMessage)
        {
            OnSaveFailedCallCount++;
            FailedCalls.Add((key, typeName, errorMessage));
        }

        public void OnLoadSuccess(string key, string typeName)
        {
            OnLoadSuccessCallCount++;
        }

        public void OnLoadFailed(string key, string typeName, string errorMessage)
        {
            OnLoadFailedCallCount++;
            FailedCalls.Add((key, typeName, errorMessage));
        }

        public void Reset()
        {
            OnSaveSuccessCallCount = 0;
            OnSaveFailedCallCount = 0;
            OnLoadSuccessCallCount = 0;
            OnLoadFailedCallCount = 0;
            FailedCalls.Clear();
        }
    }

    #endregion

    #region Unit Tests for SaveService

    /// <summary>
    /// Unit tests for SaveService core pipeline.
    /// </summary>
    [TestFixture]
    public class SaveServiceTests
    {
        private InMemorySaveBackend _backend;
        private IEncryptor _encryptor;
        private DeterministicSerializer _serializer;
        private SpySaveEventPublisher _eventPublisher;
        private SaveService _saveService;

        [SetUp]
        public void SetUp()
        {
            _backend = new InMemorySaveBackend();
            _encryptor = new PassThroughEncryptor();
            _serializer = new DeterministicSerializer();
            _eventPublisher = new SpySaveEventPublisher();
            _saveService = new SaveService(_backend, _serializer, _encryptor, eventPublisher: _eventPublisher);
        }

        [TearDown]
        public void TearDown()
        {
            _backend.Clear();
        }

        [Test]
        public async Task SaveAsync_HappyPath_CallsOnSaveSuccess()
        {
            // Arrange
            var data = new TestData { Value = 42 };

            // Act
            await _saveService.SaveAsync("key1", data);

            // Assert
            Assert.AreEqual(1, _eventPublisher.OnSaveSuccessCallCount, "OnSaveSuccess should be called once");
            Assert.AreEqual(0, _eventPublisher.OnSaveFailedCallCount, "OnSaveFailed should not be called");
        }

        [Test]
        public async Task SaveAsync_EncryptedData_IsNotPlainJson()
        {
            // Arrange
            var data = new TestData { Value = 42 };
            _encryptor = new XorEncryptor(); // Use real encryptor

            // Use real serializer for this test
            var realSerializer = new NewtonsoftJsonSerializer();
            _saveService = new SaveService(_backend, realSerializer, _encryptor, eventPublisher: _eventPublisher);

            // Act
            await _saveService.SaveAsync("key1", data);

            // Assert - Verify that stored data is not equal to plain JSON
            var stored = await _backend.LoadRawAsync("key1");
            var plainJson = realSerializer.Serialize(data);
            Assert.AreNotEqual(plainJson, stored, "Stored data should be encrypted, not plain JSON");
        }

        [Test]
        public async Task LoadAsync_HappyPath_ReturnsData()
        {
            // Arrange
            var originalData = new TestData { Value = 123 };
            await _saveService.SaveAsync("key1", originalData);

            // Reset event counts
            _eventPublisher.Reset();

            // Act
            var result = await _saveService.LoadAsync("key1", new TestData());

            // Assert
            Assert.AreEqual(123, result.Value, "Loaded data should match saved data");
            Assert.AreEqual(1, _eventPublisher.OnLoadSuccessCallCount, "OnLoadSuccess should be called once");
        }

        [Test]
        public async Task LoadAsync_KeyNotFound_ReturnsDefault()
        {
            // Act
            var result = await _saveService.LoadAsync("missing_key", new TestData { Value = -1 });

            // Assert
            Assert.AreEqual(-1, result.Value, "Should return default value");
            Assert.AreEqual(1, _eventPublisher.OnLoadFailedCallCount, "OnLoadFailed should be called once");
        }

        [Test]
        public async Task LoadAsync_DecryptFails_ReturnsDefault()
        {
            // Arrange
            _saveService = new SaveService(_backend, _serializer, new FailingEncryptor(), eventPublisher: _eventPublisher);
            await _backend.SaveRawAsync("bad_key", "some_data");

            // Act
            var result = await _saveService.LoadAsync("bad_key", new TestData { Value = -2 });

            // Assert
            Assert.AreEqual(-2, result.Value, "Should return default value on decrypt failure");
            Assert.AreEqual(1, _eventPublisher.OnLoadFailedCallCount, "OnLoadFailed should be called once");
        }

        [Test]
        public async Task ExistsAsync_ReturnsTrue_WhenKeyExists()
        {
            // Arrange
            await _saveService.SaveAsync("exists_key", new TestData());

            // Act
            var exists = await _saveService.ExistsAsync("exists_key");

            // Assert
            Assert.That(exists, Is.True, "ExistsAsync should return true for existing key");
        }

        [Test]
        public async Task DeleteAsync_RemovesKey()
        {
            // Arrange
            await _saveService.SaveAsync("to_delete", new TestData());
            Assert.That(await _saveService.ExistsAsync("to_delete"), Is.True);

            // Act
            await _saveService.DeleteAsync("to_delete");

            // Assert
            Assert.That(await _saveService.ExistsAsync("to_delete"), Is.False, "Key should be deleted");
        }
    }

    #endregion

    #region Test Data

    [Serializable]
    public class TestData
    {
        public int Value;
    }

    #endregion
}
