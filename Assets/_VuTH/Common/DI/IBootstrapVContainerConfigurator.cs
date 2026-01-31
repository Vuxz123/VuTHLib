using VContainer;

namespace _VuTH.Common.DI
{
    public interface IBootstrapVContainerConfigurator
    {
        public void ConfigureRootScope(IContainerBuilder builder);
    }
}