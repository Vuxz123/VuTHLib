#nullable enable
using _VuTH.Common.DI;
using _VuTH.Core.Persistant.SaveSystem.Backend;
using _VuTH.Core.Persistant.SaveSystem.Events;
using _VuTH.Core.Persistant.SaveSystem.Serialize;
using _VuTH.Core.Persistant.SaveSystem.Encrypt;
using MessagePipe;
using VContainer;

namespace _VuTH.Core.Persistant.SaveSystem
{
    /// <summary>
    /// VContainer configurator for SaveService.
    /// Registers dependencies based on UNITY_EDITOR condition.
    /// </summary>
    public class SaveServiceVContainerConfigurator : IBootstrapVContainerConfigurator
    {
        public void ConfigureRootScope(IContainerBuilder builder)
        {
            // Register backend based on UNITY_EDITOR condition
#if UNITY_EDITOR
            // In editor, use JsonFile backend for easier debugging
            builder.Register<JsonFileSaveBackend>(Lifetime.Singleton)
                .As<ISaveBackend>();
#else
            // In build, use PlayerPrefs by default
            builder.Register<PlayerPrefsSaveBackend>(Lifetime.Singleton)
                .As<ISaveBackend>();
#endif
            // Override: Use JsonFile backend in both editor and build (optional)
            // builder.Register<JsonFileSaveBackend>(Lifetime.Singleton)
            //     .As<ISaveBackend>();

            // Register serializer
            builder.Register<JsonSerializer>(Lifetime.Singleton)
                .As<ISerializer>();

            // Register encryptor
            builder.Register<XorEncryptor>(Lifetime.Singleton)
                .As<IEncryptor>();

            // Register event publisher (MessagePipe)
            // IPublisher<SaveEvent> is registered by MessagePipe's message pipe builder
            builder.Register<MessagePipeSaveEventPublisher>(Lifetime.Singleton)
                .As<ISaveEventPublisher>();
        }
    }
}
