using _VuTH.Common.Log;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using VuTH.Common.MessagePipe;

namespace _VuTH.Common.DI
{
    public class SceneScopeContainer : LifetimeScope
    {
        [Header("Configurators")]
        [SerializeReference] private MonoBehaviour[] configurators;

        /// <summary>
        /// Configures the container builder for the scene scope.
        /// </summary>
        /// <param name="builder"> The container builder to configure.</param>
        protected override void Configure(IContainerBuilder builder)
        {
            // Check if Parent is assigned
            if (Parent)
            {
                this.Log($"{gameObject.name} linked to Parent: {Parent.name}");
            }
            else
            {
                this.LogWarning($"{gameObject.name} has NO Parent! Check ScreenManager EnqueueParent logic.");
            }
            
            base.Configure(builder);
            
            // Register scene-scoped MessagePipe events
#if VCONTAINER
            MessagePipeHelper.RegisterSceneEvents(builder);
#endif
            
            // Apply configurators
            foreach (var configurator in configurators)
            {
                if (configurator is IVContainerConfigurator vContainerConfigurator)
                {
                    vContainerConfigurator.Configure(builder);
                }
            }
        }
    }
}