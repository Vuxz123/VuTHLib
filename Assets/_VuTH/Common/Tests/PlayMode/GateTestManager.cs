using System;
using VContainer;

namespace _VuTH.Common.Tests.PlayMode
{
    public interface IGateTestService
    {
    }

    public interface IGateTestManager : ICommonManager, IGateTestService
    {
    }

    public sealed class GateTestDependency
    {
    }

    /// <summary>
    /// Bootstrap manager used by the manager lifecycle tests: records init / deinit / inject order. It does NOT override
    /// ConfigureRootScope, so it is registered like ScreenManager / CameraManager (the default registration).
    /// </summary>
    public sealed class GateTestManager : VBootstrapManager<GateTestManager, IGateTestManager>, IGateTestManager
    {
        public int InitializeCount;
        public int DeinitializeCount;
        public bool Injected;
        public bool InjectedBeforeInitialize;
        public bool ThrowOnDeinitialize;

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
            DeinitializeCount++;
            if (ThrowOnDeinitialize) throw new InvalidOperationException("deinit failure (expected by the test)");
        }
    }

    public interface IToggleTestManager : IToggleableManager
    {
    }

    /// <summary>Toggleable manager used by the manager lifecycle tests.</summary>
    public sealed class ToggleTestManager : VManager<ToggleTestManager, IToggleTestManager>, IToggleTestManager
    {
        public int InitializeCount;
        public int DeinitializeCount;

        public void Configure(bool enabled, bool customLifecycle)
        {
            enableSystem = enabled;
            customLifecycleManagement = customLifecycle;
        }

        protected override void InitializeManager() => InitializeCount++;

        protected override void DeinitializeManager() => DeinitializeCount++;
    }
}
