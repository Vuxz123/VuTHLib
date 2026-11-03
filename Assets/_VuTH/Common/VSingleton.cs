
using _VuTH.Common.Log;
using UnityEngine;
#if VCONTAINER
using VContainer;
using _VuTH.Common.DI;
#endif

namespace _VuTH.Common
{
    /// <summary>
    /// Singleton base class cho MonoBehaviour với interface
    /// </summary>
    /// <typeparam name="T"> class kế thừa Singleton </typeparam>
    /// <typeparam name="TI"> interface mà Singleton sẽ triển khai </typeparam>
    public abstract class VSingleton<T, TI> : MonoBehaviour
#if VCONTAINER
        , IBootstrapVContainerConfigurator, IRootScopeRegistrationFilter
#endif
        where T : MonoBehaviour, TI
    {
        private static T _instance;
        
        public static TI Instance => _instance;
        
        public static bool HasInstance => _instance != null;

        /// <summary>
        /// False for a second instance (it is destroyed at the end of the frame). Duplicates must neither register
        /// into DI nor initialize.
        /// </summary>
        public bool IsPrimaryInstance => ReferenceEquals(_instance, this);
        
        protected virtual void Awake()
        {
            var self = this as T;
            
            if (_instance == null)
            {
                _instance = self; 
                DontDestroyOnLoad(gameObject);
            }
            else if (!ReferenceEquals(_instance, self))
            {
                // A second instance was created; destroy it
                Destroy(gameObject);
            }
        }
        
        protected virtual void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

#if VCONTAINER
        /// <inheritdoc />
        public bool ShouldConfigureRootScope => IsPrimaryInstance;

        public virtual void ConfigureRootScope(IContainerBuilder builder)
        {
            this.Log($"Inject {typeof(T).Name} into root scope");
            builder.RegisterInstance((T)(MonoBehaviour)this).As<TI>().AsSelf();
        }
#endif
    }
}