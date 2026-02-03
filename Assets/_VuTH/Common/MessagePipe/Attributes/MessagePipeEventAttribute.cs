using System;
using JetBrains.Annotations;

namespace _VuTH.Common.MessagePipe.Attributes
{
    /// <summary>
    /// Attribute to mark event types for automatic MessagePipe registration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class MessagePipeEventAttribute : Attribute
    {
        /// <summary>
        /// Gets the scope of the event (Global or Scene).
        /// </summary>
        public EventScope Scope { get; private set; }

        /// <summary>
        /// Gets the scene name for Scene-scoped events. Required when Scope is EventScope.Scene.
        /// The event will only be registered in containers whose active scene name matches this value.
        /// Ignored for Global-scoped events.
        /// </summary>
        [CanBeNull] public string SceneName { get; private set; }

        /// <summary>
        /// Gets whether to also register an AsyncBroker for this event type.
        /// When true, both synchronous and asynchronous message brokers will be registered.
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

    /// <summary>
    /// Defines the scope of an event for MessagePipe registration.
    /// </summary>
    public enum EventScope
    {
        /// <summary>
        /// Event is global and accessible anywhere in the application.
        /// </summary>
        Global = 0,

        /// <summary>
        /// Event is scoped to a specific scene.
        /// </summary>
        Scene = 1
    }
}