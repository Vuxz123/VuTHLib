using UnityEngine;

namespace _VuTH.Common
{
    public abstract class VManager<T, TI> : VSingleton<T, TI>, IDeferredManagerInitialization
        where T : VManager<T, TI>, TI, new() 
        where TI : ICommonManager
    {
        [Header("Manager Settings")]
        [SerializeField] protected bool enableSystem;
        [SerializeField, Tooltip("If enabled, the manager's lifecycle (initialization and deinitialization) will be handled by custom Awake lifecycle management.")]
        protected bool customLifecycleManagement = false;

        public virtual bool IsEnabledSystem
        {
            get => enableSystem;
            private set => enableSystem = value;
        }
        
        protected override void Awake()
        {
            base.Awake();

            // A duplicate is destroyed by VSingleton at the end of the frame: never let it initialize
            // (it used to create runtime objects / subscriptions right before being destroyed).
            if (!IsPrimaryInstance) return;
            if (!IsEnabledSystem || customLifecycleManagement) return;

            // Created during root DI configuration: initialize after injection instead (see VManagerInitializationGate).
            if (VManagerInitializationGate.TryDefer(this)) return;

            InitializeManager();
        }

        void IDeferredManagerInitialization.RunDeferredInitialization()
        {
            if (!this || !IsPrimaryInstance) return;
            if (!IsEnabledSystem || customLifecycleManagement) return;
            InitializeManager();
        }

        public virtual void EnableSystem(bool enable)
        {
            if (enableSystem == enable)
                return;
            enableSystem = enable;
            if (enableSystem)
            {
                InitializeManager();
            }
            else
            {
                DeinitializeManager();
            }
        }

        public void ToggleSystem()
        {
            EnableSystem(!enableSystem);
        }

        protected abstract void InitializeManager();

        protected abstract void DeinitializeManager();
    }
}