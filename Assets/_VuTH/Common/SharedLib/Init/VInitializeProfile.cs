using System;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Common.SharedLib.Init
{
    [CreateAssetMenu(fileName = "ManagerProfile", menuName = "Common/ManagerProfile")]
    public class VInitializeProfile : ScriptableObject
    {
        [SerializeField] private bool isEnabled = true;
        [SerializeReference] private IVInitializable[] initializables;

        public bool IsEnabled => isEnabled;
        
        public ReadOnlyCollection<IVInitializable> Initializables => Array.AsReadOnly(initializables);
    }
}