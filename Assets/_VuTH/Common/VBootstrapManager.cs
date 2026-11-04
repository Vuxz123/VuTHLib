using _VuTH.Common.Log;

namespace _VuTH.Common
{
    /// <summary>
    /// Always-on manager created at startup by the bootstrap (ordered by <see cref="BootstrapOrder"/>). It initializes
    /// in <c>Awake</c> — after injection when created during root DI configuration — and deinitializes when destroyed.
    /// <para>
    /// Initialize everything in <see cref="InitializeBootstrap"/>: injected dependencies are already available there.
    /// <c>[Inject]</c> methods should only store dependencies (and apply them when the manager is already initialized,
    /// e.g. when it was placed in a scene instead of being created by the bootstrap).
    /// </para>
    /// </summary>
    public abstract class VBootstrapManager<T, TI> : VManagerCore<T, TI>, IBootstrapOrderProvider
        where T : VBootstrapManager<T, TI>, TI
        where TI : ICommonManager
    {
        [UnityEngine.SerializeField] private int bootstrapOrder;

        public int BootstrapOrder => bootstrapOrder;

        protected sealed override bool InitializeOnAwake => true;

        protected sealed override void InitializeManager()
        {
            this.Log("Bootstrap Manager Initializing...");
            InitializeBootstrap();
        }

        protected sealed override void DeinitializeManager()
        {
            DeinitializeBootstrap();
        }

        protected abstract void InitializeBootstrap();
        protected abstract void DeinitializeBootstrap();
    }
}
