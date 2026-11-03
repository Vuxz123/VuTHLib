using VContainer;
using VContainer.Unity;

namespace _VuTH.Common.Tests.PlayMode
{
    public interface IGateTestManager : ICommonManager
    {
    }

    public sealed class GateTestDependency
    {
    }

    /// <summary>Bootstrap manager used by <see cref="ManagerInitializationGateTests"/>: records init / inject order.</summary>
    public sealed class GateTestManager : VBootstrapManager<GateTestManager, IGateTestManager>, IGateTestManager
    {
        public int InitializeCount;
        public bool Injected;
        public bool InjectedBeforeInitialize;

        [Inject]
        public void Construct(GateTestDependency dependency)
        {
            Injected = dependency != null;
        }

        protected override void InitializeBootstrap()
        {
            InitializeCount++;
            InjectedBeforeInitialize = Injected;
        }

        protected override void DeinitializeBootstrap()
        {
        }

        public override void ConfigureRootScope(IContainerBuilder builder)
        {
            builder.RegisterComponent(this).AsImplementedInterfaces();
        }
    }
}
