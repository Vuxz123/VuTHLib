using VContainer;

namespace _VuTH.Common.DI
{
    public interface IVContainerConfigurator
    {
        void Configure(IContainerBuilder builder);
    }
}