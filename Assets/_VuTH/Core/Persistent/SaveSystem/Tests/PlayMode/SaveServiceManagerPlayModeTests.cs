using System;
using System.Collections;
using System.IO;
using _VuTH.Core.Persistent.SaveSystem.Backend;
using _VuTH.Core.Persistent.SaveSystem.Encrypt;
using _VuTH.Core.Persistent.SaveSystem.Profile;
using _VuTH.Core.Persistent.SaveSystem.Serialize;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Persistent.SaveSystem.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for SaveServiceManager lifecycle and configuration.
    /// </summary>
    public class SaveServiceManagerPlayModeTests
    {
        private SaveServiceManager _manager;
        private string _tempDir;

        [UnityTest]
        public IEnumerator PlayerPrefs_OlderReservedWriteCannotReplaceImmediateSave() => UniTask.ToCoroutine(async () =>
        {
            var key = "VuTH_Ordering_" + Guid.NewGuid().ToString("N");
            var backend = new PlayerPrefsSaveBackend();
            try
            {
                var olderTicket = backend.ReserveWrite();
                backend.SaveRaw(key, "new");
                await backend.SaveRawOrderedAsync(key, "old", olderTicket);
                Assert.AreEqual("new", await backend.LoadRawAsync(key));

                olderTicket = backend.ReserveWrite();
                await backend.DeleteAsync(key);
                await backend.SaveRawOrderedAsync(key, "resurrected", olderTicket);
                Assert.IsFalse(await backend.ExistsAsync(key));
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        });

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"VuTH_SaveManagerTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
            {
                Object.Destroy(_manager.gameObject);
                _manager = null;
            }

            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        /// <summary>Awake initializes the manager; WithBackend then rebuilds the service on the temp folder.</summary>
        private SaveServiceManager CreateManager()
        {
            _manager = new GameObject("SaveServiceManager").AddComponent<SaveServiceManager>();
            _manager.WithBackend(new JsonFileSaveBackend(_tempDir));
            return _manager;
        }

        [UnityTest]
        public IEnumerator Initialize_WithDefaultConfig_CreatesService()
        {
            CreateManager();
            yield return null;

            Assert.IsTrue(_manager.IsInitialized, "The service is built in Awake");
            Assert.AreEqual(SaveService.DefaultSchemaVersion, _manager.GetSchemaVersion());
        }

        [UnityTest]
        public IEnumerator SaveAndLoad_WithProfile_ChecksRoundTrip()
        {
            CreateManager();
            yield return null;

            var testData = new TestPlayData { Score = 100, Name = "Player1" };
            yield return _manager.SaveAsync("test_key", testData).ToCoroutine();

            var loaded = default(TestPlayData);
            yield return _manager.LoadAsync("test_key", new TestPlayData())
                .ContinueWith(result => loaded = result).ToCoroutine();

            Assert.AreEqual(100, loaded.Score, "Score should match");
            Assert.AreEqual("Player1", loaded.Name, "Name should match");
        }

        [UnityTest]
        public IEnumerator Load_MissingKey_ReturnsDefault()
        {
            CreateManager();
            yield return null;

            var defaultData = new TestPlayData { Score = -1, Name = "Default" };
            var loaded = default(TestPlayData);
            yield return _manager.LoadAsync("missing_key", defaultData)
                .ContinueWith(result => loaded = result).ToCoroutine();

            Assert.AreEqual(-1, loaded.Score, "Should return default score");
            Assert.AreEqual("Default", loaded.Name, "Should return default name");
        }

        [Test]
        public void ShippedProfile_DeserializesItsAdapters_AndTheyWork()
        {
            // The asset used to be in an older format: every adapter came back null and the defaults were used instead.
            var profile = Resources.Load<SaveServiceAdapterProfile>(SaveServiceAdapterProfile.ResourcePath);

            Assert.IsNotNull(profile, "Resources/SaveServiceAdapterProfile");
            Assert.IsInstanceOf<XorEncryptor>(profile.Encryptor);
            Assert.IsInstanceOf<NewtonsoftJsonSerializer>(profile.Serializer);
            Assert.IsInstanceOf<PlayerPrefsSaveBackend>(profile.Backend);
            Assert.AreEqual(SaveService.DefaultSchemaVersion, profile.SchemaVersion);

            // SerializeReference instances must be fully constructed (e.g. the XOR key).
            Assert.AreEqual("hello", profile.Encryptor.Decrypt(profile.Encryptor.Encrypt("hello")));
        }

        [UnityTest]
        public IEnumerator WithEncryptor_AfterInitialization_IsApplied()
        {
            // With* calls were overwritten by the profile when the service was built.
            CreateManager();
            _manager.WithEncryptor(new NoOpEncryptor());
            yield return null;

            yield return _manager.SaveAsync("plain", new TestPlayData { Score = 7 }).ToCoroutine();

            string stored = null;
            yield return new JsonFileSaveBackend(_tempDir).LoadRawAsync("plain")
                .ContinueWith(raw => stored = raw).ToCoroutine();
            StringAssert.Contains("schemaVersion", stored, "NoOpEncryptor stores the JSON as is");
        }

        [UnityTest]
        public IEnumerator WithSchemaVersion_NewerSave_IsNotLoaded()
        {
            CreateManager().WithSchemaVersion(3);
            yield return _manager.SaveAsync("versioned", new TestPlayData { Score = 1 }).ToCoroutine();

            _manager.WithSchemaVersion(2); // e.g. the game was downgraded
            var result = default(SaveLoadResult<TestPlayData>);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("UnsupportedVersion"));
            yield return _manager.TryLoadAsync<TestPlayData>("versioned")
                .ContinueWith(r => result = r).ToCoroutine();

            Assert.AreEqual(SaveLoadStatus.UnsupportedVersion, result.Status);
            Assert.IsFalse(result.CanOverwrite);
        }
    }

    #region Test Fixtures

    [Serializable]
    public class TestPlayData
    {
        public int Score;
        public string Name;
    }

    #endregion
}
