using System;
using System.Linq;
using System.Reflection;
using _VuTH.Common.Log;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using MessagePipe;
using UnityEngine;
#if VCONTAINER
using VContainer;
using ContainerBuilderExtensions = MessagePipe.ContainerBuilderExtensions;
#endif

// ReSharper disable once CheckNamespace
namespace VuTH.Common.MessagePipe
{
    /// <summary>
    /// Helper class for MessagePipe registration and access.
    /// Handles both VContainer DI and global fallback modes.
    /// </summary>
    public static class MessagePipeHelper
    {
        private static EventScopeLookup _lookup;
        private static bool _lookupLoaded;

#if VCONTAINER
        // Cached reflection method for VContainer registration
        private static MethodInfo _registerBrokerMethod;
#else
        // Global broker instance for non-VContainer builds
        private static IServiceProvider _globalProvider;
        private static bool _globalInitialized;

        // Cached reflection method for BuiltinContainerBuilder
        private static MethodInfo _addBrokerMethod;
#endif

        /// <summary>
        /// Get the baked EventScopeLookup asset.
        /// </summary>
        public static EventScopeLookup Lookup
        {
            get
            {
                if (!_lookupLoaded)
                {
                    _lookup = Resources.Load<EventScopeLookup>(MessagePipeConstants.EventScopeLookupPath);
                    _lookupLoaded = true;

                    if (_lookup == null)
                    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        throw new InvalidOperationException(
                            $"EventScopeLookup not found at Resources/{MessagePipeConstants.EventScopeLookupPath}. " +
                            "Run VuTH/MessagePipe/Bake Event Scope Lookup in Editor.");
#else
                        typeof(MessagePipeHelper).LogWarning(
                            $"EventScopeLookup not found at Resources/{MessagePipeConstants.EventScopeLookupPath}. " +
                            "Run VuTH/MessagePipe/Bake Event Scope Lookup in Editor.");
#endif
                    }
                }
                return _lookup;
            }
        }

#if VCONTAINER
        /// <summary>
        /// Register all global-scoped MessagePipe events to the container builder.
        /// Call this from RootScopeContainer.
        /// Also registers MessagePipe core services (only once here).
        /// </summary>
        public static void RegisterGlobalEvents(IContainerBuilder builder)
        {
            var lookup = Lookup;

            // Register MessagePipe core services ONCE at root scope
            builder.RegisterMessagePipe();

            if (!lookup)
            {
                typeof(MessagePipeHelper).LogWarning("No EventScopeLookup found. Skipping global event registration.");
                return;
            }

            // Register global-scoped event brokers
            foreach (var entry in lookup.GetEntriesByScope(EventScope.Global))
            {
                var type = EventScopeLookup.ResolveType(entry);
                if (type == null)
                {
                    typeof(MessagePipeHelper).LogWarning($"Failed to resolve type: {entry.typeFullName}");
                    continue;
                }

                RegisterMessageBroker(builder, type);
            }

            typeof(MessagePipeHelper).Log($"Registered {CountEntries(lookup, EventScope.Global)} global MessagePipe event(s).");
        }

        /// <summary>
        /// Register all scene-scoped MessagePipe events to the container builder.
        /// Call this from SceneScopeContainer.
        /// NOTE: Does NOT call RegisterMessagePipe() - core services are inherited from parent scope.
        /// </summary>
        public static void RegisterSceneEvents(IContainerBuilder builder)
        {
            var lookup = Lookup;
            if (lookup == null)
            {
                typeof(MessagePipeHelper).LogWarning("No EventScopeLookup found. Skipping scene event registration.");
                return;
            }

            // NOTE: Do NOT call builder.RegisterMessagePipe() here!
            // Core services are already registered in RootScopeContainer and inherited via parent scope.
            // Only register scene-scoped event brokers here.

            foreach (var entry in lookup.GetEntriesByScope(EventScope.Scene))
            {
                var type = EventScopeLookup.ResolveType(entry);
                if (type == null)
                {
                    typeof(MessagePipeHelper).LogWarning($"Failed to resolve type: {entry.typeFullName}");
                    continue;
                }

                RegisterMessageBroker(builder, type);
            }

            typeof(MessagePipeHelper).Log($"Registered {CountEntries(lookup, EventScope.Scene)} scene MessagePipe event(s).");
        }

        private static void RegisterMessageBroker(IContainerBuilder builder, Type eventType)
        {
            // Cache MethodInfo once (lazy init)
            _registerBrokerMethod ??= typeof(ContainerBuilderExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m =>
                    m.Name == "RegisterMessageBroker" &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(IContainerBuilder));

            _registerBrokerMethod
                .MakeGenericMethod(eventType)
                .Invoke(null, new object[] { builder });
        }

        private static int CountEntries(EventScopeLookup lookup, EventScope scope)
        {
            int count = 0;
            foreach (var _ in lookup.GetEntriesByScope(scope))
                count++;
            return count;
        }
#else
        /// <summary>
        /// Initialize global MessagePipe broker for non-VContainer builds.
        /// Call this once at startup (e.g., from BootstrapManagerCentral).
        /// </summary>
        public static void InitializeGlobalBroker()
        {
            if (_globalInitialized)
                return;

            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();

            var lookup = Lookup;
            if (lookup != null)
            {
                // Cache MethodInfo once (lazy init)
                _addBrokerMethod ??= typeof(BuiltinContainerBuilder)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .First(m =>
                        m.Name == "AddMessageBroker" &&
                        m.IsGenericMethodDefinition &&
                        m.GetParameters().Length == 0);

                // Register ALL events as global (no scene scope without VContainer)
                foreach (var entry in lookup.Entries)
                {
                    var type = EventScopeLookup.ResolveType(entry);
                    if (type == null)
                    {
                        typeof(MessagePipeHelper).LogWarning($"Failed to resolve type: {entry.typeFullName}");
                        continue;
                    }

                    _addBrokerMethod
                        .MakeGenericMethod(type)
                        .Invoke(builder, null);
                }
            }

            _globalProvider = builder.BuildServiceProvider();
            GlobalMessagePipe.SetProvider(_globalProvider);
            _globalInitialized = true;

            typeof(MessagePipeHelper).Log($"Initialized global MessagePipe broker (non-VContainer mode). {lookup?.Entries.Count ?? 0} event(s) registered.");
        }

        /// <summary>
        /// Get a publisher for the specified event type (non-VContainer mode).
        /// </summary>
        public static IPublisher<T> GetPublisher<T>()
        {
            EnsureGlobalInitialized();
            return GlobalMessagePipe.GetPublisher<T>();
        }

        /// <summary>
        /// Get a subscriber for the specified event type (non-VContainer mode).
        /// </summary>
        public static ISubscriber<T> GetSubscriber<T>()
        {
            EnsureGlobalInitialized();
            return GlobalMessagePipe.GetSubscriber<T>();
        }

        private static void EnsureGlobalInitialized()
        {
            if (!_globalInitialized)
            {
                InitializeGlobalBroker();
            }
        }
#endif
    }
}
