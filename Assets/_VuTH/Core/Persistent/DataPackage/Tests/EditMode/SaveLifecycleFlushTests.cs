using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using _VuTH.Core.Persistent.SaveSystem;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

            public UniTaskCompletionSource LoadGate;
            public SaveLoadStatus? FailLoad;

            public async UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, CancellationToken cancellationToken = default)
            {
                if (LoadGate != null) await LoadGate.Task;
                return FailLoad is { } status ? SaveLoadResult<T>.Failure(status, "forced") : SaveLoadResult<T>.NotFound();
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
            public bool SupportsSyncSave => true;

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

            var written = hook.FlushAllImmediate().Written;

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

            Assert.AreEqual(0, hook.FlushAllImmediate().Written);
            Assert.IsEmpty(service.Saves);
        }

        [Test]
        public void FlushAllImmediate_NoSyncPath_ReportsThePackage_WithoutAnAsyncSave()
        {
            var service = new RecordingSaveService();
            var package = new TestPackage();
            package.SetSaveService(service);
            package.MarkDirty();

            var hook = new SaveLifecycleHook();
            hook.RegisterPackage(package);

            var report = hook.FlushAllImmediate();

            Assert.AreEqual(0, report.Written);
            CollectionAssert.AreEqual(new[] { package }, report.Unsupported);
            Assert.IsEmpty(report.Failed);
            Assert.IsEmpty(service.Saves, "the flush only does what is synchronous; the caller decides on a fallback");
            Assert.IsTrue(package.IsDirty);
        }

        // ---- Why nothing was written ----

        [Test]
        public void SaveImmediate_SaysWhyNothingWasWritten()
        {
            var clean = new TestPackage();
            clean.SetSaveService(new SyncRecordingSaveService());
            Assert.AreEqual(SyncSaveResult.NotNeeded, clean.SaveImmediate());

            var noService = new TestPackage();
            noService.MarkDirty();
            Assert.AreEqual(SyncSaveResult.NotReady, noService.SaveImmediate());

            var asyncOnly = new TestPackage();
            asyncOnly.SetSaveService(new RecordingSaveService());
            asyncOnly.MarkDirty();
            Assert.AreEqual(SyncSaveResult.Unsupported, asyncOnly.SaveImmediate());

            var written = new TestPackage();
            written.SetSaveService(new SyncRecordingSaveService());
            written.MarkDirty();
            Assert.AreEqual(SyncSaveResult.Written, written.SaveImmediate());
        }

        [Test]
        public async Task Flush_WhileLoading_WritesNothing_AndIsNotReportedAsUnsupported()
        {
            var service = new SyncRecordingSaveService { LoadGate = new UniTaskCompletionSource() };
            var package = new TestPackage();
            package.SetSaveService(service);
            package.MarkDirty();
            var loading = package.LoadAsync();

            Assert.AreEqual(SyncSaveResult.Loading, package.SaveImmediate());
            var report = SaveLifecycleHook.Flush(new[] { package });

            Assert.IsEmpty(report.Unsupported, "the data is about to be replaced: no fallback either");
            Assert.IsEmpty(service.Saves);
            service.LoadGate.TrySetResult();
            await loading;
        }

        [Test]
        public async Task Flush_WhenSavingIsBlocked_WarnsOnce_AndIsNotReportedAsUnsupported()
        {
            var service = new SyncRecordingSaveService { FailLoad = SaveLoadStatus.ReadFailed };
            var package = new TestPackage();
            package.SetSaveService(service);
            LogAssert.Expect(LogType.Error, new Regex("could not be loaded"));
            await package.LoadAsync();
            package.MarkDirty();

            LogAssert.Expect(LogType.Warning, new Regex("Not saving 'test_pkg'"));
            var report = SaveLifecycleHook.Flush(new[] { package });

            Assert.IsEmpty(report.Unsupported);
            Assert.IsEmpty(report.Failed);
            Assert.IsEmpty(service.Saves);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Flush_WhenTheWriteThrows_ReportsAFailure_WithoutSavingAgain()
        {
            var service = new SyncRecordingSaveService { Throw = true };
            var package = new TestPackage(SaveStrategy.ManualOnly);
            package.SetSaveService(service);
            package.MarkDirty();
            Exception reported = null;
            package.SaveFailed += (_, error) => reported = error;
            LogAssert.Expect(LogType.Error, new Regex("Immediate save failed"));

            var report = SaveLifecycleHook.Flush(new[] { package });

            CollectionAssert.AreEqual(new[] { package }, report.Failed);
            Assert.IsEmpty(report.Unsupported, "reported through SaveFailed, which retries: no second save");
            Assert.IsNotNull(reported);
            Assert.IsEmpty(service.Saves);
        }

        private static void Invoke(SaveLifecycleHook hook, string method, params object[] args) =>
            typeof(SaveLifecycleHook).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(hook, args);

        [Test]
        public void Quit_PackageWithoutSyncPath_IsReportedAsNotSaved()
        {
            var service = new RecordingSaveService();
            var package = new TestPackage();
            package.SetSaveService(service);
            package.MarkDirty();
            var hook = new SaveLifecycleHook();
            hook.RegisterPackage(package);
            LogAssert.Expect(LogType.Error, new Regex("'test_pkg' was NOT saved on quit"));

            Invoke(hook, "OnQuitting");

            Assert.IsEmpty(service.Saves, "an async save would never complete while quitting");
        }

        [Test]
        public void Pause_PackageWithoutSyncPath_IsSavedAsync_WithAWarning()
        {
            var service = new RecordingSaveService();
            var package = new TestPackage();
            package.SetSaveService(service);
            package.MarkDirty();
            var hook = new SaveLifecycleHook();
            hook.RegisterPackage(package);
            LogAssert.Expect(LogType.Warning, new Regex("completes only if the app resumes"));

            Invoke(hook, "OnPauseChanged", true);

            // RecordingSaveService completes synchronously, so the async save has already run.
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
