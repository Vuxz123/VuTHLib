using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _VuTH.Common.DI
{
    /// <summary>
    /// Root (Core scene) LifetimeScope. Collects every <see cref="IBootstrapVContainerConfigurator"/> in the loaded
    /// scenes and lets it register into the root container:
    /// <list type="bullet">
    /// <item>in a deterministic order — <see cref="IRootScopeConfigurationOrder"/>, then scene and hierarchy path;</item>
    /// <item>skipping instances that opt out via <see cref="IRootScopeRegistrationFilter"/> (duplicate singletons).</item>
    /// </list>
    /// </summary>
    public class RootScopeContainer : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Module installers first (e.g. MessagePipe global brokers), then the configurators.
            ScopeInstallers.InstallRoot(builder);

            foreach (var configurator in CollectConfigurators())
            {
                configurator.ConfigureRootScope(builder);
            }
        }

        private static IEnumerable<IBootstrapVContainerConfigurator> CollectConfigurators()
        {
            return FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
                .Where(m => m is IBootstrapVContainerConfigurator)
                .Where(m => m is not IRootScopeRegistrationFilter filter || filter.ShouldConfigureRootScope)
                .OrderBy(m => m is IRootScopeConfigurationOrder ordered ? ordered.RootScopeConfigurationOrder : 0)
                .ThenBy(HierarchyPath, System.StringComparer.Ordinal)
                .Cast<IBootstrapVContainerConfigurator>()
                .ToList();
        }

        private static string HierarchyPath(MonoBehaviour behaviour)
        {
            var sb = new StringBuilder();
            for (var t = behaviour.transform; t != null; t = t.parent)
            {
                sb.Insert(0, $"/{t.GetSiblingIndex():D4}:{t.name}");
            }

            return behaviour.gameObject.scene.path + sb + "#" + behaviour.GetType().FullName;
        }
    }
}
