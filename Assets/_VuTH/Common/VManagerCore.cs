using UnityEngine;

namespace _VuTH.Common
{
    /// <summary>
    /// Lifecycle shared by every manager: <see cref="InitializeManager"/> / <see cref="DeinitializeManager"/> run at
    /// most once each, in pairs, and only on the primary singleton instance.
    /// <list type="bullet">
    /// <item>Initialization starts in <c>Awake</c> when <see cref="InitializeOnAwake"/> is true — or, for managers
    /// created during root DI configuration, right after injection (<see cref="VManagerInitializationGate"/>).</item>
    /// <item>Deinitialization runs when the manager is destroyed (scene teardown, leaving Play Mode, quitting).</item>
    /// </list>
    /// </summary>
    public abstract class VManagerCore<T, TI> : VSingleton<T, TI>, IDeferredManagerInitialization
        where T : VManagerCore<T, TI>, TI
        where TI : ICommonManager
    {
        private bool _isInitialized;

        /// <inheritdoc cref="ICommonManager.IsInitialized" />
        public virtual bool IsInitialized => _isInitialized;

        /// <summary>Whether <see cref="InitializeManager"/> ran (and <see cref="DeinitializeManager"/> did not yet).</summary>
        protected bool IsManagerInitialized => _isInitialized;

        /// <summary>Whether the manager initializes by itself in <c>Awake</c>.</summary>
        protected abstract bool InitializeOnAwake { get; }

        protected override void Awake()
        {
            base.Awake();

            // A duplicate is destroyed by VSingleton at the end of the frame: never let it initialize.
            if (!IsPrimaryInstance || !InitializeOnAwake) return;

            // Created during root DI configuration: initialize after injection instead.
            if (VManagerInitializationGate.TryDefer(this)) return;

            EnsureInitialized();
        }

        void IDeferredManagerInitialization.RunDeferredInitialization()
        {
            if (!this || !InitializeOnAwake) return;
            EnsureInitialized();
        }

        protected override void OnDestroy()
        {
            // Before base.OnDestroy, which clears the singleton instance.
            if (IsPrimaryInstance) EnsureDeinitialized();
            base.OnDestroy();
        }

        /// <summary>Runs <see cref="InitializeManager"/> unless already initialized (or not the primary instance).</summary>
        protected void EnsureInitialized()
        {
            if (_isInitialized || !IsPrimaryInstance) return;

            // Set first: a manager that throws half-way still gets DeinitializeManager to clean up what it created.
            _isInitialized = true;
            InitializeManager();
        }

        /// <summary>Runs <see cref="DeinitializeManager"/> if the manager is initialized.</summary>
        protected void EnsureDeinitialized()
        {
            if (!_isInitialized) return;

            _isInitialized = false;
            try
            {
                DeinitializeManager();
            }
            catch (System.Exception e)
            {
                // Called from OnDestroy: one manager's cleanup must not break the others'.
                Debug.LogException(e, this);
            }
        }

        protected abstract void InitializeManager();

        protected abstract void DeinitializeManager();
    }
}
