using System;
using System.Collections.Generic;

namespace _VuTH.Core.Pool
{
    /// <summary>
    /// Pending delayed despawns. Each entry remembers the <see cref="PooledInstance.Generation"/> it was scheduled for:
    /// once the instance is despawned (by hand or by an earlier schedule) and spawned again, the entry no longer applies
    /// and is dropped instead of despawning an instance now used elsewhere. Time is passed in explicitly so the queue
    /// can be unit tested.
    /// </summary>
    public sealed class ScheduledDespawnQueue
    {
        private readonly List<Entry> _entries = new();

        /// <summary>Number of pending entries.</summary>
        public int Count => _entries.Count;

        /// <summary>Schedules the current life of <paramref name="instance"/> to be despawned at <paramref name="despawnTime"/>.</summary>
        public void Schedule(PooledInstance instance, float despawnTime)
        {
            _entries.Add(new Entry(instance, instance.Generation, despawnTime));
        }

        /// <summary>
        /// Drops the entries that no longer apply (instance destroyed, despawned or respawned since), then passes every
        /// due entry's instance to <paramref name="despawn"/>.
        /// </summary>
        public void Process(float currentTime, Action<PooledInstance> despawn)
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                // The despawn callback may change the list: re-check the bounds.
                if (i >= _entries.Count) continue;

                var entry = _entries[i];
                if (!IsCurrent(entry))
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                if (currentTime < entry.DespawnTime) continue;

                _entries.RemoveAt(i);
                despawn(entry.Instance);
            }
        }

        /// <summary>Drops all pending entries without despawning them.</summary>
        public void Clear() => _entries.Clear();

        private static bool IsCurrent(Entry entry) =>
            entry.Instance.IsAlive && entry.Instance.IsActive && entry.Instance.Generation == entry.Generation;

        private readonly struct Entry
        {
            public readonly PooledInstance Instance;
            public readonly int Generation;
            public readonly float DespawnTime;

            public Entry(PooledInstance instance, int generation, float despawnTime)
            {
                Instance = instance;
                Generation = generation;
                DespawnTime = despawnTime;
            }
        }
    }
}
