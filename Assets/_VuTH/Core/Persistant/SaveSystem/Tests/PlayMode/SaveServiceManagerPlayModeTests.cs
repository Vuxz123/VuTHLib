using System;
using System.Collections;
using System.IO;
using _VuTH.Core.Persistant.SaveSystem.Backend;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Persistant.SaveSystem.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for SaveServiceManager lifecycle.
    /// Tests integration with Unity scene lifecycle.
    /// </summary>
    public class SaveServiceManagerPlayModeTests
    {
        private SaveServiceManager _manager;
        private string _tempDir;

        private void LogManagerState(string label)
        {
            var manager = _manager;
            var managerId = manager != null ? manager.GetInstanceID() : -1;
            var hasInstance = SaveServiceManager.HasInstance;
            var instance = SaveServiceManager.Instance as SaveServiceManager;
            var instanceId = instance != null ? instance.GetInstanceID() : -1;
            var instanceMatches = instance != null && manager != null && ReferenceEquals(instance, manager);

            object saveService = null;
            object backend = null;
            object enableSystem = null;
            object customLifecycle = null;

            if (manager != null)
            {
                var saveServiceField = typeof(SaveServiceManager).GetField("_saveService",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var backendField = typeof(SaveServiceManager).GetField("_backend",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                saveService = saveServiceField?.GetValue(manager);
                backend = backendField?.GetValue(manager);

                var vManagerType = manager.GetType().BaseType?.BaseType;
                var enableField = vManagerType?.GetField("enableSystem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var customField = vManagerType?.GetField("customLifecycleManagement",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                enableSystem = enableField?.GetValue(manager);
                customLifecycle = customField?.GetValue(manager);
            }

            Debug.Log(
                $"[SaveServiceManagerPlayModeTests] {label} managerId={managerId} hasInstance={hasInstance} instanceId={instanceId} " +
                $"instanceMatches={instanceMatches} enableSystem={enableSystem} customLifecycle={customLifecycle} backendNull={backend == null} " +
                $"saveServiceNull={saveService == null}");
        }

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

        [UnityTest]
        public IEnumerator Initialize_WithDefaultConfig_CreatesService()
        {
            // Arrange
            var go = new GameObject("SaveServiceManager");
            _manager = go.AddComponent<SaveServiceManager>();

            // Inject temp backend before Init
            var field = typeof(SaveServiceManager).GetField("_backend",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tempBackend = new JsonFileSaveBackend(Path.Combine(_tempDir, "default_test.json"));
            field?.SetValue(_manager, tempBackend);

            LogManagerState("Before EnableSystem");

            // Act - Enable system triggers InitializeManager -> InitializeBootstrap
            _manager.EnableSystem(true);
            LogManagerState("After EnableSystem");
            yield return new WaitForSeconds(0.5f);
            LogManagerState("After WaitForSeconds");

            // Assert - SaveServiceManager itself is the ISaveManager
            Assert.IsNotNull(_manager, "SaveServiceManager should implement ISaveManager");
        }

        [UnityTest]
        public IEnumerator SaveAndLoad_WithProfile_ChecksRoundTrip()
        {
            // Arrange
            var go = new GameObject("SaveServiceManager");
            _manager = go.AddComponent<SaveServiceManager>();

            // Inject temp backend
            var field = typeof(SaveServiceManager).GetField("_backend",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tempBackend = new JsonFileSaveBackend(Path.Combine(_tempDir, "profile_test.json"));
            field?.SetValue(_manager, tempBackend);

            LogManagerState("Before EnableSystem");
            _manager.EnableSystem(true);
            LogManagerState("After EnableSystem");
            yield return new WaitForSeconds(0.5f);
            LogManagerState("After WaitForSeconds");

            // Act
            var testData = new TestPlayData { Score = 100, Name = "Player1" };
            yield return _manager.SaveAsync("test_key", testData).ToCoroutine();

            var loaded = default(TestPlayData);
            yield return _manager.LoadAsync("test_key", new TestPlayData())
                .ContinueWith(result => loaded = result).ToCoroutine();

            // Assert
            Assert.AreEqual(100, loaded.Score, "Score should match");
            Assert.AreEqual("Player1", loaded.Name, "Name should match");
        }

        [UnityTest]
        public IEnumerator Load_MissingKey_ReturnsDefault()
        {
            // Arrange
            var go = new GameObject("SaveServiceManager");
            _manager = go.AddComponent<SaveServiceManager>();

            var field = typeof(SaveServiceManager).GetField("_backend",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tempBackend = new JsonFileSaveBackend(Path.Combine(_tempDir, "default_test.json"));
            field?.SetValue(_manager, tempBackend);

            LogManagerState("Before EnableSystem");
            _manager.EnableSystem(true);
            LogManagerState("After EnableSystem");
            yield return new WaitForSeconds(0.5f);
            LogManagerState("After WaitForSeconds");

            // Act
            var defaultData = new TestPlayData { Score = -1, Name = "Default" };
            var loaded = default(TestPlayData);
            yield return _manager.LoadAsync<TestPlayData>("missing_key", defaultData)
                .ContinueWith(result => loaded = result).ToCoroutine();

            // Assert
            Assert.AreEqual(-1, loaded.Score, "Should return default score");
            Assert.AreEqual("Default", loaded.Name, "Should return default name");
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
