using System;
using System.Linq;
using System.Reflection;
using _VuTH.Common.Log;
using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using MessagePipe;
using UnityEngine;
using UnityEngine.SceneManagement;
#if VCONTAINER
using VContainer;
#endif

// ReSharper disable once CheckNamespace
namespace _VuTH.Common.MessagePipe
{
    /// <summary>
    /// MessagePipe wiring for VuTH scopes.
    /// <list type="bullet">
    /// <item><b>Root scope</b>: <c>RegisterMessagePipe(options)</c> with <see cref="MessagePipeOptionsConfig"/> applied.
    /// Brokers for every message type come from MessagePipe's open-generic registration (VContainer ≥ 1.14), so any
    /// type works as a global event — no attribute, bake or registration needed.</item>
    /// <item><b>Scene scope</b>: a scene-local broker for each <c>[MessagePipeEvent(EventScope.Scene)]</c> type that
    /// applies to the scope's OWN scene (not the active scene: screens are loaded additively). Publishers and
    /// subscribers in that scene are isolated from other scenes.</item>
    /// <item><b>Without VContainer</b>: one global <see cref="GlobalMessagePipe"/> provider with a broker for every
    /// attributed type (the builtin container has no open generics; scenes are not isolated).</item>
    /// </list>
    /// </summary>
    public static class MessagePipeHelper
    {
        private static MessagePipeOptionsConfig _config;
        private static bool _configLoaded;

        /// <summary>Applies <paramref name="config"/> (or the Resources config when null) to <paramref name="options"/>.</summary>
        public static MessagePipeOptions ApplyConfiguredOptions(MessagePipeOptions options, MessagePipeOptionsConfig config = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            config ??= LoadConfig();
            if (config != null)
            {
                options.EnableCaptureStackTrace = config.enableCaptureStackTrace;
            }

            return options;
        }

        private static MessagePipeOptionsConfig LoadConfig()
        {
            if (!_configLoaded)
            {
                _config = Resources.Load<MessagePipeOptionsConfig>(MessagePipeConstants.OptionsConfigPath);
                _configLoaded = true;
            }

            return _config;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _config = null;
            _configLoaded = false;
#if !VCONTAINER
            _globalInitialized = false;
#endif
        }

#if VCONTAINER
        private static MethodInfo _registerBrokerMethod;
        private static MethodInfo _registerGuardMethod;

        /// <summary>
        /// Root scope: MessagePipe core services + open-generic brokers for every message type. Scene/Screen events get
        /// a guard instead (see <see cref="ScopedEventMisuse"/>): the root owns no broker for them, so resolving them
        /// anywhere outside their scopes is reported.
        /// </summary>
        public static void RegisterGlobalEvents(IContainerBuilder builder)
        {
            var config = LoadConfig();
            RegisterGlobalEvents(builder, config != null ? config.scopedEventMisuse : ScopedEventMisuse.Warn);
        }

        internal static void RegisterGlobalEvents(IContainerBuilder builder, ScopedEventMisuse mode)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.RegisterMessagePipe(options => ApplyConfiguredOptions(options));

            if (mode == ScopedEventMisuse.Ignore) return;

            foreach (var info in MessagePipeEventCatalog.All.Where(e => e.IsScoped))
            {
                RegisterGuard(builder, info, mode);
            }
        }

        /// <summary>Scene scope: isolated brokers for the Scene/Screen events owned by <paramref name="scene"/>.</summary>
        public static void RegisterSceneEvents(IContainerBuilder builder, Scene scene)
        {
            RegisterSceneEvents(builder, scene.name, MessagePipeScopeContext.ResolveScreens(scene));
        }

        /// <summary>
        /// Registers a scope-local broker for every Scene event matching <paramref name="sceneName"/> and every Screen
        /// event matching one of <paramref name="screenIds"/>. Returns how many were registered.
        /// </summary>
        public static int RegisterSceneEvents(IContainerBuilder builder, string sceneName,
            System.Collections.Generic.IReadOnlyCollection<string> screenIds = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            screenIds ??= Array.Empty<string>();
            var options = ApplyConfiguredOptions(new MessagePipeOptions());
            var count = 0;
            foreach (var info in MessagePipeEventCatalog.ForScope(sceneName, screenIds))
            {
                RegisterBroker(builder, info.Type, options);
                count++;
            }

            if (count > 0)
            {
                var screens = screenIds.Count > 0 ? $" (screens: {string.Join(", ", screenIds)})" : string.Empty;
                typeof(MessagePipeHelper).Log($"Registered {count} scoped MessagePipe broker(s) for scene \"{sceneName}\"{screens}.");
            }

            return count;
        }

