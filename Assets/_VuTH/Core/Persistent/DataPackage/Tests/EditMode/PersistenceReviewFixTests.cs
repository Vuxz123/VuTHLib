using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using _VuTH.Core.Persistent.SaveSystem;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Persistent.DataPackage.Tests.EditMode
{
    /// <summary>
    /// Slot switches wait for the new data, run one at a time and cannot be split by a cancel; changes during a load
    /// can be deferred; batches save once; unreadable saves are reported; fields are disposed with their package.
    /// </summary>
    [TestFixture]
    public class PersistenceReviewFixTests
    {
        public class Dto
        {
            public int Value = 1;
            public int Other;
        }

        public class Package : PersistencePackage<Dto>
        {
            public int Value = 1;
            public int Other;
            public bool SlotScoped = true;

            public Package(string key = "progress") : base(key, SaveStrategy.ManualOnly)
            {
            }

            public override bool IsSlotScoped => SlotScoped;

            public void Set(int value)
            {
                Value = value;
                MarkDirty();
            }

            public void SetOther(int value)
            {
                Other = value;
                MarkDirty();
            }

            public override Dto ExtractPayload() => new() { Value = Value, Other = Other };

            public override void InjectPayload(Dto data)
            {
                Value = data.Value;
                Other = data.Other;
            }
        }

        /// <summary>Stores values per key; loads and saves can be held at gates.</summary>
        private sealed class GatedService : ISaveService
        {
            public readonly Dictionary<string, int> Stored = new();
            public UniTaskCompletionSource LoadGate;
            public UniTaskCompletionSource SaveGate;
            public SaveLoadStatus? ForcedStatus;

            public async UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default)
            {
                if (SaveGate != null) await SaveGate.Task;
                Stored[key] = ((Dto)(object)data).Value;
            }

            public async UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, CancellationToken cancellationToken = default)
            {
                if (LoadGate != null) await LoadGate.Task;
                if (ForcedStatus is { } status) return SaveLoadResult<T>.Failure(status, "forced");

                return Stored.TryGetValue(key, out var value)
                    ? SaveLoadResult<T>.Success((T)(object)new Dto { Value = value })
                    : SaveLoadResult<T>.NotFound();
            }

            public async UniTask<T> LoadAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
            {
                var result = await TryLoadAsync<T>(key, cancellationToken);
                return result.IsLoaded ? result.Data : defaultValue;
            }

            public UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
                UniTask.FromResult(Stored.ContainsKey(key));

            public UniTask DeleteAsync(string key, CancellationToken cancellationToken = default)
            {
                Stored.Remove(key);
                return UniTask.CompletedTask;
            }
        }

        private GatedService _service;
        private DataPersistenceManager _manager;

        [SetUp]
        public void SetUp() => _service = new GatedService();

        [TearDown]
        public void TearDown()
        {
            if (_manager) Object.DestroyImmediate(_manager.gameObject);
        }

        private Package Create(string key = "progress", bool slotScoped = true)
        {
            var package = new Package(key) { SlotScoped = slotScoped };
            package.SetSaveService(_service);
            return package;
        }

        private DataPersistenceManager CreateManager(params IPersistencePackage[] packages)
        {
            _manager = new GameObject("DataPersistenceManager").AddComponent<DataPersistenceManager>();
            foreach (var package in packages) _manager.RegisterPackage(package);
            return _manager;
        }

        // ---- #2: waiting after a slot switch ----

        [Test]
        public async Task WaitUntilLoaded_AfterASlotSwitch_WaitsForTheNewSlotsData()
        {
            var package = Create();
            await package.LoadAsync();
            var manager = CreateManager(package);
            _service.Stored["slot-2/progress"] = 42;
            _service.LoadGate = new UniTaskCompletionSource();

            var switching = manager.SwitchSlotAsync("2");
            var loaded = package.WaitUntilLoadedAsync();

            Assert.AreEqual(UniTaskStatus.Pending, loaded.Status, "the previous slot's load does not count");
            _service.LoadGate.TrySetResult();
            await switching;
            await loaded;
            Assert.AreEqual(42, package.Value);
        }

        // ---- #3: changes during a load ----

        [Test]
        public async Task Modify_DuringALoad_IsAppliedAfterTheLoadedData()
        {
            var package = Create();
            _service.Stored["progress"] = 10;
            _service.LoadGate = new UniTaskCompletionSource();

            var loading = package.LoadAsync();
            package.Modify(() => package.SetOther(5)); // e.g. a purchase restored at startup
            _service.LoadGate.TrySetResult();
            await loading;

            Assert.AreEqual(10, package.Value, "loaded data");
            Assert.AreEqual(5, package.Other, "the change is kept, on top of it");
            Assert.IsTrue(package.IsDirty, "and saved");
        }

        [Test]
        public void Modify_WhenNotLoading_RunsAtOnce()
        {
            var package = Create();

            package.Modify(() => package.Set(3));

            Assert.AreEqual(3, package.Value);
        }

        // ---- #4: slot switches ----

        [Test]
        public async Task SwitchSlot_CanceledBeforeStarting_ChangesNothing()
        {
            var package = Create();
            var manager = CreateManager(package);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var canceled = false;
            try
            {
                await manager.SwitchSlotAsync("2", cts.Token);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            Assert.IsTrue(canceled);
            Assert.IsNull(manager.ActiveSlot);
            Assert.AreEqual("progress", package.StorageKey);
        }

        [Test]
        public async Task SwitchSlot_RunsOneSwitchAtATime()
        {
            var a = Create("a");
            var b = Create("b");
            var manager = CreateManager(a, b);
            a.Set(7);
            _service.SaveGate = new UniTaskCompletionSource();

            var first = manager.SwitchSlotAsync("1");
            var second = manager.SwitchSlotAsync("2");
            Assert.AreEqual(UniTaskStatus.Pending, second.Status);

            _service.SaveGate.TrySetResult();
            await first;
            await second;

            Assert.AreEqual("2", manager.ActiveSlot);
            Assert.AreEqual("slot-2/a", a.StorageKey);
            Assert.AreEqual("slot-2/b", b.StorageKey, "never split between two slots");
            Assert.AreEqual(7, _service.Stored["a"], "the change stayed in the slot it was made in");
        }

        [Test]
        public async Task SwitchSlot_ChangeMadeWhileSaving_IsSavedToTheSlotBeingLeft()
        {
            var package = Create();
            var manager = CreateManager(package);
            package.Set(1);
            _service.SaveGate = new UniTaskCompletionSource();

            var switching = manager.SwitchSlotAsync("2");
            package.Set(2); // after the save started, before the switch
            _service.SaveGate.TrySetResult();
            await switching;

            Assert.AreEqual(2, _service.Stored["progress"]);
        }

        // ---- #5: unreadable saves are reported ----

        [Test]
        public async Task UnreadableSave_RaisesSaveBlocked_OnThePackageAndTheManager()
        {
            var package = Create(slotScoped: false);
            var manager = CreateManager(package);
            var packageEvents = 0;
            IPersistencePackage managerEvent = null;
            package.SaveBlocked += _ => packageEvents++;
            manager.OnSaveBlocked += p => managerEvent = p;
            _service.ForcedStatus = SaveLoadStatus.ReadFailed;
            LogAssert.Expect(LogType.Error, new Regex("could not be loaded"));

            await package.LoadAsync();

            Assert.AreEqual(1, packageEvents);
            Assert.AreSame(package, managerEvent);
        }

        // ---- #6: batches ----

        [Test]
        public void BatchChanges_MarksDirtyOnce_WhenTheBatchEnds()
        {
            var package = Create();
            var dirtyEvents = 0;
            using var subscription = package.DirtyObservable.Where(d => d).Subscribe(_ => dirtyEvents++);

            using (package.BatchChanges())
            {
                package.Set(5);
                package.SetOther(6);
                using (package.BatchChanges()) package.Set(7);
                Assert.AreEqual(0, dirtyEvents, "nothing saved half way");
            }

            Assert.AreEqual(1, dirtyEvents);
            Assert.IsTrue(package.IsDirty);
        }

        [Test]
        public void PlayerProfile_AddExp_IsOneChange()
        {
            var profile = new PlayerProfilePackage();
            var dirtyEvents = 0;
            using var subscription = profile.DirtyObservable.Where(d => d).Subscribe(_ => dirtyEvents++);

            profile.AddExp(150); // exp and level change together

            Assert.AreEqual(1, dirtyEvents);
            Assert.AreEqual(2, profile.Level.Value);
            Assert.AreEqual(50, profile.Exp.Value);
            profile.Dispose();
        }

        // ---- #8 / #10: disposal ----

        [Test]
        public void Dispose_DisposesThePackagesFields()
        {
            var profile = new PlayerProfilePackage();
            var completed = false;
            profile.Gold.Observable.Subscribe(_ => { }, _ => completed = true);

            profile.Dispose();

            Assert.IsTrue(completed, "fields register themselves and are disposed with the package");
        }

        [Test]
        public async Task Dispose_WhileASaveWaits_DoesNotBreakIt()
        {
            var package = Create();
            package.Set(4);
            _service.SaveGate = new UniTaskCompletionSource();

            var saving = package.SaveNowAsync();
            package.Dispose();
            _service.SaveGate.TrySetResult();

            await saving; // used to throw ObjectDisposedException when releasing the disposed lock
            Assert.AreEqual(4, _service.Stored["progress"]);
        }

        // ---- #10: registration ----

        [Test]
        public void RejectedPackage_KeepsItsData()
        {
            var first = Create();
            var manager = CreateManager(first);
            var duplicate = Create();
            duplicate.Set(9);
            LogAssert.Expect(LogType.Error, new Regex("storage key 'progress'"));

            manager.RegisterPackage(duplicate);

            Assert.AreEqual(9, duplicate.Value, "SetSlot (which resets the data) is not called on a rejected package");
            Assert.AreEqual(1, manager.PackageCount);
        }
    }
}
