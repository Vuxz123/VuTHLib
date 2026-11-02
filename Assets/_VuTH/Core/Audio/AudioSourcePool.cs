using System;
using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.Audio
{
    /// <summary>
    /// Bounded pool of one-shot <see cref="AudioSource"/>s. Sources are created on demand through a factory that
    /// receives the 1-based index of the new source; the pool remembers every source it created (owned) and which
    /// ones are currently available. Main-thread only.
    /// </summary>
    public sealed class AudioSourcePool
    {
        private readonly Queue<AudioSource> _available = new();
        private readonly List<AudioSource> _owned = new();
        private readonly Func<int, AudioSource> _createSource;

        /// <param name="createSource">Creates a new source; receives the 1-based index of the source being created.</param>
        public AudioSourcePool(Func<int, AudioSource> createSource)
        {
            _createSource = createSource ?? throw new ArgumentNullException(nameof(createSource));
        }

        /// <summary>Number of sources created by this pool (since the last <see cref="Clear"/>).</summary>
        public int OwnedCount => _owned.Count;

        /// <summary>Number of sources waiting in the pool.</summary>
        public int AvailableCount => _available.Count;

        /// <summary>Creates available sources until <see cref="OwnedCount"/> reaches <paramref name="targetCount"/>.</summary>
        public void Warm(int targetCount)
        {
            for (var i = _owned.Count; i < targetCount; i++)
            {
                var source = _createSource(i + 1);
                _owned.Add(source);
                _available.Enqueue(source);
            }
        }

        /// <summary>
        /// Returns an available source, or creates a new one while fewer than <paramref name="maxSources"/>
        /// are owned. Returns null when the pool is exhausted.
        /// </summary>
        public AudioSource Acquire(int maxSources)
        {
            if (_available.Count > 0)
            {
                return _available.Dequeue();
            }

            if (_owned.Count >= maxSources)
            {
                return null;
            }

            var source = _createSource(_owned.Count + 1);
            _owned.Add(source);
            return source;
        }

        /// <summary>Makes <paramref name="source"/> available again, unless it is already waiting in the pool.</summary>
        public void Return(AudioSource source)
        {
            if (!_available.Contains(source))
            {
                _available.Enqueue(source);
            }
        }

        /// <summary>Forgets all available and owned sources (does not destroy them).</summary>
        public void Clear()
        {
            _available.Clear();
            _owned.Clear();
        }
    }
}
