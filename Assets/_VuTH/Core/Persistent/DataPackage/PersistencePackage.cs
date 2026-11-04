#nullable enable
using System;
using System.Threading;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;
using R3;
using _VuTH.Core.Persistent.SaveSystem;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Base class for persistence packages.
    /// Acts as a data container only — save logic is handled by DataPersistenceManager.
    /// Uses R3 Observable for dirty state notifications.
    /// <para>
    /// Stored data is never overwritten unless loading it showed that this is safe (see <see cref="CanSave"/>):
    /// when the save cannot be read, has a newer schema or fails to migrate, saves are refused and logged.
    /// </para>
    /// </summary>
    /// <typeparam name="TData">DTO type for serialization.</typeparam>
    public abstract class PersistencePackage<TData> : IPersistencePackage<TData>, ISyncPersistencePackage, IDisposable
        where TData : class
    {
        /// <summary>
        /// Save interval in seconds for the Debounced strategy.
        /// </summary>
        public virtual float DebounceSeconds => 3.0f;

        private readonly Subject<bool> _dirtySubject = new();
        private readonly UniTaskCompletionSource _loadedSource = new();
        private ISaveService? _saveService;
        private bool _isDirty;
        private bool _isLoading;
        private bool _suppressDirty;
        private bool _isSaving;
        private bool _saveRequestedWhileSaving;
        private int _dirtyVersion;
        private bool _canSave = true;
        private bool _warnedChangeWhileLoading;

        /// <inheritdoc/>
        public string StorageKey { get; }

        /// <inheritdoc/>
        public SaveStrategy Strategy { get; }

        /// <inheritdoc/>
        public bool IsDirty => _isDirty;

        /// <inheritdoc/>
        public Observable<bool> DirtyObservable => _dirtySubject;

        /// <inheritdoc/>
        public SaveLoadStatus? LoadStatus { get; private set; }

        /// <inheritdoc/>
        public bool CanSave => _canSave;

        protected PersistencePackage(string storageKey, SaveStrategy strategy)
        {
            StorageKey = storageKey;
            Strategy = strategy;
        }

        /// <summary>
        /// Set the save service. Called by Manager during initialization.
        /// </summary>
        public void SetSaveService(ISaveService saveService)
        {
            _saveService = saveService;
        }

        /// <summary>
        /// Mark this package as dirty, notifying the Manager's save pipeline.
        /// </summary>
        public void MarkDirty()
        {
            if (_suppressDirty) return;

            if (_isLoading)
            {
                // The loaded data replaces the fields when the load completes: this change cannot be kept.
                if (!_warnedChangeWhileLoading)
                {
                    _warnedChangeWhileLoading = true;
                    this.LogWarning($"'{StorageKey}' changed while its data was loading; the loaded data will replace " +
                                    "the change. Await WaitUntilLoadedAsync before modifying the package.");
                }

                return;
            }

            _dirtyVersion++;
            _isDirty = true;
            _dirtySubject.OnNext(true);
        }

        #region Save

        /// <summary>
        /// Force save immediately. Called by Manager's save pipeline.
        /// </summary>
        public void SaveNow()
        {
            SaveNowAsync().Forget();
        }

        /// <inheritdoc/>
        public bool TrySaveImmediate()
        {
            if (_saveService is not ISyncSaveService syncService || !_isDirty || !CheckCanSave())
                return false;

            var payload = ExtractPayload();
            var versionAtSaveStart = _dirtyVersion;

            try
            {
                if (!syncService.TrySaveImmediate(StorageKey, payload))
                    return false;
            }
            catch (Exception ex)
            {
                this.LogError($"Immediate save failed for {StorageKey}: {ex.Message}");
                return false;
            }

            if (_isSaving)
            {
                // An async save that started earlier (with an older payload) may still finish after this write.
                // Ask its loop to run once more so the newest data is what ends up on disk.
                _saveRequestedWhileSaving = true;
            }
            else if (_dirtyVersion == versionAtSaveStart)
            {
                _isDirty = false;
                _dirtySubject.OnNext(false);
            }

            return true;
        }

        public async UniTask SaveNowAsync()
        {
            if (_saveService == null || !_isDirty || !CheckCanSave())
                return;

            if (_isSaving)
            {
                _saveRequestedWhileSaving = true;
                return;
            }

            do
            {
                _isSaving = true;
                _saveRequestedWhileSaving = false;

                var payload = ExtractPayload();
                var versionAtSaveStart = _dirtyVersion;

                try
                {
                    await _saveService.SaveAsync(StorageKey, payload, CancellationToken.None);

                    if (_dirtyVersion == versionAtSaveStart)
                    {
                        _isDirty = false;
                        _dirtySubject.OnNext(false);
                    }
                    else
                    {
                        _isDirty = true;
                        _saveRequestedWhileSaving = true;
                    }
                }
                catch (Exception ex)
                {
                    _isDirty = true;
                    this.LogError($"Save failed for {StorageKey}: {ex.Message}");
                    break;
                }
                finally
                {
                    _isSaving = false;
                }
            } while (_saveRequestedWhileSaving && _saveService != null);
        }

        private bool CheckCanSave()
        {
            if (_canSave) return true;

            this.LogWarning($"Not saving '{StorageKey}': its stored data could not be loaded ({LoadStatus}) and would " +
                            "be overwritten. Fix the cause and call LoadAsync again to re-enable saving.");
            return false;
        }

        #endregion

        #region Load

        /// <summary>
        /// Load data from storage. Called by Manager.
        /// </summary>
        public void Load()
        {
            LoadAsync().Forget();
        }

        public async UniTask LoadAsync()
        {
            if (_saveService == null)
                return;

            _isLoading = true;

            try
            {
                var result = await _saveService.TryLoadAsync<TData>(StorageKey, CancellationToken.None);
                ApplyLoadResult(result);
            }
            catch (Exception ex)
            {
                LoadStatus = SaveLoadStatus.ReadFailed;
                _canSave = false;
                this.LogError($"Load failed for {StorageKey}: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
                _warnedChangeWhileLoading = false;
                _loadedSource.TrySetResult();
            }
        }

        private void ApplyLoadResult(SaveLoadResult<TData> result)
        {
            LoadStatus = result.Status;
            _canSave = result.CanOverwrite;

            switch (result.Status)
            {
                case SaveLoadStatus.Loaded:
                    InjectWithoutNotify(result.Data);
                    break;
                case SaveLoadStatus.NotFound:
                    break; // First run: keep the defaults.
                case SaveLoadStatus.Corrupted when result.CanOverwrite:
                    this.LogError($"'{StorageKey}' was corrupted and is reset to defaults ({result.Error}).");
                    break;
                default:
                    this.LogError($"'{StorageKey}' could not be loaded ({result}). Saving is disabled for it so the " +
                                  "stored data is not overwritten.");
                    break;
            }

            _isDirty = false;
            _dirtySubject.OnNext(false);
        }

        private void InjectWithoutNotify(TData data)
        {
            var previous = _suppressDirty;
            _suppressDirty = true;
            try
            {
                InjectPayload(data);
            }
            finally
            {
                _suppressDirty = previous;
            }
        }

        /// <inheritdoc/>
        public UniTask WaitUntilLoadedAsync(CancellationToken cancellationToken = default)
        {
            return cancellationToken.CanBeCanceled
                ? _loadedSource.Task.AttachExternalCancellation(cancellationToken)
                : _loadedSource.Task;
        }

        #endregion

        /// <inheritdoc/>
        public abstract TData ExtractPayload();

        /// <inheritdoc/>
        public abstract void InjectPayload(TData data);

        // Explicit implementation for non-generic IPersistencePackage
        object IPersistencePackage.ExtractPayload() => ExtractPayload()!;

        void IPersistencePackage.InjectPayload(object data) => InjectPayload((TData)data);

        /// <summary>
        /// Runs <paramref name="loadAction"/> without marking the package dirty. Loading already does this around
        /// <see cref="InjectPayload(TData)"/>; use it for other bulk updates that must not trigger a save.
        /// </summary>
        protected void LoadWithoutNotify(Action loadAction)
        {
            var previous = _suppressDirty;
            _suppressDirty = true;
            try
            {
                loadAction();
            }
            finally
            {
                _suppressDirty = previous;
            }
        }

        /// <summary>
        /// Dispose the package.
        /// </summary>
        public virtual void Dispose()
        {
            _dirtySubject.OnNext(false);
            _dirtySubject.Dispose();
        }
    }
}
