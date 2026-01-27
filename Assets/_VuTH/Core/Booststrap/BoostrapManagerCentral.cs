using _VuTH.Common;
using _VuTH.Common.DI;
using _VuTH.Common.Log;
using _VuTH.Core.Booststrap.Profile;
using UnityEngine;
using VContainer;

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
            var boostrapPrefabs = boostrapProfile.boostrapPrefabs;
            this.Log("Loading " + boostrapPrefabs.Length + " Bootstrap Managers from Profile");
            _vBootstrapManager = new ICommonManager[boostrapPrefabs.Length];
            for (int i = 0; i < boostrapPrefabs.Length; i++)
            {
                var prefab = boostrapPrefabs[i];
                var instance = Instantiate(prefab, null);
                var manager = instance.GetComponent<ICommonManager>();
                if (manager == null)
                {
                    this.LogError($"Prefab '{prefab.name}' missing ICommonManager component!");
                    continue;
                }
                _vBootstrapManager[i] = manager;
                this.Log("Initialized Bootstrap Manager: " + prefab.name);
            }
        }
    }
}