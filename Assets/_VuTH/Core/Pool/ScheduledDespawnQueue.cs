using System;
using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.Pool
{
    /// <summary>
    /// Pending delayed despawns. Stores (object, due time) pairs and, when processed with the current time,
    /// hands every due object to a callback. Entries whose object was destroyed are dropped silently.
    /// Time is passed in explicitly so the queue does not depend on <see cref="Time"/> and can be unit tested.
    /// </summary>
    public sealed class ScheduledDespawnQueue
    {
        private readonly List<ScheduledDespawn> _entries = new();

        /// <summary>Number of pending entries.</summary>
        public int Count => _entries.Count;

        /// <summary>Schedules <paramref name="obj"/> to be despawned once the time reaches <paramref name="despawnTime"/>.</summary>
        public void Schedule(GameObject obj, float despawnTime)
        {
            _entries.Add(new ScheduledDespawn
            {
                Object = obj,
                DespawnTime = despawnTime
            });
        }

        /// <summary>
        /// Walks the entries from newest to oldest: destroyed objects are removed, and objects whose due time is
        /// <c>&lt;= <paramref name="currentTime"/></c> are passed to <paramref name="despawn"/> and then removed.
        /// </summary>
        public void Process(float currentTime, Action<GameObject> despawn)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var scheduled = _entries[i];

                if (scheduled.Object == null)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                if (currentTime >= scheduled.DespawnTime)
                {
                    despawn(scheduled.Object);
                    _entries.RemoveAt(i);
                }
            }
        }

        /// <summary>Drops all pending entries without despawning them.</summary>
        public void Clear() => _entries.Clear();

        private struct ScheduledDespawn
        {
            public GameObject Object;
            public float DespawnTime;
        }
    }
}
