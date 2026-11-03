using System;
using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.Audio
{
    /// <summary>
    /// Pure ducking state: per channel, a set of active duck requests (volume factors). The channel's target factor is
    /// the lowest requested factor (1 when none), and the current factor moves towards it over the fade duration of the
    /// latest change. <see cref="AudioManager"/> ticks it every frame and multiplies channel volumes by
    /// <see cref="GetFactor"/>.
    /// </summary>
    public sealed class AudioDuckController
    {
        private sealed class ChannelState
        {
            public readonly Dictionary<int, float> Requests = new();
            public float Current = 1f;
            public float Speed = float.PositiveInfinity; // factor units per second
        }

        private readonly Dictionary<AudioChannel, ChannelState> _channels = new();
        private int _nextId = 1;

        /// <summary>Adds a duck request and returns its id (pass it to <see cref="Remove"/>).</summary>
        public int Add(AudioChannel channel, float volumeFactor, float fadeDuration)
        {
            var state = GetState(channel);
            var id = _nextId++;
            state.Requests[id] = Mathf.Clamp01(volumeFactor);
            SetSpeed(state, fadeDuration);
            return id;
        }

        /// <summary>Removes a duck request; the channel then fades back over <paramref name="fadeDuration"/>.</summary>
        public bool Remove(int id, float fadeDuration)
        {
            foreach (var state in _channels.Values)
            {
                if (!state.Requests.Remove(id)) continue;
                SetSpeed(state, fadeDuration);
                return true;
            }

            return false;
        }

        public float GetTarget(AudioChannel channel)
        {
            if (!_channels.TryGetValue(channel, out var state) || state.Requests.Count == 0) return 1f;

            var min = 1f;
            foreach (var factor in state.Requests.Values) min = Mathf.Min(min, factor);
            return min;
        }

        public float GetFactor(AudioChannel channel)
        {
            return _channels.TryGetValue(channel, out var state) ? state.Current : 1f;
        }

        /// <summary>Advances every channel towards its target. Returns true when any factor changed.</summary>
        public bool Tick(float deltaTime)
        {
            var changed = false;
            foreach (var pair in _channels)
            {
                var state = pair.Value;
                var target = GetTarget(pair.Key);
                if (Mathf.Approximately(state.Current, target))
                {
                    state.Current = target;
                    continue;
                }

                var step = float.IsPositiveInfinity(state.Speed) ? float.PositiveInfinity : state.Speed * Mathf.Max(0f, deltaTime);
                state.Current = Mathf.MoveTowards(state.Current, target, step);
                changed = true;
            }

            return changed;
        }

        public void Clear()
        {
            _channels.Clear();
        }

        private ChannelState GetState(AudioChannel channel)
        {
            if (!_channels.TryGetValue(channel, out var state))
            {
                state = new ChannelState();
                _channels[channel] = state;
            }

            return state;
        }

        private static void SetSpeed(ChannelState state, float fadeDuration)
        {
            state.Speed = fadeDuration > 0f ? 1f / fadeDuration : float.PositiveInfinity;
        }
    }

    /// <summary>
    /// Active duck request returned by <see cref="IAudioManager.Duck"/>. Dispose it to restore the channel volume.
    /// Disposing twice is safe.
    /// </summary>
    public sealed class AudioDuckHandle : IDisposable
    {
        private readonly Action<int, float> _release;
        private readonly int _id;
        private readonly float _releaseFade;
        private bool _disposed;

        internal AudioDuckHandle(Action<int, float> release, int id, float releaseFade)
        {
            _release = release;
            _id = id;
            _releaseFade = releaseFade;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _release?.Invoke(_id, _releaseFade);
        }
    }
}
