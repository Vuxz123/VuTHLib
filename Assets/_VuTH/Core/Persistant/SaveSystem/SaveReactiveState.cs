using System;
using R3;

namespace _VuTH.Core.Persistant.SaveSystem
{
    /// <summary>
    /// Reactive state wrapper for save data using R3.
    /// Provides observable properties that notify subscribers when values change.
    /// Falls back to simple property if R3 is not available.
    /// </summary>
    /// <typeparam name="T">The type of data to store reactively.</typeparam>
    public class SaveReactiveState<T> : IDisposable where T : class
    {
        private readonly ReactiveProperty<T> _state;
        public ReadOnlyReactiveProperty<T> State { get; }

        public SaveReactiveState(T initialValue = null)
        {
            _state = new ReactiveProperty<T>(initialValue);
            State = _state.ToReadOnlyReactiveProperty();
        }

        public void SetValue(T value)
        {
            _state.Value = value;
        }

        public T Value => _state.Value;

        public void Dispose()
        {
            _state?.Dispose();
            State?.Dispose();
        }
    }

    /// <summary>
    /// Helper class for subscribing to save events reactively.
    /// </summary>
    public static class SaveReactiveExtensions
    {
        /// <summary>
        /// Creates a reactive state manager for a specific save key.
        /// </summary>
        public static SaveReactiveState<T> CreateReactiveState<T>(this ISaveService saveService, string key, T defaultValue = null)
            where T : class
        {
            return new SaveReactiveState<T>(defaultValue);
        }
    }
}
