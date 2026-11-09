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

        [Test]
        public void BackupNamespace_IsRejectedByEveryApplicationEntryPoint()
        {
            var service = new SaveService(new JsonFileSaveBackend(_tempDir), new JsonSerializer(), new NoOpEncryptor());
            var reserved = SaveService.GetCorruptedBackupKey("slot");
            Assert.Throws<ArgumentException>(() => service.TrySaveImmediate(reserved, new Dto()));
            Assert.ThrowsAsync<ArgumentException>(async () => await service.SaveAsync(reserved, new Dto()));
            Assert.ThrowsAsync<ArgumentException>(async () => await service.TryLoadAsync<Dto>(reserved));
            Assert.Throws<ArgumentException>(() => service.ExistsAsync(reserved));
            Assert.ThrowsAsync<ArgumentException>(async () => await service.DeleteAsync(reserved));
            Assert.IsFalse(Directory.Exists(_tempDir));
        }

        [Test]
        public async Task DictionaryKeys_RoundTripWithoutChangingCaseOrLosingEntries()
        {
            var service = new SaveService(new JsonFileSaveBackend(_tempDir), new NewtonsoftJsonSerializer(), new NoOpEncryptor());
            var data = new System.Collections.Generic.Dictionary<string, int> { ["Gold"] = 1, ["gold"] = 2 };
            await service.SaveAsync("dictionary", data);
            var result = await service.TryLoadAsync<System.Collections.Generic.Dictionary<string, int>>("dictionary");
            CollectionAssert.AreEquivalent(data, result.Data);
        }

        [Test]
        public async Task NullJsonPayload_IsCorruptedAndBackedUp()
        {
            var backend = new JsonFileSaveBackend(_tempDir);
            backend.SaveRaw("slot", "{\"schemaVersion\":1,\"payload\":\"null\"}");
            var service = new SaveService(backend, new JsonSerializer(), new NoOpEncryptor());
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("Corrupted"));
            var result = await service.TryLoadAsync<Dto>("slot");
            Assert.AreEqual(SaveLoadStatus.Corrupted, result.Status);
            Assert.IsTrue(result.CanOverwrite);
        }

        [Test]
        public async Task CorruptBackupAndDelete_PreserveApplicationSuffixKey()
        {
            var backend = new JsonFileSaveBackend(_tempDir);
            var service = new SaveService(backend, new NewtonsoftJsonSerializer(), new NoOpEncryptor());
            await service.SaveAsync("slot.corrupt", new Dto { Name = "application-data" });
            backend.SaveRaw("slot", "damaged");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("Corrupted"));
            var result = await service.TryLoadAsync<Dto>("slot");
            Assert.IsTrue(result.CanOverwrite);
            Assert.AreEqual("application-data", (await service.TryLoadAsync<Dto>("slot.corrupt")).Data.Name);
            await service.DeleteAsync("slot");
            Assert.AreEqual("application-data", (await service.TryLoadAsync<Dto>("slot.corrupt")).Data.Name);
        }

        private sealed class BlockingEncryptor : IThreadSafeEncryptor
        {
            public readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>();
            public readonly ManualResetEventSlim Release = new ManualResetEventSlim();
            private int _calls;

            public string Encrypt(string value)
            {
                if (Interlocked.Increment(ref _calls) == 1)
                {
                    Started.TrySetResult(true);
                    if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                }
                return value;
            }

            public string Decrypt(string value) => value;
        }

        [Test]
        public async Task SaveService_SlowEncryptionCannotOverwriteImmediateSave()
        {
            var backend = new JsonFileSaveBackend(_tempDir);
            var encryptor = new BlockingEncryptor();
            var service = new SaveService(backend, new JsonSerializer(), encryptor)
                { ThreadPoolEncryptionThreshold = 0 };
            var older = service.SaveAsync("slot", new Dto { Name = "old" });
            try
            {
                Assert.AreSame(encryptor.Started.Task,
                    await Task.WhenAny(encryptor.Started.Task, Task.Delay(5000)));
                Assert.IsTrue(service.TrySaveImmediate("slot", new Dto { Name = "new" }));
            }
            finally
            {
                encryptor.Release.Set();
                await older;
            }
            Assert.AreEqual("new", (await service.LoadAsync("slot", new Dto())).Name);
        }

        [Test]
        public void SaveService_NullPayloadIsRejectedBeforeWriting()
        {
            var backend = new JsonFileSaveBackend(_tempDir);
            var service = new SaveService(backend, new NewtonsoftJsonSerializer(), new NoOpEncryptor());
            Assert.Throws<ArgumentNullException>(() => service.TrySaveImmediate<Dto>("slot", null));
            Assert.ThrowsAsync<ArgumentNullException>(async () => await service.SaveAsync<Dto>("slot", null));
            Assert.IsFalse(Directory.Exists(_tempDir));
        }

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
