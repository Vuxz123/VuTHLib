using System;
using System.Collections.ObjectModel;
using UnityEngine;

namespace _VuTH.Common.Init
{
    [CreateAssetMenu(fileName = "VInitializeProfile", menuName = "Profiles/VInitializeProfile", order = 1)]
    public class VInitializeProfile : ScriptableObject
    {
        [SerializeField] private bool isEnabled = true;
        [SerializeReference] private IVInitializable[] initializables;

        public bool IsEnabled => isEnabled;
        
        public ReadOnlyCollection<IVInitializable> Initializables => Array.AsReadOnly(initializables);
    }
}