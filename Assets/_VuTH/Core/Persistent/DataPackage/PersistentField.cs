using System;
using System.Collections.Generic;
using R3;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Smart data field wrapper that provides reactive properties.
    /// Uses ReactiveProperty (R3) to automatically notify subscribers when value changes.
    /// <para>
    /// An optional normalizer (e.g. <c>Mathf.Clamp01</c>) is applied to every value the field receives: the default,
    /// <see cref="Value"/>, and loaded data (<see cref="SetValueWithoutMarkingDirty"/>). A damaged or edited save can then
    /// never put the field out of range, and the generated payload mapping needs no special code.
    /// </para>
    /// <para>
    /// Subclasses can refuse values the game sets (<see cref="AcceptsValue"/>, e.g. a currency below zero). The
    /// normalizer is a constructor argument rather than a virtual method on purpose: the base constructor normalizes the
    /// default value, before a subclass's own fields are assigned.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type of data to store.</typeparam>
    public class PersistentField<T> : IDisposable
    {
        private readonly ReactiveProperty<T> _value;
        private readonly IPersistencePackage _ownerPackage;
        private readonly bool _notifyOnChange;
        private readonly Func<T, T> _normalize;
        private readonly IEqualityComparer<T> _comparer;

        /// <summary>
        /// Observable property for UI binding. Subscribe to automatically update UI when value changes.
        /// </summary>
        public ReadOnlyReactiveProperty<T> Observable { get; }

        /// <summary>
        /// Current value. Setting a different value marks the owner package as dirty; setting an equal value does
        /// nothing. The value is checked (<see cref="AcceptsValue"/>) then normalized first. After changing a mutable
        /// value in place (e.g. adding to a list), call <see cref="NotifyChanged"/>.
        /// </summary>
        public T Value
        {
            get => _value.Value;
            set => TrySetValue(value);
        }

        /// <summary>
        /// Sets the value like <see cref="Value"/>; returns false when the field refuses it (e.g. a currency going below
        /// zero with <see cref="RangeMode.Reject"/>), in which case the value is unchanged.
        /// </summary>
        public bool TrySetValue(T value)
        {
            if (!AcceptsValue(value)) return false;

            value = Normalize(value);
            if (_comparer.Equals(_value.Value, value)) return true;

            _value.Value = value;
            if (_notifyOnChange)
            {
                _ownerPackage?.MarkDirty();
            }

            return true;
        }

        /// <summary>
        /// Whether a value set by the game (<see cref="Value"/>, <see cref="TrySetValue"/>) is accepted. Not called for
        /// the default nor for loaded data, which are normalized instead. Never called from the constructor.
        /// </summary>
        protected virtual bool AcceptsValue(T value) => true;

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
        /// <param name="defaultValue">Initial value (normalized too).</param>
        /// <param name="notifyOnChange">Whether to auto-mark dirty when value changes.</param>
        /// <param name="normalize">
        /// Applied to every value the field receives, e.g. <c>Mathf.Clamp01</c> for a volume. Null: values are kept as is.
        /// </param>
        /// <param name="comparer">Decides whether a new value is a change. Default: <see cref="EqualityComparer{T}.Default"/>.</param>
        public PersistentField(IPersistencePackage owner, T defaultValue = default, bool notifyOnChange = true,
            Func<T, T> normalize = null, IEqualityComparer<T> comparer = null)
        {
            _ownerPackage = owner;
            _notifyOnChange = notifyOnChange;
            _normalize = normalize;
            _comparer = comparer ?? EqualityComparer<T>.Default;
            _value = new ReactiveProperty<T>(Normalize(defaultValue), _comparer);
            Observable = _value.ToReadOnlyReactiveProperty();

            // Disposed together with its package: no need to dispose every field by hand.
            (owner as IPersistentFieldOwner)?.RegisterField(this);
        }

        /// <summary>
        /// Sets the value (normalized) without marking the package dirty (no save), e.g. when injecting loaded data.
        /// Subscribers ARE notified, so the UI shows the loaded value.
        /// </summary>
        public void SetValueWithoutMarkingDirty(T value)
        {
            _value.Value = Normalize(value);
        }

        [Obsolete("Subscribers are notified; only the save is skipped. Use SetValueWithoutMarkingDirty.")]
        public void SetValueWithoutNotify(T value) => SetValueWithoutMarkingDirty(value);

        /// <summary>
        /// Subscribe to value changes.
        /// Returns IDisposable — caller MUST dispose when done to prevent leaks.
        /// </summary>
        public IDisposable Subscribe(Action<T> onNext)
        {
            return _value.Subscribe(onNext);
        }

        private T Normalize(T value) => _normalize != null ? _normalize(value) : value;

        public void Dispose()
        {
            _value?.Dispose();
            Observable?.Dispose();
        }
    }
}
