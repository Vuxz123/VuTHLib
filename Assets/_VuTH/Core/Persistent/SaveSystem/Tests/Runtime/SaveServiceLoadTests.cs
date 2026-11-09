using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using _VuTH.Core.Persistent.SaveSystem.Backend;
using _VuTH.Core.Persistent.SaveSystem.Encrypt;
using _VuTH.Core.Persistent.SaveSystem.Migrate;
using _VuTH.Core.Persistent.SaveSystem.Serialize;
using NUnit.Framework;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace _VuTH.Core.Persistent.SaveSystem.Tests.Runtime
{
    /// <summary>
    /// Loading reports what happened and never lets damaged, unreadable or newer saves be silently replaced.
    /// </summary>
    [TestFixture]
    public class SaveServiceLoadTests
    {
        private InMemorySaveBackend _backend;
        private NewtonsoftJsonSerializer _serializer;
        private SpySaveEventPublisher _events;

        [SetUp]
        public void SetUp()
        {
            _backend = new InMemorySaveBackend();
            _serializer = new NewtonsoftJsonSerializer();
            _events = new SpySaveEventPublisher();
        }

        private SaveService CreateService(int schemaVersion = 1, SaveMigrationChain chain = null, IEncryptor encryptor = null) =>
            new(_backend, _serializer, encryptor ?? new XorEncryptor(), schemaVersion, chain, _events);

        private static void ExpectLoadError(string status) =>
            LogAssert.Expect(LogType.Error, new Regex(status));

        private sealed class CancelBackupBackend : ISaveBackend
        {
            private readonly InMemorySaveBackend _storage;
            private readonly CancellationTokenSource _cancellation;

            public CancelBackupBackend(InMemorySaveBackend storage, CancellationTokenSource cancellation)
            {
                _storage = storage;
                _cancellation = cancellation;
            }

            public UniTask SaveRawAsync(string key, string data, CancellationToken cancellationToken = default)
            {
                // The read and decode have already completed; cancel exactly when the backup starts.
                _cancellation.Cancel();
                return UniTask.FromCanceled(cancellationToken);
            }

            public UniTask<string> LoadRawAsync(string key, CancellationToken cancellationToken = default) =>
                _storage.LoadRawAsync(key, cancellationToken);
            public UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
                _storage.ExistsAsync(key, cancellationToken);
            public UniTask DeleteAsync(string key, CancellationToken cancellationToken = default) =>
                _storage.DeleteAsync(key, cancellationToken);
        }

        [TestCase("garbage")]
        [TestCase("{\"schemaVersion\":1,\"payload\":\"not-json\"}")]
        public async Task TryLoad_CancelledDuringBackup_PropagatesCancellation(string raw)
        {
            await _backend.SaveRawAsync("k", raw);
            using var cancellation = new CancellationTokenSource();
            var service = new SaveService(new CancelBackupBackend(_backend, cancellation), _serializer,
                new NoOpEncryptor(), eventPublisher: _events);

            var error = Assert.CatchAsync<OperationCanceledException>(async () =>
                await service.TryLoadAsync<TestData>("k", cancellation.Token));

            Assert.AreEqual(cancellation.Token, error.CancellationToken);
            Assert.AreEqual(raw, _backend.Raw("k"));
            Assert.IsNull(_backend.Raw(SaveService.GetCorruptedBackupKey("k")));
            Assert.AreEqual(0, _events.OnLoadFailedCallCount);
            Assert.AreEqual(0, _events.OnLoadSuccessCallCount);
        }

        [Test]
        public async Task TryLoad_Saved_IsLoaded()
        {
            var service = CreateService();
            await service.SaveAsync("k", new TestData { Value = 5 });

            var result = await service.TryLoadAsync<TestData>("k");

            Assert.AreEqual(SaveLoadStatus.Loaded, result.Status);
            Assert.AreEqual(5, result.Data.Value);
            Assert.IsTrue(result.CanOverwrite);
        }

        [Test]
        public async Task TryLoad_Missing_IsNotFound_AndOverwritable()
        {
            var result = await CreateService().TryLoadAsync<TestData>("missing");

            Assert.AreEqual(SaveLoadStatus.NotFound, result.Status);
            Assert.IsTrue(result.CanOverwrite);
        }

        [Test]
        public async Task TryLoad_Garbage_IsCorrupted_AndBackedUpBeforeAllowingOverwrite()
        {
            await _backend.SaveRawAsync("k", "not base64 at all!");
            ExpectLoadError("Corrupted");

            var result = await CreateService().TryLoadAsync<TestData>("k");

            Assert.AreEqual(SaveLoadStatus.Corrupted, result.Status);
            Assert.IsTrue(result.CanOverwrite);
            Assert.AreEqual("not base64 at all!", _backend.Raw(SaveService.GetCorruptedBackupKey("k")));
            Assert.AreEqual(1, _events.OnLoadFailedCallCount);
        }

        [Test]
        public async Task TryLoad_Corrupted_WhenBackupFails_IsNotOverwritable()
        {
            await _backend.SaveRawAsync("k", "garbage");
            _backend.WriteError = new IOException("disk full");
            ExpectLoadError("Corrupted");

            var result = await CreateService().TryLoadAsync<TestData>("k");

            Assert.AreEqual(SaveLoadStatus.Corrupted, result.Status);
            Assert.IsFalse(result.CanOverwrite, "the damaged data is the only copy");
        }

        [Test]
        public async Task TryLoad_WrapperWithoutPayload_IsCorrupted()
        {
            var noop = new NoOpEncryptor();
            await _backend.SaveRawAsync("k", "{}");
            ExpectLoadError("Corrupted");

            var result = await CreateService(encryptor: noop).TryLoadAsync<TestData>("k");

            Assert.AreEqual(SaveLoadStatus.Corrupted, result.Status);
        }

        [Test]
        public async Task TryLoad_ReadError_IsReadFailed_AndNotOverwritable()
        {
            // Used to be swallowed into "not found" → defaults → the next save replaced the real data.
            await _backend.SaveRawAsync("k", "data");
            _backend.ReadError = new IOException("sharing violation");
            ExpectLoadError("ReadFailed");

            var result = await CreateService().TryLoadAsync<TestData>("k");

            Assert.AreEqual(SaveLoadStatus.ReadFailed, result.Status);
            Assert.IsFalse(result.CanOverwrite);
            Assert.IsNull(_backend.Raw(SaveService.GetCorruptedBackupKey("k")));
        }

        [Test]
        public async Task TryLoad_NewerSchema_IsUnsupported_AndNotOverwritable()
        {
            await CreateService(schemaVersion: 3).SaveAsync("k", new TestData { Value = 1 });
            ExpectLoadError("UnsupportedVersion");

            var result = await CreateService(schemaVersion: 2).TryLoadAsync<TestData>("k");

            Assert.AreEqual(SaveLoadStatus.UnsupportedVersion, result.Status);
            Assert.IsFalse(result.CanOverwrite);
        }

        [Test]
        public async Task TryLoad_MissingMigrator_IsMigrationFailed()
        {
            await CreateService(schemaVersion: 1).SaveAsync("k", new TestData { Value = 1 });
            ExpectLoadError("MigrationFailed");

            var result = await CreateService(schemaVersion: 2).TryLoadAsync<TestData>("k");

            Assert.AreEqual(SaveLoadStatus.MigrationFailed, result.Status);
            Assert.IsFalse(result.CanOverwrite);
        }

        [Test]
        public async Task TryLoad_MigratedPayloadUnreadable_IsMigrationFailed_NotCorrupted()
        {
            await CreateService(schemaVersion: 1).SaveAsync("k", new TestData { Value = 1 });
            var chain = new SaveMigrationChain();
            chain.AddMigrator(new VersionMigrator(1, 2, _serializer)); // returns a non-JSON payload
            ExpectLoadError("MigrationFailed");

            var result = await CreateService(schemaVersion: 2, chain: chain).TryLoadAsync<TestData>("k");

            Assert.AreEqual(SaveLoadStatus.MigrationFailed, result.Status);
            Assert.IsNull(_backend.Raw(SaveService.GetCorruptedBackupKey("k")), "not treated as damaged data");
        }

        [Test]
        public async Task TryLoad_WithMigrator_Loads()
        {
            await CreateService(schemaVersion: 1).SaveAsync("k", new TestData { Value = 9 });
            var chain = new SaveMigrationChain();
            chain.AddMigrator(new DefaultSaveMigrator(1, 2));

            var result = await CreateService(schemaVersion: 2, chain: chain).TryLoadAsync<TestData>("k");

            Assert.AreEqual(SaveLoadStatus.Loaded, result.Status);
            Assert.AreEqual(9, result.Data.Value);
        }

        [Test]
        public async Task LoadAsync_Failure_ReturnsDefault()
        {
            await _backend.SaveRawAsync("k", "garbage");
            ExpectLoadError("Corrupted");

            var value = await CreateService().LoadAsync("k", new TestData { Value = -1 });

            Assert.AreEqual(-1, value.Value);
        }

        [Test]
        public async Task SaveAsync_ThrowingSubscriber_DoesNotReportAFailedSave()
        {
            var service = new SaveService(_backend, _serializer, new XorEncryptor(),
                eventPublisher: new ThrowingSaveEventPublisher());
            LogAssert.Expect(LogType.Error, new Regex("subscriber threw"));

            await service.SaveAsync("k", new TestData { Value = 1 }); // must not throw

            Assert.IsNotNull(_backend.Raw("k"));
        }

        private sealed class ThrowingSaveEventPublisher : Events.ISaveEventPublisher
        {
            public void OnSaveSuccess(string key, string dataType) => throw new InvalidOperationException("boom");
            public void OnSaveFailed(string key, string dataType, string error) => throw new InvalidOperationException("boom");
            public void OnLoadSuccess(string key, string dataType) => throw new InvalidOperationException("boom");
            public void OnLoadFailed(string key, string dataType, string error) => throw new InvalidOperationException("boom");
        }
    }
}
