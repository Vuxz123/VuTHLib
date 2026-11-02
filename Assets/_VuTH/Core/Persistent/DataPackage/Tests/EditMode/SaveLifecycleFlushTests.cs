using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _VuTH.Core.Persistent.SaveSystem;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Core.Persistent.DataPackage.Tests.EditMode
{
    /// <summary>
    /// Synchronous flush on app pause / quit: PersistencePackage.TrySaveImmediate and SaveLifecycleHook.FlushAllImmediate.
    /// </summary>
    [TestFixture]
    public class SaveLifecycleFlushTests
    {
        public class Dto
        {
            public int Value;
        }

        private sealed class TestPackage : PersistencePackage<Dto>
        {
            public int Value;

            public TestPackage(SaveStrategy strategy = SaveStrategy.OnAppClose) : base("test_pkg", strategy)
            {
            }

            public override Dto ExtractPayload() => new() { Value = Value };
            public override void InjectPayload(Dto data) => Value = data.Value;
        }

        /// <summary>Records saves; optionally supports the synchronous path.</summary>
        private class RecordingSaveService : ISaveService
        {
            public readonly List<(string Key, object Data, bool Sync)> Saves = new();

            public UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default)
            {
                Saves.Add((key, data, false));
                return UniTask.CompletedTask;
            }

            public UniTask<T> LoadAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
                => UniTask.FromResult(defaultValue);

            public UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
                => UniTask.FromResult(false);

            public UniTask DeleteAsync(string key, CancellationToken cancellationToken = default)
                => UniTask.CompletedTask;
        }

        private sealed class SyncRecordingSaveService : RecordingSaveService, ISyncSaveService
        {
            public bool Throw;

            public bool TrySaveImmediate<T>(string key, T data)
            {
                if (Throw) throw new InvalidOperationException("disk full");
                Saves.Add((key, data, true));
                return true;
            }
        }

        private const string RelayName = "[VuTH] ApplicationPauseRelay";

        [Test]
        public void TrySaveImmediate_DirtyPackage_WritesSynchronously_AndClearsDirty()
        {
            var service = new SyncRecordingSaveService();
            var package = new TestPackage { Value = 42 };
            package.SetSaveService(service);
            package.MarkDirty();

            var written = package.TrySaveImmediate();

            Assert.IsTrue(written);
            Assert.IsFalse(package.IsDirty);
            Assert.AreEqual(1, service.Saves.Count);
            Assert.IsTrue(service.Saves[0].Sync);
            Assert.AreEqual(42, ((Dto)service.Saves[0].Data).Value);
        }

        [Test]
        public void TrySaveImmediate_CleanPackage_DoesNothing()
        {
            var service = new SyncRecordingSaveService();
            var package = new TestPackage();
            package.SetSaveService(service);

            Assert.IsFalse(package.TrySaveImmediate());
            Assert.IsEmpty(service.Saves);
        }

        [Test]
        public void TrySaveImmediate_ServiceWithoutSyncPath_ReturnsFalse_AndStaysDirty()
        {
            var service = new RecordingSaveService();
            var package = new TestPackage();
            package.SetSaveService(service);
            package.MarkDirty();

            Assert.IsFalse(package.TrySaveImmediate());
            Assert.IsTrue(package.IsDirty);
            Assert.IsEmpty(service.Saves);
        }

        [Test]
        public void TrySaveImmediate_WhenWriteThrows_ReturnsFalse_AndStaysDirty()
        {
            var service = new SyncRecordingSaveService { Throw = true };
            var package = new TestPackage();
            package.SetSaveService(service);
            package.MarkDirty();

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                Assert.IsFalse(package.TrySaveImmediate());
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }

            Assert.IsTrue(package.IsDirty);
        }

        [Test]
        public void FlushAllImmediate_SavesOnlyDirtyRegisteredPackages()
        {
            var service = new SyncRecordingSaveService();
            var dirty = new TestPackage { Value = 1 };
            var clean = new TestPackage { Value = 2 };
            dirty.SetSaveService(service);
            clean.SetSaveService(service);
            dirty.MarkDirty();

            var hook = new SaveLifecycleHook();
            hook.RegisterPackage(dirty);
            hook.RegisterPackage(clean);

            var written = hook.FlushAllImmediate();

            Assert.AreEqual(1, written);
            Assert.AreEqual(1, service.Saves.Count);
            Assert.IsTrue(service.Saves[0].Sync);
            Assert.IsFalse(dirty.IsDirty);
        }

        [Test]
        public void FlushAllImmediate_DisposedRegistration_IsSkipped()
        {
            var service = new SyncRecordingSaveService();
            var package = new TestPackage();
            package.SetSaveService(service);
            package.MarkDirty();

            var hook = new SaveLifecycleHook();
            hook.RegisterPackage(package).Dispose();

            Assert.AreEqual(0, hook.FlushAllImmediate());
            Assert.IsEmpty(service.Saves);
        }

        [Test]
        public void FlushAllImmediate_NoSyncPath_FallsBackToAsyncSave()
        {
            var service = new RecordingSaveService();
            var package = new TestPackage();
            package.SetSaveService(service);
            package.MarkDirty();

            var hook = new SaveLifecycleHook();
            hook.RegisterPackage(package);

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                Assert.AreEqual(0, hook.FlushAllImmediate());
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }

            // RecordingSaveService completes synchronously, so the fallback SaveNow() has already run.
            Assert.AreEqual(1, service.Saves.Count);
            Assert.IsFalse(service.Saves[0].Sync);
        }

        [Test]
        public void InitializeAndDispose_CreateAndRemoveThePauseRelay()
        {
            var hook = new SaveLifecycleHook();

            hook.Initialize();
            Assert.IsTrue(RelayExists(), "Initialize should create the pause relay GameObject");

            hook.Dispose();
            Assert.IsFalse(RelayExists(), "Dispose should destroy the pause relay GameObject");
        }

        private static bool RelayExists()
        {
            return Resources.FindObjectsOfTypeAll<GameObject>().Any(go => go != null && go.name == RelayName);
        }
    }
}
