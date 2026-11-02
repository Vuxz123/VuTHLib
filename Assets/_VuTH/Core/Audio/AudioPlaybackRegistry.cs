using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.Audio
{
    /// <summary>
    /// Tracks active playbacks for <see cref="AudioManager"/>: allocates playback ids (starting at 1, never reused,
    /// not reset by <see cref="Clear"/>) and maintains the id-to-entry and source-to-id lookups.
    /// It does not stop sources or cancel tokens; callers own that. Main-thread only.
    /// </summary>
    public sealed class AudioPlaybackRegistry
    {
        private readonly Dictionary<int, AudioPlaybackEntry> _entries = new();
        private readonly Dictionary<AudioSource, int> _sourceToPlayback = new();
        private int _nextPlaybackId = 1;

        /// <summary>Number of registered playbacks.</summary>
        public int Count => _entries.Count;

        /// <summary>All registered playback entries.</summary>
        public Dictionary<int, AudioPlaybackEntry>.ValueCollection Entries => _entries.Values;

        /// <summary>
        /// Registers a new playback on <paramref name="source"/> with the next playback id and returns its entry.
        /// The source is mapped to the new id, replacing any previous mapping.
        /// </summary>
        public AudioPlaybackEntry Register(AudioSource source, AudioChannel channel, float baseVolume, bool isMusic)
        {
            var playbackId = _nextPlaybackId++;
            var entry = new AudioPlaybackEntry(playbackId, source, channel, baseVolume, isMusic);
            _entries[playbackId] = entry;
            _sourceToPlayback[source] = playbackId;
            return entry;
        }

        /// <summary>True when a playback with <paramref name="playbackId"/> is registered.</summary>
        public bool Contains(int playbackId)
        {
            return _entries.ContainsKey(playbackId);
        }

        /// <summary>Looks up the entry registered under <paramref name="playbackId"/>.</summary>
        public bool TryGet(int playbackId, out AudioPlaybackEntry entry)
        {
            return _entries.TryGetValue(playbackId, out entry);
        }

        /// <summary>Looks up the playback id currently mapped to <paramref name="source"/>.</summary>
        public bool TryGetPlaybackId(AudioSource source, out int playbackId)
        {
            return _sourceToPlayback.TryGetValue(source, out playbackId);
        }

        /// <summary>Removes the entry for <paramref name="playbackId"/>, then the mapping for <paramref name="source"/>.</summary>
        public void Remove(int playbackId, AudioSource source)
        {
            _entries.Remove(playbackId);
            _sourceToPlayback.Remove(source);
        }

        /// <summary>Removes only the source-to-id mapping for <paramref name="source"/>.</summary>
        public void RemoveSource(AudioSource source)
        {
            _sourceToPlayback.Remove(source);
        }

        /// <summary>Removes all entries and mappings. The playback id counter keeps counting.</summary>
        public void Clear()
        {
            _entries.Clear();
            _sourceToPlayback.Clear();
        }
    }
}
