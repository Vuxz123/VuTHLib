using UnityEngine;
#if VCONTAINER
using System;
using System.Linq;
using _VuTH.Common.DI;
using VContainer;
using VContainer.Unity;
#endif

namespace _VuTH.Common
{
    /// <summary>
    /// MonoBehaviour singleton exposed through an interface. The first instance wins and survives scene loads; later
    /// instances are destroyed at the end of the frame.
    /// </summary>
    /// <typeparam name="T">The concrete singleton type.</typeparam>
    /// <typeparam name="TI">The interface the singleton is used through.</typeparam>
    public abstract class VSingleton<T, TI> : MonoBehaviour
#if VCONTAINER
        , IBootstrapVContainerConfigurator, IRootScopeRegistrationFilter
#endif
        where T : MonoBehaviour, TI
    {
        private static T _instance;

        /// <summary>
        /// Service-locator access. Prefer injecting <typeparamref name="TI"/>; use this only where DI is not available.
        /// </summary>
        public static TI Instance => _instance;

        public static bool HasInstance => _instance != null;

        /// <summary>
        /// False for a second instance (it is destroyed at the end of the frame). Duplicates must neither register
        /// into DI nor initialize.
        /// </summary>
        public bool IsPrimaryInstance => ReferenceEquals(_instance, this);

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = (T)(MonoBehaviour)this;
                DontDestroyOnLoad(gameObject);
                return;
            }

            if (IsPrimaryInstance) return;

            // Duplicate: remove the whole object when it only holds this singleton (+ Transform), otherwise only this
            // component — the other components on that object are not ours to destroy.
            if (GetComponents<Component>().Length <= 2) Destroy(gameObject);
            else Destroy(this);
        }

        protected virtual void OnDestroy()
        {
            if (IsPrimaryInstance)
            {
                _instance = null;
            }
        }

#if VCONTAINER
        private static Type[] _serviceTypes;

        /// <inheritdoc />
        public bool ShouldConfigureRootScope => IsPrimaryInstance;

        /// <summary>
        /// Registers this singleton in the root scope (see <see cref="RegisterSelf"/>). Override to register more,
        /// calling base.
        /// </summary>
        public virtual void ConfigureRootScope(IContainerBuilder builder)
        {
            RegisterSelf(builder);
        }

        /// <summary>
        /// Registers this instance as <typeparamref name="T"/>, <typeparamref name="TI"/> and the interfaces
        /// <typeparamref name="TI"/> extends (except the generic manager contracts).
        /// <para>
        /// Uses <c>RegisterComponent</c>: unlike <c>RegisterInstance</c>, it injects the instance when the container is
        /// built, so <c>[Inject]</c> methods actually run.
        /// </para>
        /// </summary>
        protected RegistrationBuilder RegisterSelf(IContainerBuilder builder)
        {
            return builder.RegisterComponent((T)(MonoBehaviour)this).As(ServiceTypes);
        }

        /// <summary>
        /// <typeparamref name="TI"/> and its base interfaces, minus <see cref="ICommonManager"/> /
        /// <see cref="IToggleableManager"/>.
        /// </summary>
        internal static Type[] ServiceTypes => _serviceTypes ??= typeof(TI).GetInterfaces()
            .Where(type => type != typeof(ICommonManager) && type != typeof(IToggleableManager))
            .Prepend(typeof(TI))
            .Distinct()
            .ToArray();
#endif
    }
}
