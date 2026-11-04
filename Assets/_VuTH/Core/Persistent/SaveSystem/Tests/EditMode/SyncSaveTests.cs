using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using _VuTH.Core.Persistent.SaveSystem.Backend;
using _VuTH.Core.Persistent.SaveSystem.Encrypt;
using _VuTH.Core.Persistent.SaveSystem.Serialize;
using _VuTH.Core.Persistent.SaveSystem.Tests.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace _VuTH.Core.Persistent.SaveSystem.Tests.EditMode
{
    /// <summary>
    /// Synchronous save path used on app pause / quit (ISyncSaveBackend + ISyncSaveService).
    /// </summary>
    [TestFixture]
    public class SyncSaveTests
    {
        [Serializable]
        public class Dto
        {
            public string Name = "";
            public int Level;
        }

        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"VuTH_SyncSaveTest_{Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        [Test]
        public async Task JsonFileBackend_SaveRaw_CreatesDirectory_AndIsReadableAsync()
        {
            var backend = new JsonFileSaveBackend(_tempDir);

            backend.SaveRaw("slot", "{\"a\":1}");

            Assert.AreEqual("{\"a\":1}", await backend.LoadRawAsync("slot", CancellationToken.None));
        }

        [Test]
        public async Task JsonFileBackend_SaveRaw_OverwritesExisting_AndLeavesNoTempFile()
        {
            var backend = new JsonFileSaveBackend(_tempDir);
            backend.SaveRaw("slot", "old");

            backend.SaveRaw("slot", "new");

            Assert.AreEqual("new", await backend.LoadRawAsync("slot", CancellationToken.None));
            CollectionAssert.IsEmpty(Directory.GetFiles(_tempDir, "*.tmp"));
        }

        [Test]
        public void JsonFileBackend_And_PlayerPrefsBackend_AdvertiseSyncCapability()
        {
            Assert.IsInstanceOf<ISyncSaveBackend>(new JsonFileSaveBackend(_tempDir));
            Assert.IsInstanceOf<ISyncSaveBackend>(new PlayerPrefsSaveBackend());
        }

        [Test]
        public async Task SaveService_TrySaveImmediate_WithSyncBackend_RoundTripsThroughLoadAsync()
        {
            var publisher = new SpySaveEventPublisher();
            var service = new SaveService(new JsonFileSaveBackend(_tempDir), new JsonSerializer(), new NoOpEncryptor(),
                eventPublisher: publisher);

            var saved = service.TrySaveImmediate("profile", new Dto { Name = "Vu", Level = 7 });
            var loaded = await service.LoadAsync("profile", new Dto());

            Assert.IsTrue(saved);
            Assert.AreEqual("Vu", loaded.Name);
            Assert.AreEqual(7, loaded.Level);
            Assert.AreEqual(1, publisher.OnSaveSuccessCallCount);
        }

        [Test]
        public async Task SaveService_TrySaveImmediate_WithAsyncOnlyBackend_ReturnsFalse_AndWritesNothing()
        {
            var backend = new InMemorySaveBackend();
            var service = new SaveService(backend, new JsonSerializer(), new NoOpEncryptor());

            var saved = service.TrySaveImmediate("profile", new Dto { Name = "x" });

            Assert.IsFalse(saved);
            Assert.IsFalse(await backend.ExistsAsync("profile"));
        }

        [Test]
        public void SaveService_TrySaveImmediate_WhenEncryptFails_PublishesFailure_AndRethrows()
        {
            var publisher = new SpySaveEventPublisher();
            var service = new SaveService(new JsonFileSaveBackend(_tempDir), new JsonSerializer(), new FailingEncryptor(),
                eventPublisher: publisher);

            Assert.Throws<InvalidOperationException>(() => service.TrySaveImmediate("profile", new Dto()));
            Assert.AreEqual(1, publisher.OnSaveFailedCallCount);
            Assert.AreEqual(0, publisher.OnSaveSuccessCallCount);
        }
    }
}
