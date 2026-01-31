using VContainer;

namespace _VuTH.Common.DI
{
    // ReSharper disable once InconsistentNaming
    public interface IVContainerConfigurator
    {
        void Configure(IContainerBuilder builder);
    }
}