using UnityEngine;

namespace _VuTH.Common
{
    /// <summary>
    /// Manager that can be switched on and off at runtime (<see cref="IToggleableManager"/>). For always-on managers
    /// created at startup, use <see cref="VBootstrapManager{T,TI}"/>.
    /// </summary>
    public abstract class VManager<T, TI> : VManagerCore<T, TI>, IToggleableManager
        where T : VManager<T, TI>, TI
        where TI : ICommonManager
    {
        [Header("Manager Settings")]
        [SerializeField, Tooltip("Whether the system is on. Enabled systems initialize in Awake.")]
        protected bool enableSystem;

        [SerializeField, Tooltip("If enabled, the manager does not initialize in Awake: call EnableSystem(true) yourself.")]
        protected bool customLifecycleManagement;

        public bool IsEnabledSystem => enableSystem;

        protected override bool InitializeOnAwake => enableSystem && !customLifecycleManagement;

        public virtual void EnableSystem(bool enable)
        {
            enableSystem = enable;

            // Idempotent both ways. EnableSystem(true) also starts a manager whose flag was already set but which did
            // not initialize yet (custom lifecycle management) — that used to be a no-op.
            if (enable) EnsureInitialized();
            else EnsureDeinitialized();
        }

        public void ToggleSystem()
        {
            EnableSystem(!enableSystem);
        }
    }
}
