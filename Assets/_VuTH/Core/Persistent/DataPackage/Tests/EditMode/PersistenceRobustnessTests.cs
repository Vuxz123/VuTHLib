using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
    /// A load that finishes after a slot switch is ignored; saves that fail are retried and reported; changes made while
    /// saving is blocked survive the next load; deleting everything is serialized with slot switches and does not stop
    /// at a failing package; packages created by the manager are disposed with it.
    /// </summary>
    [TestFixture]
    public class PersistenceRobustnessTests
    {
        public class Dto
        {
            public int Value = 1;
        }

        public class Package : PersistencePackage<Dto>
        {
            public int Value = 1;
            public readonly List<int> Injected = new();
            public bool SlotScoped = true;
            public bool BrokenDefaults;
            public TimeSpan? RetryDelay = TimeSpan.FromMilliseconds(1);

            public Package(string key = "progress", SaveStrategy strategy = SaveStrategy.ManualOnly) : base(key, strategy)
            {
            }

            public override bool IsSlotScoped => SlotScoped;

            public void Add(int amount)
            {
                Value += amount;
                MarkDirty();
            }

            public override Dto ExtractPayload() => new() { Value = Value };

            public override void InjectPayload(Dto data)
            {
                Value = data.Value;
                Injected.Add(data.Value);
            }

            protected override Dto CreateDefaultPayload() =>
                BrokenDefaults ? throw new InvalidOperationException("no defaults") : base.CreateDefaultPayload();

            protected override TimeSpan? GetSaveRetryDelay(int failures) => failures > 3 ? null : RetryDelay;
        }

        private sealed class Service : ISaveService
        {
            public readonly Dictionary<string, int> Stored = new();
            public UniTaskCompletionSource LoadGate;
            public SaveLoadStatus? ForcedStatus;
            public int FailingSaves;

            public UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default)
            {
                if (FailingSaves > 0)
                {
                    FailingSaves--;
                    throw new IOException("disk full");
                }

                Stored[key] = ((Dto)(object)data).Value;
                return UniTask.CompletedTask;
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

        private Service _service;
        private DataPersistenceManager _manager;

        [SetUp]
        public void SetUp() => _service = new Service();

        [TearDown]
        public void TearDown()
        {
            if (_manager) Object.DestroyImmediate(_manager.gameObject);
        }

        private Package Create(string key = "progress", SaveStrategy strategy = SaveStrategy.ManualOnly)
        {
            var package = new Package(key, strategy);
            package.SetSaveService(_service);
            return package;
        }

        private DataPersistenceManager CreateManager(params IPersistencePackage[] packages)
        {
            _manager = new GameObject("DataPersistenceManager").AddComponent<DataPersistenceManager>();
            typeof(DataPersistenceManager).GetField("_saveService", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_manager, _service);
            foreach (var package in packages) _manager.RegisterPackage(package);
            return _manager;
        }

        private static async Task WaitUntil(Func<bool> condition, string message)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) Assert.Fail(message);
                await UniTask.Yield();
            }
        }

        // ---- #1: a load still reading the previous slot ----

        [Test]
        public async Task LoadOfThePreviousSlot_FinishingAfterASwitch_IsIgnored()
        {
            var package = Create();
            var manager = CreateManager(package);
            _service.Stored["progress"] = 7;
            _service.Stored["slot-2/progress"] = 42;
            _service.LoadGate = new UniTaskCompletionSource();

            var firstLoad = package.LoadAsync();           // reads the default slot, held at the gate
            var switching = manager.SwitchSlotAsync("2");  // moves the package while that read is pending
            var loaded = package.WaitUntilLoadedAsync();
            _service.LoadGate.TrySetResult();

            await firstLoad;
            await switching;
            await loaded;

            Assert.AreEqual(42, package.Value);
            CollectionAssert.DoesNotContain(package.Injected, 7, "the previous slot's data is never shown");
        }

        // ---- #2: failed saves ----

        [Test]
        public async Task FailedSave_IsReported_AndRetried()
        {
            var package = Create(strategy: SaveStrategy.Immediate);
            var manager = CreateManager(package);
            Exception reported = null;
            IPersistencePackage reportedByManager = null;
            package.SaveFailed += (_, error) => reported = error;
            manager.OnSaveFailed += (failed, _) => reportedByManager = failed;
            _service.FailingSaves = 2;
            LogAssert.Expect(LogType.Error, new Regex("Save failed for progress"));
            LogAssert.Expect(LogType.Error, new Regex("Save failed for progress"));

            package.Add(5);
            await package.SaveNowAsync();

            Assert.IsInstanceOf<IOException>(reported);
            Assert.AreSame(package, reportedByManager);
            await WaitUntil(() => !package.IsDirty, "the package saves again on its own after a failure");
            Assert.AreEqual(6, _service.Stored["progress"]);
        }

        [Test]
        public async Task ManualOnlyPackage_IsNotRetried()
        {
            var package = Create();
            _service.FailingSaves = 1;
            LogAssert.Expect(LogType.Error, new Regex("Save failed for progress"));

            package.Add(5);
            await package.SaveNowAsync();
            await UniTask.DelayFrame(5);

            Assert.IsTrue(package.IsDirty);
            Assert.IsFalse(_service.Stored.ContainsKey("progress"));
        }

        // ---- #3: changes while saving is blocked ----

        [Test]
        public async Task ChangeMadeWhileSavingIsBlocked_IsAppliedAgain_AfterTheNextSuccessfulLoad()
        {
            var package = Create();
            _service.ForcedStatus = SaveLoadStatus.ReadFailed;
            LogAssert.Expect(LogType.Error, new Regex("could not be loaded"));
            await package.LoadAsync();

            package.Modify(() => package.Add(5)); // e.g. a restored purchase
            Assert.AreEqual(6, package.Value, "shown at once");

            _service.ForcedStatus = null;
            _service.Stored["progress"] = 100;
            await package.LoadAsync();

            Assert.AreEqual(105, package.Value, "applied to the data that could finally be loaded");
            Assert.IsTrue(package.IsDirty);
        }

        [Test]
        public async Task ChangeDeferredDuringAFailedLoad_IsKept_ForTheNextLoad()
        {
            var package = Create();
            _service.ForcedStatus = SaveLoadStatus.ReadFailed;
            _service.LoadGate = new UniTaskCompletionSource();
            LogAssert.Expect(LogType.Error, new Regex("could not be loaded"));

            var loading = package.LoadAsync();
            package.Modify(() => package.Add(5));
            LogAssert.Expect(LogType.Warning, new Regex("kept and applied again"));
            _service.LoadGate.TrySetResult();
            await loading;
            Assert.AreEqual(6, package.Value);

            _service.LoadGate = null;
            _service.ForcedStatus = null;
            _service.Stored["progress"] = 100;
            await package.LoadAsync();

            Assert.AreEqual(105, package.Value);
        }

        [Test]
        public async Task ChangeMadeWhileSavingIsBlocked_IsAppliedAfterADelete()
        {
            var package = Create();
            _service.ForcedStatus = SaveLoadStatus.ReadFailed;
            LogAssert.Expect(LogType.Error, new Regex("could not be loaded"));
            await package.LoadAsync();
            package.Modify(() => package.Add(5));

            await package.DeleteAsync();

            Assert.AreEqual(6, package.Value, "defaults (1) plus the kept change");
            Assert.IsTrue(package.IsDirty);
        }

        // ---- #4: delete all ----

        [Test]
        public async Task DeleteAll_ContinuesPastAFailingPackage_AndReportsIt()
        {
            var broken = Create("broken");
            broken.SlotScoped = false;
            broken.BrokenDefaults = true;
            var healthy = Create("healthy");
            healthy.SlotScoped = false;
            var manager = CreateManager(broken, healthy);
            _service.Stored["broken"] = 3;
            _service.Stored["healthy"] = 4;
            LogAssert.Expect(LogType.Error, new Regex("Could not delete 'broken'"));

            AggregateException error = null;
            try
            {
                await manager.DeleteAllAsync();
            }
            catch (AggregateException e)
            {
                error = e;
            }

            Assert.IsNotNull(error);
            Assert.AreEqual(1, error.InnerExceptions.Count);
            Assert.IsFalse(_service.Stored.ContainsKey("healthy"), "the healthy package is still deleted");
            Assert.IsTrue(_service.Stored.ContainsKey("broken"), "a package without defaults deletes nothing");
        }

        [Test]
        public async Task DeleteAll_WaitsForASlotSwitchInProgress()
        {
            var package = Create();
            var manager = CreateManager(package);
            _service.Stored["slot-2/progress"] = 42;
            _service.LoadGate = new UniTaskCompletionSource();

            var switching = manager.SwitchSlotAsync("2");
            var deleting = manager.DeleteAllAsync();
            Assert.AreEqual(UniTaskStatus.Pending, deleting.Status);

            _service.LoadGate.TrySetResult();
            await switching;
            await deleting;

            Assert.IsFalse(_service.Stored.ContainsKey("slot-2/progress"), "deleted in the slot switched to");
            Assert.AreEqual(1, package.Value);
        }

        // ---- #6: narrower API ----

        [Test]
        public void TryGetPackage_DoesNotWarn_WhenMissing()
        {
            var manager = CreateManager();

            Assert.IsFalse(manager.TryGetPackage<Package>(out var package));
            Assert.IsNull(package);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SlotAndService_AreNotPartOfThePublicInterface()
        {
            Assert.IsNull(typeof(IPersistencePackage).GetMethod("SetSlot"));
            Assert.IsNull(typeof(IPersistencePackage).GetMethod("SetSaveService"));
            Assert.IsNull(typeof(Package).GetMethod("SetSlot", BindingFlags.Instance | BindingFlags.Public));
        }
    }
}
