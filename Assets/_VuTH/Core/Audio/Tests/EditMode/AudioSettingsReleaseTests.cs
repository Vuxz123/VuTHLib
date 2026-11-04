using System.Collections.Generic;
using System.Threading;
using _VuTH.Core.Persistent.SaveSystem;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace _VuTH.Core.Audio.Tests.EditMode
{
    /// <summary>Releasing an owned settings package (quit / teardown) must persist pending changes synchronously.</summary>
    public class AudioSettingsReleaseTests
    {
        private sealed class RecordingSaveService : ISaveService, ISyncSaveService
        {
            public readonly List<(string Key, bool Sync)> Saves = new();
            public bool SupportsSync = true;

            public bool TrySaveImmediate<T>(string key, T data)
            {
                if (!SupportsSync) return false;
                Saves.Add((key, true));
                return true;
            }

            public UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default)
            {
                Saves.Add((key, false));
                return UniTask.CompletedTask;
            }

            public UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, CancellationToken cancellationToken = default)
                => UniTask.FromResult(SaveLoadResult<T>.NotFound());

            public UniTask<T> LoadAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
                => UniTask.FromResult(defaultValue);

            public UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
                => UniTask.FromResult(false);

            public UniTask DeleteAsync(string key, CancellationToken cancellationToken = default)
                => UniTask.CompletedTask;
        }

        [Test]
        public void ReleasePackage_DirtyOwnedPackage_IsSavedSynchronously()
        {
            var service = new RecordingSaveService();
            var package = new AudioSettingsPackage();
            package.SetSaveService(service);
            var binding = new AudioSettingsBinding(() => { });
            binding.Bind(package, ownsPackage: true, isRegistered: false);

            package.MasterVolume.Value = 0.3f; // pending change, still inside the debounce window
            binding.ReleasePackage();

            CollectionAssert.AreEqual(new[] { ("audio_settings", true) }, service.Saves);
        }

        [Test]
        public void ReleasePackage_WithoutSyncBackend_FallsBackToAsyncSave()
        {
            var service = new RecordingSaveService { SupportsSync = false };
            var package = new AudioSettingsPackage();
            package.SetSaveService(service);
            var binding = new AudioSettingsBinding(() => { });
            binding.Bind(package, ownsPackage: true, isRegistered: false);

            package.MasterVolume.Value = 0.3f;
            binding.ReleasePackage();

            CollectionAssert.AreEqual(new[] { ("audio_settings", false) }, service.Saves);
        }

        [Test]
        public void ReleasePackage_CleanPackage_DoesNotWrite()
        {
            var service = new RecordingSaveService();
            var package = new AudioSettingsPackage();
            package.SetSaveService(service);
            var binding = new AudioSettingsBinding(() => { });
            binding.Bind(package, ownsPackage: true, isRegistered: false);

            binding.ReleasePackage();

            Assert.IsEmpty(service.Saves);
        }
    }
}
