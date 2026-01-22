using VContainer;

namespace Common.DI
{
    public interface IBoostrapVContainerConfigurator
    {
        public void ConfigureRootScope(IContainerBuilder builder);
    }
}