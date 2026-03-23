using _VuTH.Common.Log;
using VContainer;
using VContainer.Unity;

namespace _VuTH.Common
{
    public abstract class VBootstrapManager<T, TI> : VManager<T, TI>, IBootstrapOrderProvider
        where T : VBootstrapManager<T, TI>, TI, new()
        where TI : ICommonManager
    {
        [UnityEngine.SerializeField] private int bootstrapOrder;

        public int BootstrapOrder => bootstrapOrder;

        public override bool IsEnabledSystem
        {
            get
            {
                // Always return true to indicate the bootstrap manager is always enabled
                enableSystem = true;
                return true;
            }
        }

        public override void EnableSystem(bool enable)
        {
            // Do nothing to ensure the bootstrap manager is always enabled
        }

        protected override void InitializeManager()
        {
            this.Log("Bootstrap Manager Initializing...");
            InitializeBootstrap();
        }

        protected override void DeinitializeManager()
        {
            DeinitializeBootstrap();
        }

        protected abstract void InitializeBootstrap();
        protected abstract void DeinitializeBootstrap();
    }
}
