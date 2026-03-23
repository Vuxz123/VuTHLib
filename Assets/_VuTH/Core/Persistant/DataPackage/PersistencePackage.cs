#nullable enable
using System;
using System.Threading;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;
using R3;
using _VuTH.Core.Persistant.SaveSystem;

namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Base class for persistence packages.
    /// Acts as a data container only — save logic is handled by DataPersistenceManager.
    /// Uses R3 Observable for dirty state notifications.
    /// </summary>
    /// <typeparam name="TData">DTO type for serialization.</typeparam>
    public abstract class PersistencePackage<TData> : IPersistencePackage<TData>, IDisposable 
        where TData : class
    {
        /// <summary>
        /// Default debounce time in seconds for Debounced strategy.
        /// </summary>
        public virtual float DebounceSeconds => 3.0f;
        
        private readonly Subject<bool> _dirtySubject = new();
        private ISaveService? _saveService;
        private bool _isDirty;
        private bool _isLoading;
        private bool _isSaving;
        private bool _saveRequestedWhileSaving;
        private int _dirtyVersion;
        
        /// <inheritdoc/>
        public string StorageKey { get; }
        
        /// <inheritdoc/>
        public SaveStrategy Strategy { get; }
        
        /// <inheritdoc/>
        public bool IsDirty => _isDirty;
        
        /// <inheritdoc/>
        public Observable<bool> DirtyObservable => _dirtySubject;

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
            if (_isLoading) return;
            _dirtyVersion++;
            _isDirty = true;
            _dirtySubject.OnNext(true);
        }
        
        /// <summary>
        /// Force save immediately. Called by Manager's save pipeline.
        /// </summary>
        public void SaveNow()
        {
            _ = SaveNowAsync();
        }
        
        public async UniTask SaveNowAsync()
        {
            if (_saveService == null || !_isDirty)
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

                    this.Log($"Saved {StorageKey}");
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
        
        /// <summary>
        /// Load data from storage. Called by Manager.
        /// </summary>
        public void Load()
        {
            if (_saveService == null)
                return;
                
            _isLoading = true;
            
            try
            {
                var loadedData = _saveService.LoadAsync(StorageKey, ExtractPayload(), CancellationToken.None)
                    .GetAwaiter().GetResult();

                if (loadedData != null)
                {
                    InjectPayload(loadedData);
                }
                
                _isDirty = false;
                _dirtySubject.OnNext(false);
                this.Log($"Loaded {StorageKey}");
            }
            catch (Exception ex)
            {
                this.LogError($"Load failed for {StorageKey}: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }
        
        /// <inheritdoc/>
        public abstract TData ExtractPayload();
        
        /// <inheritdoc/>
        public abstract void InjectPayload(TData data);
        
        // Explicit implementation for non-generic IPersistencePackage
        object IPersistencePackage.ExtractPayload() => ExtractPayload()!;
        
        void IPersistencePackage.InjectPayload(object data) => InjectPayload((TData)data);
        
        /// <summary>
        /// Load without triggering dirty notification. Used during InjectPayload.
        /// </summary>
        protected void LoadWithoutNotify(Action loadAction)
        {
            var wasLoading = _isLoading;
            _isLoading = true;
            
            loadAction();
            
            _isLoading = wasLoading;
            _isDirty = false;
        }
        
        /// <summary>
        /// Dispose the package.
        /// </summary>
        public virtual void Dispose()
        {
            _dirtySubject?.OnNext(false);
            _dirtySubject?.Dispose();
        }
    }
}
