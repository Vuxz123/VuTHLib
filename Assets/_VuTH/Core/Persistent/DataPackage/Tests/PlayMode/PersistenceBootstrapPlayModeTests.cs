using System;
using System.Collections;
using System.IO;
using _VuTH.Core.Persistent.SaveSystem;
using _VuTH.Core.Persistent.SaveSystem.Backend;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
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
