using VContainer;
using VContainer.Unity;

namespace Common.SharedLib.DI
{
    public class RootScopeContainer : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var boostrap = GetComponentsInChildren<IBoostrapVContainerConfigurator>();
            foreach (var b in boostrap)
            {
                b.ConfigureRootScope(builder);
            }
        }
    }
}