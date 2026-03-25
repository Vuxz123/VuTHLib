using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using _VuTH.Core.Persistant.SaveSystem;

namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Base class for persistence packages.
    /// Acts as an Aggregator that groups multiple PersistentField and handles save/load logic.
    /// Uses Debounce from R3 to optimize I/O operations.
    /// </summary>
    /// <typeparam name="TData">DTO type for serialization.</typeparam>
    public abstract class PersistencePackage<TData> : IPersistencePackage<TData>, IDisposable 
        where TData : class
    {
        /// <summary>
        /// Default debounce time in seconds for Debounced strategy.
        /// </summary>
        protected virtual float DebounceSeconds => 3.0f;
        
        private readonly List<PersistentFieldBase> _fields = new();
        private readonly CompositeDisposable _disposables = new();
        private IDisposable? _saveSubscription;
        private ISaveService? _saveService;
        private bool _isDirty;
        private bool _isLoading;
        
        /// <inheritdoc/>
        public string StorageKey { get; }
        
        /// <inheritdoc/>
        public SaveStrategy Strategy { get; }
        
        /// <inheritdoc/>
        public bool IsDirty => _isDirty;

        protected PersistencePackage(string storageKey, SaveStrategy strategy)
        {
            StorageKey = storageKey;
            Strategy = strategy;
        }
        
        /// <summary>
        /// Initialize the package with save service. Call this after construction.
        /// </summary>
        public void Initialize(ISaveService saveService)
        {
            _saveService = saveService;
            SetupSavePipeline();
        }
        
        /// <summary>
        /// Register a persistent field to this package.
        /// </summary>
        protected void RegisterField(PersistentFieldBase field)
        {
            _fields.Add(field);
        }
        
        /// <summary>
        /// Setup the save pipeline based on strategy.
        /// </summary>
        private void SetupSavePipeline()
        {
            // Create observable that tracks dirty state
            var dirtyObservable = Observable.EveryValueChanged(this, x => x.IsDirty);

            Func<bool, bool> predicate = dirty => dirty && !_isLoading;
            Action<bool> onNext = _ => SaveNow();
            switch (Strategy)
            {
                case SaveStrategy.Immediate:
                    // Save immediately when dirty
                    _saveSubscription = dirtyObservable
                        .Where(predicate)
                        .Subscribe(onNext);
                    break;
                    
                case SaveStrategy.Debounced:
                    // Debounce saves - wait for stable state
                    _saveSubscription = dirtyObservable
                        .Where(predicate)
                        .ThrottleFirst(TimeSpan.FromSeconds(DebounceSeconds))
                        .Subscribe(onNext);
                    break;
                    
                case SaveStrategy.ManualOnly:
                case SaveStrategy.OnAppClose:
                    // No auto-save, manual only
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            _saveSubscription?.AddTo(_disposables);
        }
        
        /// <inheritdoc/>
        public virtual void MarkDirty()
        {
            _isDirty = true;
        }
        
        /// <inheritdoc/>
        public virtual void SaveNow()
        {
            if (_saveService == null || !_isDirty)
                return;
                
            var payload = ExtractPayload();
            _ = SaveInternalAsync(payload);
        }
        
        private async UniTask SaveInternalAsync(TData payload)
        {
            try
            {
                await _saveService!.SaveAsync(StorageKey, payload, CancellationToken.None);
                _isDirty = false;
                UnityEngine.Debug.Log($"[PersistencePackage] Saved {StorageKey}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[PersistencePackage] Save failed for {StorageKey}: {ex.Message}");
            }
        }
        
        /// <inheritdoc/>
        public virtual void Load()
        {
            if (_saveService == null)
                return;
                
            _isLoading = true;
            
            try
            {
                // Use LoadAsync with default to get existing or default data
                var loadedData = _saveService.LoadAsync(StorageKey, ExtractPayload(), CancellationToken.None)
                    .GetAwaiter().GetResult();

                if (loadedData != null)
                {
                    InjectPayload(loadedData);
                }
                
                _isDirty = false;
                UnityEngine.Debug.Log($"[PersistencePackage] Loaded {StorageKey}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[PersistencePackage] Load failed for {StorageKey}: {ex.Message}");
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
        
        /// <summary>
        /// Load without triggering auto-save. Used during InjectPayload.
        /// </summary>
        protected void LoadWithoutNotify(Action loadAction)
        {
            var wasLoading = _isLoading;
            _isLoading = true;
            
            loadAction();
            
            _isLoading = wasLoading;
            _isDirty = false;
        }
        
        public virtual void Dispose()
        {
            _saveSubscription?.Dispose();
            
            foreach (var field in _fields)
            {
                field.Dispose();
            }
            _fields.Clear();
            
            _disposables.Dispose();
        }
    }
    
    /// <summary>
    /// Non-generic base for field registration.
    /// </summary>
    public abstract class PersistentFieldBase : IDisposable
    {
        public abstract void Dispose();
    }
    
    /// <summary>
    /// Typed field wrapper for internal use.
    /// </summary>
    public class PersistentFieldTyped<T> : PersistentFieldBase
    {
        private readonly PersistentField<T> _field;
        
        public PersistentField<T> Field => _field;
        public T Value
        {
            get => _field.Value;
            set => _field.Value = value;
        }
        
        public PersistentFieldTyped(PersistentField<T> field)
        {
            _field = field;
        }
        
        public override void Dispose()
        {
            _field.Dispose();
        }
    }
}
