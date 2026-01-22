using UnityEngine;

namespace Common
{
    public abstract class VManager<T, TI> : VSingleton<T, TI>
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
            if (IsEnabledSystem && !customLifecycleManagement)
            {
                InitializeManager();
            }
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