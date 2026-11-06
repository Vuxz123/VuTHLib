using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using _VuTH.Core.Persistent.SaveSystem;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Persistent.DataPackage.Tests.EditMode
{
    /// <summary>
    /// Packages never overwrite stored data they could not load, expose when loading finished, and the manager finds
    /// packages by base type, rejects shared storage keys and does not reload over unsaved changes.
    /// </summary>
    [TestFixture]
    public class PersistenceLoadSafetyTests
    {
        public class Dto
        {
            public int Value;
        }

        public class BasePackage : PersistencePackage<Dto>
        {
            public int Value;

            public BasePackage(string key = "pkg") : base(key, SaveStrategy.ManualOnly)
            {
            }

            public void Set(int value)
            {
                Value = value;
                MarkDirty();
            }

            public override Dto ExtractPayload() => new() { Value = Value };

            public override void InjectPayload(Dto data)
            {
                Value = data.Value;
                MarkDirty(); // a careless implementation: must not make the package dirty during loading
            }
        }

        public sealed class DerivedPackage : BasePackage
        {
        }

        public sealed class OtherPackage : BasePackage
        {
            public OtherPackage(string key) : base(key)
            {
            }
        }

        /// <summary>Returns a scripted load result; records saves.</summary>
        private sealed class ScriptedSaveService : ISaveService, ISyncSaveService
        {
            public SaveLoadStatus Status = SaveLoadStatus.NotFound;
            public bool CanOverwrite = true;
            public int StoredValue;
            public UniTaskCompletionSource Gate;
            public readonly List<int> Saves = new();

            public async UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, CancellationToken cancellationToken = default)
            {
                if (Gate != null) await Gate.Task;

                if (Status == SaveLoadStatus.Loaded)
                {
                    return SaveLoadResult<T>.Success((T)(object)new Dto { Value = StoredValue });
                }

                return Status == SaveLoadStatus.NotFound
                    ? SaveLoadResult<T>.NotFound()
                    : SaveLoadResult<T>.Failure(Status, "scripted", CanOverwrite);
            }

            public async UniTask<T> LoadAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
            {
                var result = await TryLoadAsync<T>(key, cancellationToken);
                return result.IsLoaded ? result.Data : defaultValue;
            }

            public UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default)
            {
                Saves.Add(((Dto)(object)data).Value);
                return UniTask.CompletedTask;
            }

            public bool TrySaveImmediate<T>(string key, T data)
            {
                Saves.Add(((Dto)(object)data).Value);
                return true;
            }

            public UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
                UniTask.FromResult(false);

            public UniTask DeleteAsync(string key, CancellationToken cancellationToken = default) =>
                UniTask.CompletedTask;
        }

        private ScriptedSaveService _service;
        private DataPersistenceManager _manager;

        [SetUp]
        public void SetUp()
        {
            _service = new ScriptedSaveService();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager) Object.DestroyImmediate(_manager.gameObject);
        }

        private BasePackage CreatePackage()
        {
            var package = new BasePackage();
            package.SetSaveService(_service);
            return package;
        }

        // ---- Package ----

        [Test]
        public async Task Load_Success_InjectsData_WithoutMarkingDirty()
        {
            var package = CreatePackage();
            _service.Status = SaveLoadStatus.Loaded;
            _service.StoredValue = 7;

            await package.LoadAsync();

            Assert.AreEqual(7, package.Value);
            Assert.AreEqual(SaveLoadStatus.Loaded, package.LoadStatus);
            Assert.IsTrue(package.CanSave);
            Assert.IsFalse(package.IsDirty);
            Assert.AreEqual(UniTaskStatus.Succeeded, package.WaitUntilLoadedAsync().Status);
        }

        [Test]
        public async Task Load_Unreadable_DisablesSaving_UntilALaterLoadSucceeds()
        {
            // Defaults used to be saved over the stored data after any load failure.
            var package = CreatePackage();
            _service.Status = SaveLoadStatus.ReadFailed;
            _service.CanOverwrite = false;
            LogAssert.Expect(LogType.Error, new Regex("could not be loaded"));

            await package.LoadAsync();
            package.Set(1);
            await package.SaveNowAsync();

            Assert.IsFalse(package.CanSave);
            Assert.IsFalse(package.TrySaveImmediate());
            CollectionAssert.IsEmpty(_service.Saves, "the stored data must not be overwritten");
            Assert.IsTrue(package.IsDirty, "the change is kept in memory");

            _service.Status = SaveLoadStatus.Loaded;
            _service.StoredValue = 5;
            await package.LoadAsync();
            package.Set(6);
            await package.SaveNowAsync();

            Assert.IsTrue(package.CanSave);
            CollectionAssert.AreEqual(new[] { 6 }, _service.Saves);
        }

        [TestCase(SaveLoadStatus.UnsupportedVersion)]
        [TestCase(SaveLoadStatus.MigrationFailed)]
        public async Task Load_NewerOrUnmigratable_DisablesSaving(SaveLoadStatus status)
        {
            var package = CreatePackage();
            _service.Status = status;
            _service.CanOverwrite = false;
            LogAssert.Expect(LogType.Error, new Regex("could not be loaded"));

            await package.LoadAsync();

            Assert.IsFalse(package.CanSave);
        }

        [Test]
        public async Task Load_CorruptedAndBackedUp_KeepsDefaults_AndAllowsSaving()
        {
            var package = CreatePackage();
            _service.Status = SaveLoadStatus.Corrupted;
            _service.CanOverwrite = true;
            LogAssert.Expect(LogType.Error, new Regex("corrupted"));

            await package.LoadAsync();
            package.Set(3);
            await package.SaveNowAsync();

            Assert.IsTrue(package.CanSave);
            CollectionAssert.AreEqual(new[] { 3 }, _service.Saves);
        }

        [Test]
        public async Task ChangeWhileLoading_IsReplacedByTheLoadedData_AndWaitUntilLoadedCompletesAfterwards()
        {
            var package = CreatePackage();
            _service.Status = SaveLoadStatus.Loaded;
            _service.StoredValue = 10;
            _service.Gate = new UniTaskCompletionSource();

            var loading = package.LoadAsync();
            var loaded = package.WaitUntilLoadedAsync();
            LogAssert.Expect(LogType.Error, new Regex("is LOST"));
            package.Set(99); // too early: the load has not finished
            Assert.AreEqual(UniTaskStatus.Pending, loaded.Status);

            _service.Gate.TrySetResult();
            await loading;

            Assert.AreEqual(10, package.Value);
            Assert.IsFalse(package.IsDirty);
            Assert.AreEqual(UniTaskStatus.Succeeded, loaded.Status);
        }

        // ---- Manager ----

        private DataPersistenceManager CreateManager()
        {
            // Edit mode: Awake does not run, so the manager does not initialize by itself.
            _manager = new GameObject("DataPersistenceManager").AddComponent<DataPersistenceManager>();
            return _manager;
        }

        [Test]
        public void GetPackage_ByBaseType_FindsTheDerivedPackage()
        {
            var manager = CreateManager();
            var derived = new DerivedPackage();
            manager.RegisterPackage(derived);

            Assert.AreSame(derived, manager.GetPackage<BasePackage>());
            Assert.AreSame(derived, manager.GetPackage<DerivedPackage>());
        }

        [Test]
        public void RegisterPackage_SharedStorageKey_IsRejected()
        {
            var manager = CreateManager();
            manager.RegisterPackage(new BasePackage("shared"));
            LogAssert.Expect(LogType.Error, new Regex("storage key 'shared'"));

            manager.RegisterPackage(new OtherPackage("shared"));

            Assert.AreEqual(1, manager.PackageCount);
        }

        [Test]
        public async Task LoadAll_SkipsPackagesWithUnsavedChanges_UnlessDiscarding()
        {
            var manager = CreateManager();
            var package = CreatePackage();
            manager.RegisterPackage(package);
            package.Set(1);
            _service.Status = SaveLoadStatus.Loaded;
            _service.StoredValue = 9;

            await manager.LoadAllAsync();
            Assert.AreEqual(1, package.Value, "unsaved change kept");

            await manager.LoadAllAsync(discardUnsavedChanges: true);
            Assert.AreEqual(9, package.Value);
        }

        [Test]
        public void WaitUntilReady_FailsWhenThereIsNoSaveService()
        {
            var manager = CreateManager();
            LogAssert.Expect(LogType.Error, new Regex("Cannot find ISaveService"));

            manager.InitializeWithoutVContainer();

            Assert.Throws<InvalidOperationException>(() => manager.WaitUntilReadyAsync().GetAwaiter().GetResult());
        }
    }
}
