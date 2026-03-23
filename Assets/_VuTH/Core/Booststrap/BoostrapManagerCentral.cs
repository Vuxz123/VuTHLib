using System.Collections.Generic;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Booststrap.Profile;
using UnityEngine;
#if VCONTAINER
using _VuTH.Common.DI;
using VContainer;
#endif

namespace _VuTH.Core.Booststrap
{
    public class BootstrapManagerCentral : MonoBehaviour
#if VCONTAINER
        , IBootstrapVContainerConfigurator
#endif
    {
        [SerializeField, ReadOnlyField] private BootstrapProfile boostrapProfile;

        private ICommonManager[] _vBootstrapManager;

#if VCONTAINER
        public void ConfigureRootScope(IContainerBuilder builder)
        {
            LoadBootstrapManagers();

            foreach (var manager in _vBootstrapManager)
            {
                if (manager is IBootstrapVContainerConfigurator boostrapVContainerConfigurator)
                {
                    boostrapVContainerConfigurator.ConfigureRootScope(builder);
                }
            }
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
            if (boostrapProfile) return;
            if (BootstrapProfileUtilities.TryGetProfile(out var profile))
            {
                boostrapProfile = profile;
            }
            else
            {
                this.LogError("Bootstrap Profile is not set and could not be found in Resources!");
            }
        }

        private void LoadBootstrapManagers()
        {
            EnsureProfileSet();
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
            var boostrapPrefabs = boostrapProfile.boostrapPrefabs;
            var orderedPrefabs = new List<OrderedBootstrapPrefab>(boostrapPrefabs.Length);

            for (var i = 0; i < boostrapPrefabs.Length; i++)
            {
                var prefab = boostrapPrefabs[i];
                if (!prefab)
                {
                    continue;
                }

                var orderProvider = prefab.GetComponent<IBootstrapOrderProvider>();
                var order = orderProvider?.BootstrapOrder ?? 0;
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
