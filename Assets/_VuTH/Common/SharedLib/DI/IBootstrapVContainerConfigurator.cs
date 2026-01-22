using VContainer;

namespace Common.DI
{
    public interface IBootstrapVContainerConfigurator
    {
        public void ConfigureRootScope(IContainerBuilder builder);
    }
}