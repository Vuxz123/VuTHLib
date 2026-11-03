using System.Collections.Generic;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Bootstrap.Profile;
using UnityEngine;
using UnityEngine.Serialization;
#if VCONTAINER
using _VuTH.Common.DI;
using VContainer;
#endif

namespace _VuTH.Core.Bootstrap
{
    public class BootstrapManagerCentral : MonoBehaviour
#if VCONTAINER
        , IBootstrapVContainerConfigurator, IRootScopeConfigurationOrder
#endif
    {
        [SerializeField, ReadOnlyField, FormerlySerializedAs("boostrapProfile")]
        private BootstrapProfile bootstrapProfile;

        private ICommonManager[] _vBootstrapManager;

#if VCONTAINER
        /// <summary>Configured last, so the deferred-initialization callback below is the very last build callback.</summary>
        public int RootScopeConfigurationOrder => int.MaxValue;

        public void ConfigureRootScope(IContainerBuilder builder)
        {
            // Managers created here would run InitializeBootstrap in Awake, before the container exists and before
            // [Inject] Construct. Queue them and initialize once every component has been injected.
            using (VManagerInitializationGate.BeginDeferral())
            {
                LoadBootstrapManagers();
            }

            foreach (var manager in _vBootstrapManager)
            {
                if (manager is not IBootstrapVContainerConfigurator configurator) continue;
                if (manager is IRootScopeRegistrationFilter { ShouldConfigureRootScope: false })
                {
                    this.LogWarning($"Skipping duplicate bootstrap manager {manager.GetType().Name} (an instance already exists).");
                    continue;
                }

                configurator.ConfigureRootScope(builder);
            }

            // RegisterComponent injects through build callbacks, which run in registration order: this one runs after
            // all of them, i.e. after every manager's [Inject] Construct.
            builder.RegisterBuildCallback(_ => VManagerInitializationGate.FlushDeferred());
        }
#endif

        private void Awake()
        {
#if !VCONTAINER
            LoadBootstrapManagers();
#endif
        }

        private void EnsureProfileSet()
        {
            if (bootstrapProfile) return;
            if (BootstrapProfileUtilities.TryGetProfile(out var profile))
            {
                bootstrapProfile = profile;
            }
            else
            {
                this.LogError("Bootstrap Profile is not set and could not be found in Resources!");
            }
        }

        private void LoadBootstrapManagers()
        {
            if (_vBootstrapManager != null)
            {
                // The root scope can be rebuilt; never instantiate the managers twice.
                return;
            }

            EnsureProfileSet();
            if (!bootstrapProfile)
            {
                _vBootstrapManager = System.Array.Empty<ICommonManager>();
                return;
            }

            var orderedPrefabs = GetOrderedBootstrapPrefabs();
            this.Log("Loading " + orderedPrefabs.Count + " Bootstrap Managers from Profile");
            _vBootstrapManager = new ICommonManager[orderedPrefabs.Count];
            for (var i = 0; i < orderedPrefabs.Count; i++)
            {
                var orderedPrefab = orderedPrefabs[i];
                var prefab = orderedPrefab.Prefab;
                var instance = Instantiate(prefab, null);
                var manager = instance.GetComponent<ICommonManager>();
                if (manager == null)
                {
                    this.LogError($"Prefab '{prefab.name}' missing ICommonManager component!");
                    continue;
                }

                _vBootstrapManager[i] = manager;
                this.Log($"Initialized Bootstrap Manager: {prefab.name} (order={orderedPrefab.Order})");
            }
        }

        private List<OrderedBootstrapPrefab> GetOrderedBootstrapPrefabs()
        {
            var prefabs = bootstrapProfile.boostrapPrefabs ?? System.Array.Empty<GameObject>();
            var orderedPrefabs = new List<OrderedBootstrapPrefab>(prefabs.Length);

            for (var i = 0; i < prefabs.Length; i++)
            {
                var prefab = prefabs[i];
                if (!prefab)
                {
                    continue;
                }

                var order = prefab.TryGetComponent<IBootstrapOrderProvider>(out var orderProvider)
                    ? orderProvider.BootstrapOrder
                    : 0;
                orderedPrefabs.Add(new OrderedBootstrapPrefab(prefab, order, i));
            }

            orderedPrefabs.Sort(static (left, right) =>
            {
                var orderComparison = left.Order.CompareTo(right.Order);
                return orderComparison != 0 ? orderComparison : left.OriginalIndex.CompareTo(right.OriginalIndex);
            });

            return orderedPrefabs;
        }

        private readonly struct OrderedBootstrapPrefab
        {
            public readonly GameObject Prefab;
            public readonly int Order;
            public readonly int OriginalIndex;

            public OrderedBootstrapPrefab(GameObject prefab, int order, int originalIndex)
            {
                Prefab = prefab;
                Order = order;
                OriginalIndex = originalIndex;
            }
        }
    }
}
