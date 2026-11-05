using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using _VuTH.Core.Persistent.SaveSystem;
using _VuTH.Core.Persistent.SaveSystem.Migrate;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Persistent.DataPackage.Tests.EditMode
{
    /// <summary>
    /// Per-package schemas, serialized load / save, delete and reset, save slots, and value-aware dirty marking.
    /// </summary>
    [TestFixture]
    public class PersistencePackageFeatureTests
    {
        public class Dto
        {
            public int Value = 1;
        }

        public class Package : PersistencePackage<Dto>
        {
            public int Value = 1;
            public SaveSchema Schema;
            public bool SlotScoped;

            public Package(string key = "pkg") : base(key, SaveStrategy.ManualOnly)
            {
            }

            public override bool IsSlotScoped => SlotScoped;

            protected override SaveSchema CreateSchema() => Schema;

            public void Set(int value)
            {
                Value = value;
                MarkDirty();
            }

            public override Dto ExtractPayload() => new() { Value = Value };
            public override void InjectPayload(Dto data) => Value = data.Value;
        }

        /// <summary>Stores values per key, records every call, and can hold calls at a gate.</summary>
        private sealed class RecordingService : ISaveService, IVersionedSaveService
        {
            public readonly Dictionary<string, int> Stored = new();
            public readonly List<string> Calls = new();
            public UniTaskCompletionSource SaveGate;
            public int? LastSchemaVersion;
            private int _running;
            public int MaxConcurrent;

            private async UniTask Enter(string call, UniTaskCompletionSource gate)
            {
                Calls.Add(call);
                _running++;
                MaxConcurrent = Math.Max(MaxConcurrent, _running);
                if (gate != null) await gate.Task;
                _running--;
            }

            public async UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default)
            {
                await Enter("save " + key, SaveGate);
                Stored[key] = ((Dto)(object)data).Value;
            }

            public async UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, CancellationToken cancellationToken = default)
            {
                await Enter("load " + key, null);
                return Stored.TryGetValue(key, out var value)
                    ? SaveLoadResult<T>.Success((T)(object)new Dto { Value = value })
                    : SaveLoadResult<T>.NotFound();
            }

            public UniTask SaveAsync<T>(string key, T data, SaveSchema schema, CancellationToken cancellationToken = default)
            {
                LastSchemaVersion = schema.Version;
                return SaveAsync(key, data, cancellationToken);
            }

            public UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, SaveSchema schema, CancellationToken cancellationToken = default)
            {
                LastSchemaVersion = schema.Version;
                return TryLoadAsync<T>(key, cancellationToken);
            }

            public bool TrySaveImmediate<T>(string key, T data, SaveSchema schema) => false;

            public async UniTask<T> LoadAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
            {
                var result = await TryLoadAsync<T>(key, cancellationToken);
                return result.IsLoaded ? result.Data : defaultValue;
            }

            public UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
                UniTask.FromResult(Stored.ContainsKey(key));

            public UniTask DeleteAsync(string key, CancellationToken cancellationToken = default)
            {
                Calls.Add("delete " + key);
                Stored.Remove(key);
                return UniTask.CompletedTask;
            }
        }

        private RecordingService _service;
        private DataPersistenceManager _manager;

        [SetUp]
        public void SetUp() => _service = new RecordingService();

        [TearDown]
        public void TearDown()
        {
            if (_manager) Object.DestroyImmediate(_manager.gameObject);
        }

        private Package Create(string key = "pkg", bool slotScoped = false)
        {
            var package = new Package(key) { SlotScoped = slotScoped };
            package.SetSaveService(_service);
            return package;
        }

        // ---- Schema ----

        [Test]
        public async Task OwnSchema_IsUsedForLoadAndSave()
        {
            var package = Create();
            package.Schema = new SaveSchema(4);

            await package.LoadAsync();
            Assert.AreEqual(4, _service.LastSchemaVersion);

            _service.LastSchemaVersion = null;
            package.Set(2);
            await package.SaveNowAsync();
            Assert.AreEqual(4, _service.LastSchemaVersion);
        }

        [Test]
        public async Task WithoutSchema_UsesTheServiceWideVersion()
        {
            var package = Create();
            package.Set(2);
            await package.SaveNowAsync();

            Assert.IsNull(_service.LastSchemaVersion);
        }

        // ---- Loading ----

        [Test]
        public void LoadWithoutSaveService_ReleasesWaitersWithAnError()
        {
            // Used to leave WaitUntilLoadedAsync pending forever.
            var package = new Package();
            LogAssert.Expect(LogType.Error, new Regex("no save service"));

            package.LoadAsync().Forget();

            Assert.Throws<InvalidOperationException>(() => package.WaitUntilLoadedAsync().GetAwaiter().GetResult());
        }

        [Test]
        public async Task LoadAfterAFailedAttempt_CompletesTheNewWait()
        {
            var package = new Package();
            LogAssert.Expect(LogType.Error, new Regex("no save service"));
            await package.LoadAsync();

            package.SetSaveService(_service);
            await package.LoadAsync();

            Assert.AreEqual(UniTaskStatus.Succeeded, package.WaitUntilLoadedAsync().Status);
        }

        [Test]
        public async Task Load_WaitsForTheSaveInProgress()
        {
            // A load reading while a save is still being written could read the old data.
            var package = Create();
            _service.SaveGate = new UniTaskCompletionSource();
            package.Set(5);

            var saving = package.SaveNowAsync();
            var loading = package.LoadAsync();
            CollectionAssert.AreEqual(new[] { "save pkg" }, _service.Calls, "the load waits");

            _service.SaveGate.TrySetResult();
            await saving;
            await loading;

            CollectionAssert.AreEqual(new[] { "save pkg", "load pkg" }, _service.Calls);
            Assert.AreEqual(5, package.Value);
            Assert.AreEqual(1, _service.MaxConcurrent);
        }

        // ---- Delete ----

        [Test]
        public async Task Delete_RemovesTheData_AndResetsToDefaults()
        {
            var package = Create();
            package.Set(9);
            await package.SaveNowAsync();

            await package.DeleteAsync();

            Assert.IsFalse(_service.Stored.ContainsKey("pkg"));
            Assert.AreEqual(1, package.Value, "the DTO's default");
            Assert.IsFalse(package.IsDirty);
            Assert.IsTrue(package.CanSave);
        }

        // ---- Slots ----

        private DataPersistenceManager CreateManager()
        {
            _manager = new GameObject("DataPersistenceManager").AddComponent<DataPersistenceManager>();
            return _manager;
        }

        [Test]
        public async Task SwitchSlot_SavesThenLoadsSlotScopedPackagesOnly()
        {
            var manager = CreateManager();
            var progress = Create("progress", slotScoped: true);
            var settings = Create("settings");
            manager.RegisterPackage(progress);
            manager.RegisterPackage(settings);
            progress.Set(3);

            await manager.SwitchSlotAsync("2");

            Assert.AreEqual("2", manager.ActiveSlot);
            Assert.AreEqual("slot-2/progress", progress.StorageKey);
            Assert.AreEqual("settings", settings.StorageKey);
            CollectionAssert.AreEqual(new[] { "save progress", "load slot-2/progress" }, _service.Calls);
            Assert.AreEqual(3, _service.Stored["progress"], "the change stays in the slot it was made in");
            Assert.AreEqual(1, progress.Value, "nothing in the new slot: it starts from defaults");

            progress.Set(8);
            await progress.SaveNowAsync();
            await manager.SwitchSlotAsync(null);

            Assert.AreEqual("progress", progress.StorageKey);
            Assert.AreEqual(3, progress.Value);
            Assert.AreEqual(8, _service.Stored["slot-2/progress"]);
        }

        [Test]
        public void RegisteredPackage_JoinsTheActiveSlot()
        {
            var manager = CreateManager();
            manager.SwitchSlotAsync("a").GetAwaiter().GetResult();

            var package = Create("progress", slotScoped: true);
            manager.RegisterPackage(package);

            Assert.AreEqual("slot-a/progress", package.StorageKey);
        }

        [Test]
        public void SetSlot_OnASharedPackage_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => Create().SetSlot("1"));
        }

        // ---- PersistentField ----

        [Test]
        public void PersistentField_MarksDirtyOnlyWhenTheValueChanges()
        {
            var package = Create();
            var field = new PersistentField<int>(package, 4);

            field.Value = 4;
            Assert.IsFalse(package.IsDirty, "same value");

            field.Value = 5;
            Assert.IsTrue(package.IsDirty);
        }

        [Test]
        public void PersistentField_NotifyChanged_MarksDirty_ForInPlaceChanges()
        {
            var package = Create();
            var field = new PersistentField<List<int>>(package, new List<int>());
            var notified = 0;
            using var subscription = field.Subscribe(_ => notified++);

            field.Value.Add(1);
            field.NotifyChanged();

            Assert.IsTrue(package.IsDirty);
            Assert.AreEqual(2, notified, "initial value, then the change");
        }
    }
}
