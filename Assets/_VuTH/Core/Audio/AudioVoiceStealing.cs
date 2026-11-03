using System.Collections.Generic;

namespace _VuTH.Core.Audio
{
    /// <summary>
    /// Chooses which one-shot to cut when the pool is exhausted. Uses Unity's priority convention
    /// (0 = most important, 256 = least important).
    /// <list type="bullet">
    /// <item>Only non-music playbacks are candidates.</item>
    /// <item>The victim is the least important playback; ties go to the one that started first.</item>
    /// <item>Nothing is stolen when every active sound is more important than the new one (the new one is dropped).</item>
    /// </list>
    /// </summary>
    public static class AudioVoiceStealing
    {
        public static AudioPlaybackEntry SelectVictim(IEnumerable<AudioPlaybackEntry> active, int newPriority)
        {
            AudioPlaybackEntry victim = null;
            foreach (var entry in active)
            {
                if (entry == null || entry.IsMusic) continue;

                if (victim == null ||
                    entry.Priority > victim.Priority ||
                    (entry.Priority == victim.Priority && entry.StartedAt < victim.StartedAt))
                {
                    victim = entry;
                }
            }

            return victim != null && victim.Priority >= newPriority ? victim : null;
        }
    }
}
