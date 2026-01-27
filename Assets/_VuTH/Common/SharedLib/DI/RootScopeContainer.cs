using UnityEngine;
using VContainer;
using VContainer.Unity;
using ZLinq;

namespace _VuTH.Common.DI
{
    public class RootScopeContainer : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
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