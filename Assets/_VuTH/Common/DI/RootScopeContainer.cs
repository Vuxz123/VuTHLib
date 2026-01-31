using UnityEngine;
using VContainer;
using VContainer.Unity;
using VuTH.Common.MessagePipe;
using ZLinq;

namespace _VuTH.Common.DI
{
    public class RootScopeContainer : LifetimeScope
    {
        /// <summary>
        /// Configure the root scope container
        /// </summary>
        /// <param name="builder"> The container builder</param>
        protected override void Configure(IContainerBuilder builder)
        {
            // Register global-scoped MessagePipe events first
            MessagePipeHelper.RegisterGlobalEvents(builder);
            
            // Bootstrap configurators
            var boostrap = 
                FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .AsValueEnumerable().OfType<IBootstrapVContainerConfigurator>();
            foreach (var b in boostrap)
            {
                b.ConfigureRootScope(builder);
            }
        }
    }
}