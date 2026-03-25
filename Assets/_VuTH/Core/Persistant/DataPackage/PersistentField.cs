using System;
using R3;

namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Smart data field wrapper that provides reactive properties.
    /// Uses ReactiveProperty (R3) to automatically notify subscribers when value changes.
    /// </summary>
    /// <typeparam name="T">The type of data to store.</typeparam>
    public class PersistentField<T> : IDisposable
    {
        private readonly ReactiveProperty<T> _value;
        private readonly IPersistencePackage _ownerPackage;
        private readonly bool _notifyOnChange;
        
        /// <summary>
        /// Observable property for UI binding. Subscribe to automatically update UI when value changes.
        /// </summary>
        public ReadOnlyReactiveProperty<T> Observable { get; }
        
        /// <summary>
        /// Current value. Setting this will mark the owner package as dirty.
        /// </summary>
        public T Value
        {
            get => _value.Value;
            set
            {
                _value.Value = value;
                if (_notifyOnChange)
                {
                    _ownerPackage?.MarkDirty();
                }
            }
        }
        
        /// <summary>
        /// Creates a new PersistentField.
        /// </summary>
        /// <param name="owner">The package that owns this field.</param>
        /// <param name="defaultValue">Initial value.</param>
        /// <param name="notifyOnChange">Whether to auto-mark dirty when value changes.</param>
        public PersistentField(IPersistencePackage owner, T defaultValue = default, bool notifyOnChange = true)
        {
            _ownerPackage = owner;
            _notifyOnChange = notifyOnChange;
            _value = new ReactiveProperty<T>(defaultValue);
            Observable = _value.ToReadOnlyReactiveProperty();
        }
        
        /// <summary>
        /// Set value without triggering auto-save (used during data loading).
        /// </summary>
        public void SetValueWithoutNotify(T value)
        {
            _value.Value = value;
        }
        
        /// <summary>
        /// Subscribe to value changes.
        /// IMPORTANT: Always add .AddTo(disposable) to prevent memory leaks!
        /// </summary>
        public Observable<T> Subscribe(Action<T> onNext)
        {
            _value.Subscribe(onNext);
            return _value;
        }
        
        public void Dispose()
        {
            _value?.Dispose();
            Observable?.Dispose();
        }
    }
}
