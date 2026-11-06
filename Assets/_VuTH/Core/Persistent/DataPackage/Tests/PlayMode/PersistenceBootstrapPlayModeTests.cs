using System;
using System.Collections;
using System.IO;
using System.Reflection;
using _VuTH.Core.Persistent.SaveSystem;
using _VuTH.Core.Persistent.SaveSystem.Backend;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Persistent.DataPackage.Tests.PlayMode
{
    /// <summary>
    /// End to end, with the real managers (no DI): the save manager starts, the persistence manager loads the packages
    /// of the DataPackageProfile, changes are saved by their strategy and are there again after a restart.
    /// </summary>
    public class PersistenceBootstrapPlayModeTests
    {
        private string _tempDir;
        private SaveServiceManager _saveManager;
        private DataPersistenceManager _dataManager;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"VuTH_PersistenceBootstrap_{Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (_dataManager) Object.DestroyImmediate(_dataManager.gameObject);
            if (_saveManager) Object.DestroyImmediate(_saveManager.gameObject);
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        private async UniTask<DataPersistenceManager> StartAsync()
        {
            if (!_saveManager)
            {
                _saveManager = new GameObject("SaveServiceManager").AddComponent<SaveServiceManager>();
                _saveManager.WithBackend(new JsonFileSaveBackend(_tempDir));
            }

            _dataManager = new GameObject("DataPersistenceManager").AddComponent<DataPersistenceManager>();
            await _dataManager.WaitUntilReadyAsync();
            return _dataManager;
        }

        private async UniTask RestartDataManagerAsync()
        {
            Object.Destroy(_dataManager.gameObject);
            await UniTask.NextFrame();
            await StartAsync();
        }

        public class OnCloseDto
        {
            public int Value;
        }

        /// <summary>Saved only on pause / quit.</summary>
        public class OnClosePackage : PersistencePackage<OnCloseDto>
        {
            public int Value;

            public OnClosePackage() : base("test_on_close", SaveStrategy.OnAppClose)
            {
            }

            public void Set(int value)
            {
                Value = value;
                MarkDirty();
            }

            public override OnCloseDto ExtractPayload() => new() { Value = Value };
            public override void InjectPayload(OnCloseDto data) => Value = data.Value;
        }

        [UnityTest]
        public IEnumerator WithoutDI_OnAppClosePackages_AreFlushedOnPause() => UniTask.ToCoroutine(async () =>
        {
            // The lifecycle hook used to come only from VContainer: without it, pause / quit flushed nothing.
            var manager = await StartAsync();
            var package = new OnClosePackage();
            manager.RegisterPackage(package);
            await package.WaitUntilLoadedAsync();
            package.Set(5);

            var hook = (SaveLifecycleHook)typeof(DataPersistenceManager)
                .GetField("_lifecycleHook", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(manager);
            Assert.IsNotNull(hook, "created by the manager itself");

            Assert.AreEqual(1, hook.FlushAllImmediate().Written, "the pause / quit path writes the package");
            Assert.IsFalse(package.IsDirty);

            await RestartDataManagerAsync();
            var reloaded = new OnClosePackage();
            _dataManager.RegisterPackage(reloaded);
            await reloaded.WaitUntilLoadedAsync();
            Assert.AreEqual(5, reloaded.Value);
        });

        [UnityTest]
        public IEnumerator ConfiguredPackage_IsLoaded_Saved_AndRestoredAfterRestart() => UniTask.ToCoroutine(async () =>
        {
            var manager = await StartAsync();
            var profile = manager.GetPackage<PlayerProfilePackage>();

            Assert.IsNotNull(profile, "PlayerProfilePackage comes from the DataPackageProfile");
            Assert.AreEqual(SaveLoadStatus.NotFound, profile.LoadStatus);
            await profile.WaitUntilLoadedAsync();

            profile.Gold.Value = 50; // Immediate strategy: saved right away
            await profile.SaveNowAsync(); // joins that save
            Assert.IsFalse(profile.IsDirty);

            await RestartDataManagerAsync();
            var restored = _dataManager.GetPackage<PlayerProfilePackage>();

            Assert.AreNotSame(profile, restored);
            Assert.AreEqual(SaveLoadStatus.Loaded, restored.LoadStatus);
            Assert.AreEqual(50, restored.Gold.Value);
        });

        [UnityTest]
        public IEnumerator SaveSlots_KeepSeparateProgress() => UniTask.ToCoroutine(async () =>
        {
            var manager = await StartAsync();
            var profile = manager.GetPackage<PlayerProfilePackage>();
            profile.Gold.Value = 10;
            await profile.SaveNowAsync();

            await manager.SwitchSlotAsync("2");
            Assert.AreEqual(0, profile.Gold.Value, "a new slot starts from defaults");
            profile.Gold.Value = 20;
            await profile.SaveNowAsync();

            await manager.SwitchSlotAsync(null);
            Assert.AreEqual(10, profile.Gold.Value);

            await manager.DeleteSlotAsync("2");
            await manager.SwitchSlotAsync("2");
            Assert.AreEqual(0, profile.Gold.Value, "slot 2 was deleted");
        });

        [UnityTest]
        public IEnumerator DeleteAll_ResetsProgress_AndSurvivesRestart() => UniTask.ToCoroutine(async () =>
        {
            var manager = await StartAsync();
            var profile = manager.GetPackage<PlayerProfilePackage>();
            profile.Gold.Value = 70;
            await profile.SaveNowAsync();

            await manager.DeleteAllAsync();
            Assert.AreEqual(0, profile.Gold.Value);

            await RestartDataManagerAsync();
            Assert.AreEqual(SaveLoadStatus.NotFound, _dataManager.GetPackage<PlayerProfilePackage>().LoadStatus);
        });

        /// <summary>A backend with no synchronous path.</summary>
        private sealed class AsyncOnlyBackend : ISaveBackend
        {
            private readonly System.Collections.Generic.Dictionary<string, string> _data = new();

            public UniTask SaveRawAsync(string key, string data, System.Threading.CancellationToken cancellationToken = default)
            {
                _data[key] = data;
                return UniTask.CompletedTask;
            }

            public UniTask<string> LoadRawAsync(string key, System.Threading.CancellationToken cancellationToken = default) =>
                UniTask.FromResult(_data.TryGetValue(key, out var data) ? data : null);

            public UniTask<bool> ExistsAsync(string key, System.Threading.CancellationToken cancellationToken = default) =>
                UniTask.FromResult(_data.ContainsKey(key));

            public UniTask DeleteAsync(string key, System.Threading.CancellationToken cancellationToken = default)
            {
                _data.Remove(key);
                return UniTask.CompletedTask;
            }
        }

        [UnityTest]
        public IEnumerator OnAppClosePackage_WithoutASyncBackend_IsWarnedAboutAtStartup() => UniTask.ToCoroutine(async () =>
        {
            _saveManager = new GameObject("SaveServiceManager").AddComponent<SaveServiceManager>();
            _saveManager.WithBackend(new AsyncOnlyBackend());
            var manager = await StartAsync();
            Assert.IsFalse(_saveManager.SupportsSyncSave);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "'test_on_close' \\(OnAppClose\\) is flushed on pause / quit, but the save backend has no synchronous path"));
            var package = new OnClosePackage();
            manager.RegisterPackage(package);
            await package.WaitUntilLoadedAsync();
        });

        private static bool IsDisposed(IPersistencePackage package)
        {
            for (var type = package.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) return (bool)field.GetValue(package);
            }

            throw new InvalidOperationException("No _disposed field.");
        }

        [UnityTest]
        public IEnumerator ConfiguredPackages_AreFlushedThenDisposed_WithTheManager() => UniTask.ToCoroutine(async () =>
        {
            var manager = await StartAsync();
            var profile = manager.GetPackage<PlayerProfilePackage>();
            await profile.WaitUntilLoadedAsync();
            var completed = false;
            using var subscription = profile.Gold.Observable.Subscribe(_ => { }, _ => completed = true);

            profile.Gold.Value = 70;
            Object.DestroyImmediate(_dataManager.gameObject); // deinitialized right after the change
            await UniTask.NextFrame();

            Assert.IsTrue(IsDisposed(profile), "the manager created it from the profile, so it disposes it");
            Assert.IsTrue(completed, "its fields are disposed with it, completing their subscriptions");

            await StartAsync();
            var restored = _dataManager.GetPackage<PlayerProfilePackage>();
            Assert.AreNotSame(profile, restored, "a new initialization creates new instances");
            Assert.IsFalse(IsDisposed(restored));
            Assert.AreEqual(70, restored.Gold.Value, "the change was flushed before the package was disposed");
        });

        [UnityTest]
        public IEnumerator PackageRegisteredByTheGame_IsNotDisposedWithTheManager() => UniTask.ToCoroutine(async () =>
        {
            var manager = await StartAsync();
            var package = new OnClosePackage();
            manager.RegisterPackage(package);
            await package.WaitUntilLoadedAsync();
            package.Set(3);

            Object.DestroyImmediate(_dataManager.gameObject);
            await UniTask.NextFrame();

            Assert.IsFalse(IsDisposed(package), "the game owns it");
            Assert.IsFalse(package.IsDirty, "still flushed on deinitialization");

            await StartAsync();
            var reloaded = new OnClosePackage();
            _dataManager.RegisterPackage(reloaded);
            await reloaded.WaitUntilLoadedAsync();
            Assert.AreEqual(3, reloaded.Value);
            package.Dispose();
        });

        [UnityTest]
        public IEnumerator PlayerPrefsBackend_WritesOfOneFrame_ShareAFlush_AndAllComplete() => UniTask.ToCoroutine(async () =>
        {
            var backend = new PlayerPrefsSaveBackend();
            const string keyA = "vuth_test_flush_a", keyB = "vuth_test_flush_b";

            var a = backend.SaveRawAsync(keyA, "1");
            var b = backend.SaveRawAsync(keyB, "2");
            Assert.AreEqual(UniTaskStatus.Pending, a.Status, "flushed at the end of the frame");

            await UniTask.WhenAll(a, b);
            Assert.AreEqual("1", await backend.LoadRawAsync(keyA));
            Assert.AreEqual("2", await backend.LoadRawAsync(keyB));

            await UniTask.WhenAll(backend.DeleteAsync(keyA), backend.DeleteAsync(keyB));
            Assert.IsFalse(await backend.ExistsAsync(keyA));
        });
    }
}
