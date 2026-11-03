using System;
using JetBrains.Annotations;

namespace _VuTH.Common.MessagePipe.Attributes
{
    /// <summary>
    /// Marks a MessagePipe message type.
    /// <list type="bullet">
    /// <item><see cref="EventScope.Global"/> (default): optional with VContainer — every type already has a global
    /// broker (open generics). Mark it anyway so it is listed in the settings tab and works without VContainer.</item>
    /// <item><see cref="EventScope.Scene"/>: each scene scope gets its OWN broker, so publishers/subscribers in one scene
    /// never see messages from another. Second argument = scene name (empty = every scene).</item>
    /// <item><see cref="EventScope.Screen"/>: bound to a SCREEN instead of a scene name — pass the ScreenIdentifier name,
    /// ideally the generated constant (<c>ScreenIds.Names.Home</c>) so a rename is a compile error. Every scene scope
    /// belonging to that screen (main + additive scenes) gets its own broker.</item>
    /// </list>
    /// Resolving the publisher/subscriber of a Scene/Screen event from a scope that has no broker for it logs a warning
    /// (it would silently fall back to the shared global broker).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class MessagePipeEventAttribute : Attribute
    {
        /// <summary>Scope of the event.</summary>
        public EventScope Scope { get; private set; }

        /// <summary>
        /// Second argument: the scene name for <see cref="EventScope.Scene"/> (empty = every scene; the scope's own scene
        /// is matched, not the active scene) or the ScreenIdentifier name for <see cref="EventScope.Screen"/>.
        /// Ignored for Global events.
        /// </summary>
        [CanBeNull] public string SceneName { get; private set; }

        /// <summary>Same value as <see cref="SceneName"/>, named for <see cref="EventScope.Screen"/> events.</summary>
        [CanBeNull] public string ScreenId => Scope == EventScope.Screen ? SceneName : null;

        /// <summary>
        /// Kept for source compatibility. Async (and buffered) brokers are always registered together with the
        /// synchronous one by MessagePipe, so this has no effect.
        /// </summary>
        public bool RegisterAsyncBroker { get; private set; }

        public MessagePipeEventAttribute(
            EventScope scope = EventScope.Global,
            string sceneName = null,
            bool registerAsyncBroker = false
        )
        {
            Scope = scope;
            SceneName = sceneName;
            RegisterAsyncBroker = registerAsyncBroker;
        }
    }

    /// <summary>Where an event's broker lives.</summary>
    public enum EventScope
    {
        /// <summary>One broker shared by the whole application (root scope).</summary>
        Global = 0,

        /// <summary>One broker per scene scope (optionally only in a named scene).</summary>
        Scene = 1,

        /// <summary>One broker per scene scope belonging to the named screen (ScreenIdentifier).</summary>
        Screen = 2
    }
}
