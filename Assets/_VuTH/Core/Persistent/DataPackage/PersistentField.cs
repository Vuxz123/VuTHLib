using System;
using System.Collections.Generic;
using R3;

namespace _VuTH.Core.Persistent.DataPackage
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
        /// Current value. Setting a different value marks the owner package as dirty; setting an equal value does
        /// nothing. After changing a mutable value in place (e.g. adding to a list), call <see cref="NotifyChanged"/>.
        /// </summary>
        public T Value
        {
            get => _value.Value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value.Value, value)) return;

                _value.Value = value;
                if (_notifyOnChange)
                {
                    _ownerPackage?.MarkDirty();
                }
            }
        }

        /// <summary>
        /// Reports a change made inside the current value (e.g. an item added to a list): notifies subscribers and
        /// marks the owner package as dirty.
        /// </summary>
        public void NotifyChanged()
        {
            _value.ForceNotify();
            if (_notifyOnChange)
            {
                _ownerPackage?.MarkDirty();
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
        /// Returns IDisposable — caller MUST dispose when done to prevent leaks.
        /// </summary>
        public IDisposable Subscribe(Action<T> onNext)
        {
            return _value.Subscribe(onNext);
        }
        
        public void Dispose()
        {
            _value?.Dispose();
            Observable?.Dispose();
        }
    }
}
