using UnityEngine;

namespace _VuTH.Common.MessagePipe.Configuration
{
    /// <summary>
    /// Optional options for the root MessagePipe registration (Resources/MessagePipe/MessagePipeOptionsConfig).
    /// Applied to <c>MessagePipeOptions</c> by <see cref="MessagePipeHelper.ApplyConfiguredOptions"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "MessagePipeOptionsConfig", menuName = "VuTH/MessagePipe/Options Config")]
    public class MessagePipeOptionsConfig : ScriptableObject
    {
        [Header("MessagePipe Options")]
        [Tooltip("Capture subscription stack traces (MessagePipe diagnostics window). Costs memory/CPU; editor/dev only.")]
        [SerializeField] public bool enableCaptureStackTrace = false;

        [Header("Scoped Events")]
        [Tooltip("What happens when the publisher/subscriber of a Scene/Screen event is resolved from a scope that has no " +
                 "broker for it (it falls back to the shared global broker, so scenes are NOT isolated).")]
        [SerializeField] public ScopedEventMisuse scopedEventMisuse = ScopedEventMisuse.Warn;
    }

    /// <summary>Reaction to resolving a Scene/Screen event outside the scopes that own it.</summary>
    public enum ScopedEventMisuse
    {
        /// <summary>No check (plain fallback to the global broker).</summary>
        Ignore = 0,

        /// <summary>Log a warning once per event type, then use the global broker.</summary>
        Warn = 1,

        /// <summary>Throw: the resolve (and the injection that needed it) fails.</summary>
        Error = 2,
    }
}