        private static void RegisterBroker(IContainerBuilder builder, Type eventType, MessagePipeOptions options)
        {
            _registerBrokerMethod ??= typeof(global::MessagePipe.ContainerBuilderExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m =>
                    m.Name == nameof(global::MessagePipe.ContainerBuilderExtensions.RegisterMessageBroker) &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1 &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[1].ParameterType == typeof(MessagePipeOptions));

            _registerBrokerMethod.MakeGenericMethod(eventType).Invoke(null, new object[] { builder, options });
        }

        private static void RegisterGuard(IContainerBuilder builder, MessagePipeEventInfo info, ScopedEventMisuse mode)
        {
            _registerGuardMethod ??= typeof(MessagePipeHelper).GetMethod(nameof(RegisterGuardGeneric),
                BindingFlags.NonPublic | BindingFlags.Static);
            _registerGuardMethod!.MakeGenericMethod(info.Type).Invoke(null, new object[] { builder, info, mode });
        }

        /// <summary>
        /// Closed registrations that shadow the open-generic ones at the root: they report the misuse, then hand out a
        /// broker on the shared global core — exactly what the fallback used to do silently.
        /// </summary>
        private static void RegisterGuardGeneric<T>(IContainerBuilder builder, MessagePipeEventInfo info, ScopedEventMisuse mode)
        {
            var reported = false;

            void Report(string contract)
            {
                var where = info.Scope == Attributes.EventScope.Screen
                    ? $"screen \"{info.ScreenId}\""
                    : info.SceneName.Length > 0 ? $"scene \"{info.SceneName}\"" : "a scene scope";
                var message = $"{contract}<{typeof(T).Name}> was resolved outside its owning scope: {typeof(T).Name} is " +
                              $"[MessagePipeEvent({info.Scope})] and only {where} has its own broker. Using the shared global " +
                              "broker instead, so messages are NOT isolated. Resolve it from a scope in that scene/screen.";
                if (mode == ScopedEventMisuse.Error) throw new InvalidOperationException(message);
                if (reported) return;
                reported = true;
                typeof(MessagePipeHelper).LogWarning(message);
            }

            builder.Register<IPublisher<T>>(r =>
            {
                Report("IPublisher");
                return new MessageBroker<T>(r.Resolve<MessageBrokerCore<T>>(), r.Resolve<FilterAttachedMessageHandlerFactory>());
            }, Lifetime.Singleton);

            builder.Register<ISubscriber<T>>(r =>
            {
                Report("ISubscriber");
                return new MessageBroker<T>(r.Resolve<MessageBrokerCore<T>>(), r.Resolve<FilterAttachedMessageHandlerFactory>());
            }, Lifetime.Singleton);

            builder.Register<IAsyncPublisher<T>>(r =>
            {
                Report("IAsyncPublisher");
                return new AsyncMessageBroker<T>(r.Resolve<AsyncMessageBrokerCore<T>>(), r.Resolve<FilterAttachedAsyncMessageHandlerFactory>());
            }, Lifetime.Singleton);

            builder.Register<IAsyncSubscriber<T>>(r =>
            {
                Report("IAsyncSubscriber");
                return new AsyncMessageBroker<T>(r.Resolve<AsyncMessageBrokerCore<T>>(), r.Resolve<FilterAttachedAsyncMessageHandlerFactory>());
            }, Lifetime.Singleton);
        }
#else
        private static bool _globalInitialized;
        private static MethodInfo _addBrokerMethod;

        /// <summary>Builds the global provider once, with a broker for every attributed event type.</summary>
        public static void InitializeGlobalBroker()
        {
            if (_globalInitialized) return;

            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe(options => ApplyConfiguredOptions(options));

            _addBrokerMethod ??= typeof(BuiltinContainerBuilder)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == nameof(BuiltinContainerBuilder.AddMessageBroker) &&
                            m.IsGenericMethodDefinition &&
                            m.GetGenericArguments().Length == 1 &&
                            m.GetParameters().Length == 0);

            foreach (var info in MessagePipeEventCatalog.All)
            {
                _addBrokerMethod.MakeGenericMethod(info.Type).Invoke(builder, null);
            }

            GlobalMessagePipe.SetProvider(builder.BuildServiceProvider());
            _globalInitialized = true;
            typeof(MessagePipeHelper).Log(
                $"Initialized global MessagePipe broker (non-VContainer mode). {MessagePipeEventCatalog.All.Count} event(s) registered.");
        }

        public static IPublisher<T> GetPublisher<T>()
        {
            InitializeGlobalBroker();
            return GlobalMessagePipe.GetPublisher<T>();
        }

        public static ISubscriber<T> GetSubscriber<T>()
        {
            InitializeGlobalBroker();
            return GlobalMessagePipe.GetSubscriber<T>();
        }
#endif
    }
}
