using VContainer;

namespace Common.DI
{
    public interface IVContainerConfigurator
    {
        void Configure(IContainerBuilder builder);
    }
}