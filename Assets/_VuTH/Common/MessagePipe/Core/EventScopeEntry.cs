using System;
using _VuTH.Common.MessagePipe.Attributes;
using UnityEngine;

namespace _VuTH.Common.MessagePipe.Core
{
    /// <summary>
    /// Serializable entry for a single event type and its scope.
    /// </summary>
    [Serializable]
    public class EventScopeEntry
    {
        [Tooltip("Assembly-qualified type name for runtime resolution")]
        public string typeFullName;

        [Tooltip("Event scope (Global or Scene)")]
        public EventScope scope;

        [Tooltip("Scene name for Scene-scoped events; empty for Global events")]
        [SerializeField] public string sceneName;

        [Tooltip("Whether to also register an AsyncBroker for this event type")]
        [SerializeField] public bool registerAsyncBroker;
    }
}
