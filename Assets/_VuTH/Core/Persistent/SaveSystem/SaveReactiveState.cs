using System;
using R3;

namespace _VuTH.Core.Persistent.SaveSystem
{
    /// <summary>
    /// Holds a value as an R3 reactive property, so subscribers are notified when it changes.
    /// It does not load or save anything by itself.
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
}
