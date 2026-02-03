using System;
using System.Linq;
using System.Reflection;
using _VuTH.Common.Log;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using MessagePipe;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    /// Uses generated MessagePipeRegistrar for optimized registration when available.
    /// </summary>
    public static class MessagePipeHelper
    {
        private static EventScopeLookup _lookup;
        private static bool _lookupLoaded;

        // Cached reflection to generated registrar (Core.Generated.MessagePipeRegistrar)
        private static readonly Type RegistrarType;
        private static readonly MethodInfo RegisterGlobalMethod;
        private static readonly MethodInfo RegisterSceneMethod;
        private static bool _registrarChecked;

#if VCONTAINER
        // Cached reflection method for VContainer registration (fallback)
        private static MethodInfo _registerBrokerMethod;
#else
        // Global broker instance for non-VContainer builds
        private static IServiceProvider _globalProvider;
        private static bool _globalInitialized;

        // Cached reflection method for BuiltinContainerBuilder (fallback)
        private static MethodInfo _addBrokerMethod;

        // Cached reflection to generated registrar for non-VContainer
        private static MethodInfo _registrarBuiltinMethod;
#endif

        /// <summary>
        /// Get configured MessagePipeOptions based on the config asset.
        /// Uses reflection to safely apply options even if the property doesn't exist in this Unity version.
        /// Returns a new MessagePipeOptions instance with configured settings (never null).
        /// </summary>
        public static MessagePipeOptions GetConfiguredOptions()
        {
            // Load config from Resources
            var config = Resources.Load<MessagePipeOptionsConfig>(MessagePipeConstants.OptionsConfigPath);
            
            // Create default options
            var options = new MessagePipeOptions();
            
            // If no config found, return defaults
            if (config == null)
            {
                return options;
            }
            
            // Use reflection to safely apply options
            // This avoids compile errors if the property doesn't exist in the MessagePipe version
            try
            {
                // Try to set EnableCaptureStackTrace if it exists
                var enableCaptureStackTraceProperty = typeof(MessagePipeOptions).GetProperty("EnableCaptureStackTrace",
                    BindingFlags.Public | BindingFlags.Instance);
                
                if (enableCaptureStackTraceProperty != null && enableCaptureStackTraceProperty.PropertyType == typeof(bool))
                {
                    enableCaptureStackTraceProperty.SetValue(options, config.enableCaptureStackTrace);
                }
            }
            catch (System.Exception ex)
            {
                // Log warning but don't fail - options will use defaults
                typeof(MessagePipeHelper).LogWarning($"[MessagePipe] Failed to apply MessagePipeOptions config: {ex.Message}");
            }
            
            return options;
        }

        static MessagePipeHelper()
        {
            // Try to load the generated registrar via reflection
            // This is done once at static initialization to avoid per-call overhead
            RegistrarType = Type.GetType("Core.Generated.MessagePipeRegistrar, Assembly-CSharp", false, false);

            if (RegistrarType != null)
            {
#if VCONTAINER
                RegisterGlobalMethod = RegistrarType.GetMethod("RegisterGlobal", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(IContainerBuilder) }, null);
                RegisterSceneMethod = RegistrarType.GetMethod("RegisterScene", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(IContainerBuilder), typeof(string) }, null);
#else
                RegisterGlobalMethod = RegistrarType.GetMethod("RegisterGlobal", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(BuiltinContainerBuilder) }, null);
#endif
            }
        }

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
        /// Uses generated MessagePipeRegistrar for optimized registration when available.
        /// </summary>
        public static void RegisterGlobalEvents(IContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            // Try to use generated registrar first (zero runtime reflection overhead)
            if (RegisterGlobalMethod != null)
            {
                RegisterGlobalMethod.Invoke(null, new object[] { builder });
                typeof(MessagePipeHelper).Log("[MessagePipe] Registered global events via generated MessagePipeRegistrar.");
                return;
            }

            // Fallback: use lookup-based registration with reflection
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

            typeof(MessagePipeHelper).LogWarning("[MessagePipe] Using fallback lookup-based registration. " +
                "Consider running VuTH/MessagePipe/Bake Event Scope Lookup to generate MessagePipeRegistrar for optimized performance.");
        }

        /// <summary>
        /// Register all scene-scoped MessagePipe events to the container builder.
        /// Call this from SceneScopeContainer.
        /// NOTE: Does NOT call RegisterMessagePipe() - core services are inherited from parent scope.
        /// Uses generated MessagePipeRegistrar for optimized registration when available.
        /// </summary>
        public static void RegisterSceneEvents(IContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            // Get active scene name
            var activeScene = SceneManager.GetActiveScene();
            var activeSceneName = activeScene.name;

            // Try to use generated registrar first (zero runtime reflection overhead)
            if (RegisterSceneMethod != null)
            {
                RegisterSceneMethod.Invoke(null, new object[] { builder, activeSceneName });
                typeof(MessagePipeHelper).Log($"[MessagePipe] Registered scene events for \"{activeSceneName}\" via generated MessagePipeRegistrar.");
                return;
            }

            // Fallback: use lookup-based registration with reflection
            var lookup = Lookup;
            if (lookup == null)
            {
                typeof(MessagePipeHelper).LogWarning("No EventScopeLookup found. Skipping scene event registration.");
                return;
            }

            // NOTE: Do NOT call builder.RegisterMessagePipe() here!
            // Core services are already registered in RootScopeContainer and inherited via parent scope.
            // Only register scene-scoped event brokers here.

            int registeredCount = 0;
            foreach (var entry in lookup.GetEntriesByScope(EventScope.Scene))
            {
                // Validate SceneName
                if (string.IsNullOrEmpty(entry.sceneName))
                {
                    typeof(MessagePipeHelper).LogWarning(
                        $"[MessagePipe] Skipping scene-scoped event {entry.typeFullName}: SceneName is empty. " +
                        "Please specify a SceneName in the [MessagePipeEvent] attribute, e.g., [MessagePipeEvent(EventScope.Scene, \"MyScene\")].");
                    continue;
                }

                if (!string.Equals(entry.sceneName, activeSceneName, StringComparison.Ordinal))
                {
                    typeof(MessagePipeHelper).LogWarning(
                        $"[MessagePipe] Skipping scene-scoped event {entry.typeFullName}: SceneName \"{entry.sceneName}\" does not match active scene \"{activeSceneName}\".");
                    continue;
                }

                var type = EventScopeLookup.ResolveType(entry);
                if (type == null)
                {
                    typeof(MessagePipeHelper).LogWarning($"[MessagePipe] Failed to resolve type: {entry.typeFullName}");
                    continue;
                }

                RegisterMessageBroker(builder, type);
                registeredCount++;
            }

            typeof(MessagePipeHelper).Log($"[MessagePipe] Registered {registeredCount} scene MessagePipe event(s) for scene \"{activeSceneName}\".");
            typeof(MessagePipeHelper).LogWarning("[MessagePipe] Using fallback lookup-based registration. " +
                "Consider running VuTH/MessagePipe/Bake Event Scope Lookup to generate MessagePipeRegistrar for optimized performance.");
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
