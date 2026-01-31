using System;

namespace _VuTH.Common.MessagePipe.Attributes
{
    /// <summary>
    /// Attribute to mark event types for automatic MessagePipe registration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class MessagePipeEventAttribute : Attribute
    {
        public EventScope Scope { get; set; } = EventScope.Global;
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
