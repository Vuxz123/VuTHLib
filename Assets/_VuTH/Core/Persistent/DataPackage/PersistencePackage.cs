#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;
using R3;
using _VuTH.Core.Persistent.SaveSystem;
using _VuTH.Core.Persistent.SaveSystem.Migrate;

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
    /// <para>
    /// Loads, saves and deletes of one package run one after another, never interleaved, so a load never reads a save
    /// that is still being written and two loads never mix.
    /// </para>
    /// </summary>
    /// <typeparam name="TData">DTO type for serialization.</typeparam>
    public abstract class PersistencePackage<TData> : IPersistencePackage<TData>, ISyncPersistencePackage,
        IPackageLoadFailure, IPersistentFieldOwner, IDisposable
        where TData : class
    {
        /// <summary>
        /// Save interval in seconds for the Debounced strategy.
        /// </summary>
        public virtual float DebounceSeconds => 3.0f;

        private readonly Subject<bool> _dirtySubject = new();
        private readonly SemaphoreSlim _operationLock = new(1, 1);
        // Result: null once loaded, or the reason the package cannot load. Never faulted, so no unobserved errors.
        private UniTaskCompletionSource<Exception?> _loadedSource = new();
        private ISaveService? _saveService;
        private SaveSchema? _schema;
        private bool _schemaCreated;
        private bool _isDirty;
        private int _pendingLoads;
        private bool _suppressDirty;
        private bool _isSaving;
        private bool _saveRequestedWhileSaving;
        private UniTaskCompletionSource? _saveLoopDone;
        private int _dirtyVersion;
        private bool _canSave = true;
        private bool _warnedChangeWhileLoading;
        private readonly List<Action> _deferredChanges = new();
        private readonly List<IDisposable> _fields = new();
        private int _batchDepth;
        private bool _batchDirty;
        private bool _disposed;

        /// <inheritdoc/>
        public event Action<IPersistencePackage>? SaveBlocked;

        /// <inheritdoc/>
        public string BaseStorageKey { get; }

        /// <inheritdoc/>
        public string StorageKey { get; private set; }

        /// <inheritdoc/>
        public virtual bool IsSlotScoped => false;

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

        private bool IsLoading => _pendingLoads > 0;

        protected PersistencePackage(string storageKey, SaveStrategy strategy)
        {
            if (string.IsNullOrEmpty(storageKey))
                throw new ArgumentException("The storage key cannot be empty.", nameof(storageKey));

            BaseStorageKey = storageKey;
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

        /// <inheritdoc/>
        public void SetSlot(string? slot)
        {
            if (!IsSlotScoped)
                throw new InvalidOperationException($"{GetType().Name} is not slot scoped (override IsSlotScoped).");

            var key = SaveSlots.GetKey(string.IsNullOrEmpty(slot) ? null : slot, BaseStorageKey);
            if (key == StorageKey) return;

            // The data in memory belongs to the previous slot: start the new one from defaults until it is loaded.
            StorageKey = key;
            InjectWithoutNotify(CreateDefaultPayload());
            LoadStatus = null;
            _canSave = true;
            ClearDirty();

            // Waiters must wait for the new slot's data, not see the previous load as done.
            if (_loadedSource.Task.Status != UniTaskStatus.Pending) _loadedSource = new UniTaskCompletionSource<Exception?>();
        }

        #region Schema

        /// <summary>
        /// Override to version this package's data on its own: return its current version and the migrators from older
        /// versions. Without it the package uses the service-wide schema version, shared by every package, so changing
        /// the format of this package would need a migrator for all the others.
        /// <para>
        /// Saves written before the package had a schema carry the service-wide version (1 by default): start the own
        /// version there, e.g. <c>new SaveSchema(2, new MyMigrator1To2())</c>. Called once.
        /// </para>
        /// </summary>
        protected virtual SaveSchema? CreateSchema() => null;

        private SaveSchema? Schema
        {
            get
            {
                if (_schemaCreated) return _schema;
                _schemaCreated = true;
                _schema = CreateSchema();
                return _schema;
            }
        }

        private IVersionedSaveService VersionedService => _saveService as IVersionedSaveService ??
            throw new InvalidOperationException(
                $"'{StorageKey}' declares its own schema, but {_saveService?.GetType().Name} does not implement " +
                $"{nameof(IVersionedSaveService)}.");

        private UniTask SaveToServiceAsync(TData payload)
        {
            var schema = Schema;
            if (schema == null) return _saveService!.SaveAsync(StorageKey, payload, CancellationToken.None);

            return VersionedService.SaveAsync(StorageKey, payload, schema, CancellationToken.None);
        }

        private UniTask<SaveLoadResult<TData>> LoadFromServiceAsync()
        {
            var schema = Schema;
            if (schema == null) return _saveService!.TryLoadAsync<TData>(StorageKey, CancellationToken.None);

            return VersionedService.TryLoadAsync<TData>(StorageKey, schema, CancellationToken.None);
        }

        #endregion

        /// <summary>
        /// Mark this package as dirty, notifying the Manager's save pipeline.
        /// </summary>
        public void MarkDirty()
        {
            if (_suppressDirty) return;

            if (IsLoading)
            {
                // The loaded data replaces the fields when the load completes: this change is lost. An error, not a
                // warning: for data such as purchases it is real data loss. Use Modify, or await WaitUntilLoadedAsync.
                if (!_warnedChangeWhileLoading)
                {
                    _warnedChangeWhileLoading = true;
                    this.LogError($"'{StorageKey}' changed while its data was loading: the change is LOST (the loaded " +
                                  "data replaces it). Wrap the change in Modify(...), which applies it after the load, " +
                                  "or await WaitUntilLoadedAsync first.");
                }

                return;
            }

            if (_batchDepth > 0)
            {
                _batchDirty = true; // Saved once, when the batch ends.
                return;
            }

            _dirtyVersion++;
            _isDirty = true;
            if (!_disposed) _dirtySubject.OnNext(true);
        }

        private void ClearDirty()
        {
            _isDirty = false;
            if (!_disposed) _dirtySubject.OnNext(false); // a save may finish after Dispose
        }

        /// <summary>
        /// Applies <paramref name="change"/> now, or right after the load in progress (a change made during a load
        /// would be replaced by the loaded data). Use it for changes that can come at any time, such as a purchase
        /// restored at startup.
        /// </summary>
        public void Modify(Action change)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));

            if (IsLoading)
            {
                _deferredChanges.Add(change);
                return;
            }

            change();
        }

        /// <summary>
        /// Groups several changes into one: the package is marked dirty (and, with <see cref="SaveStrategy.Immediate"/>,
        /// saved) once when the scope ends, never with half of the changes applied.
        /// <code>using (package.BatchChanges()) { exp.Value += 50; level.Value++; }</code>
        /// <para>
        /// Not a transaction: if the block throws, the changes made before the exception stay in memory and are saved
        /// when the scope is disposed. Validate first (e.g. <see cref="PersistentField{T}.TrySetValue"/>), then change.
        /// </para>
        /// </summary>
        public IDisposable BatchChanges()
        {
            _batchDepth++;
            return new BatchScope(this);
        }

        private void EndBatch()
        {
            if (_batchDepth == 0) return;
            if (--_batchDepth > 0 || !_batchDirty) return;

            _batchDirty = false;
            MarkDirty();
        }

        private sealed class BatchScope : IDisposable
        {
            private PersistencePackage<TData>? _package;

            public BatchScope(PersistencePackage<TData> package) => _package = package;

            public void Dispose()
            {
                _package?.EndBatch();
                _package = null;
            }
        }

        private void ApplyDeferredChanges()
        {
            if (_deferredChanges.Count == 0) return;

            var changes = _deferredChanges.ToArray();
            _deferredChanges.Clear();
            foreach (var change in changes)
            {
                try
                {
                    change();
                }
                catch (Exception ex)
                {
                    this.LogError($"A deferred change of '{StorageKey}' threw: {ex}");
                }
            }
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
            // While loading, the fields are about to be replaced by the loaded data: nothing worth writing.
            if (_saveService is not ISyncSaveService syncService || !_isDirty || IsLoading || !CheckCanSave())
                return false;

            var payload = ExtractPayload();
            var versionAtSaveStart = _dirtyVersion;

            try
            {
                var schema = Schema;
                var written = schema == null
                    ? syncService.TrySaveImmediate(StorageKey, payload)
                    : VersionedService.TrySaveImmediate(StorageKey, payload, schema);
                if (!written) return false;
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
                ClearDirty();
            }

            return true;
        }

        public async UniTask SaveNowAsync()
        {
            if (_disposed || _saveService == null || !_isDirty || !CheckCanSave())
                return;

            if (_isSaving)
            {
                // The running save loop writes once more with the newest data; complete when it is done.
                _saveRequestedWhileSaving = true;
                if (_saveLoopDone != null) await _saveLoopDone.Task;
                return;
            }

            _isSaving = true;
            var done = _saveLoopDone = new UniTaskCompletionSource();
            await _operationLock.WaitAsync();
            try
            {
                do
                {
                    _saveRequestedWhileSaving = false;

                    // Re-checked after waiting: a load or delete may have run meanwhile.
                    if (_saveService == null || !_isDirty || !_canSave) break;

                    var payload = ExtractPayload();
                    var versionAtSaveStart = _dirtyVersion;

                    try
                    {
                        await SaveToServiceAsync(payload);

                        if (_dirtyVersion == versionAtSaveStart)
                        {
                            ClearDirty();
                        }
                        else
                        {
                            _saveRequestedWhileSaving = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _isDirty = true;
                        this.LogError($"Save failed for {StorageKey}: {ex.Message}");
                        break;
                    }
                } while (_saveRequestedWhileSaving);
            }
            finally
            {
                _isSaving = false;
                _operationLock.Release();
                done.TrySetResult();
            }
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
            if (_disposed) return;

            if (_saveService == null)
            {
                const string error = "it has no save service. Register it with the DataPersistenceManager, or call " +
                                     "SetSaveService first.";
                this.LogError($"Cannot load '{StorageKey}': {error}");
                FailLoading(new InvalidOperationException($"'{StorageKey}' cannot load: {error}"));
                return;
            }

            // A previous attempt could not load: waiters from now on wait for this attempt.
            if (_loadedSource.Task.Status == UniTaskStatus.Succeeded && _loadedSource.Task.GetAwaiter().GetResult() != null)
            {
                _loadedSource = new UniTaskCompletionSource<Exception?>();
            }

            _pendingLoads++;
            await _operationLock.WaitAsync();
            try
            {
                var result = await LoadFromServiceAsync();
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
                _pendingLoads--;
                if (!IsLoading) _warnedChangeWhileLoading = false;
                _operationLock.Release();
                if (!IsLoading) ApplyDeferredChanges();
                _loadedSource.TrySetResult(null);
            }

            if (!_canSave) RaiseSaveBlocked();
        }

        private void RaiseSaveBlocked()
        {
            try
            {
                SaveBlocked?.Invoke(this);
            }
            catch (Exception ex)
            {
                this.LogError($"A SaveBlocked subscriber of '{StorageKey}' threw: {ex}");
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

            ClearDirty();
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
        public async UniTask WaitUntilLoadedAsync(CancellationToken cancellationToken = default)
        {
            var task = _loadedSource.Task;
            var error = cancellationToken.CanBeCanceled
                ? await task.AttachExternalCancellation(cancellationToken)
                : await task;
            if (error != null) throw error;
        }

        /// <summary>Releases waiters with <paramref name="error"/> when the package cannot load at all.</summary>
        private void FailLoading(Exception error)
        {
            _loadedSource.TrySetResult(error);
        }

        void IPackageLoadFailure.FailLoading(Exception error) => FailLoading(error);

        #endregion

        #region Delete

        /// <inheritdoc/>
        public async UniTask DeleteAsync()
        {
            if (_saveService == null)
                throw new InvalidOperationException($"Cannot delete '{StorageKey}': it has no save service.");

            // Created first: if the package cannot produce its defaults, nothing is deleted.
            var defaults = CreateDefaultPayload();

            await _operationLock.WaitAsync();
            try
            {
                await _saveService.DeleteAsync(StorageKey, CancellationToken.None);

                InjectWithoutNotify(defaults);
                LoadStatus = SaveLoadStatus.NotFound;
                _canSave = true; // Nothing stored any more: nothing can be overwritten.
                ClearDirty();
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// The data of a new player, injected by <see cref="DeleteAsync"/>. Default: a new <typeparamref name="TData"/>
        /// (its field initializers). Override when the package defaults differ from the DTO's.
        /// </summary>
        protected virtual TData CreateDefaultPayload()
        {
            try
            {
                return (TData)Activator.CreateInstance(typeof(TData), nonPublic: true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} cannot create a default {typeof(TData).Name} ({ex.Message}). " +
                    $"Override {nameof(CreateDefaultPayload)}.", ex);
            }
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

        void IPersistentFieldOwner.RegisterField(IDisposable field)
        {
            // A generated field created on first use after Dispose: nothing would dispose it any more.
            if (_disposed)
            {
                field.Dispose();
                return;
            }

            _fields.Add(field);
        }

        /// <summary>
        /// Disposes the package and its <see cref="PersistentField{T}"/>s. The operation lock is not disposed: a save
        /// still waiting for it must be able to finish.
        /// </summary>
        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var field in _fields) field.Dispose();
            _fields.Clear();
            _deferredChanges.Clear();

            _dirtySubject.OnNext(false);
            _dirtySubject.Dispose();
        }
    }

    /// <summary>A package owning <see cref="PersistentField{T}"/>s, disposed with it.</summary>
    internal interface IPersistentFieldOwner
    {
        void RegisterField(IDisposable field);
    }
}
