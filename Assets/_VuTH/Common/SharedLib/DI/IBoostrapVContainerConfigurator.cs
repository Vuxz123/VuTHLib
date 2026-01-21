using VContainer;

namespace Common.SharedLib.DI
{
    public interface IBoostrapVContainerConfigurator
    {
        public void ConfigureRootScope(IContainerBuilder builder);
    }
}