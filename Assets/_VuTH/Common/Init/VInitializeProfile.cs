using System;
using System.Collections.ObjectModel;
using UnityEngine;

namespace _VuTH.Common.Init
{
    /// <summary>
    /// Asset holding plain C# <see cref="IVInitializable"/> instances (via <c>[SerializeReference]</c>).
    /// Scene components cannot live here — <c>[SerializeReference]</c> does not support UnityEngine.Object; they are
    /// collected on the scene's <see cref="VInitializeInvokeSite"/> instead.
    /// </summary>
    [CreateAssetMenu(fileName = "VInitializeProfile", menuName = "Profiles/VInitializeProfile", order = 1)]
    public class VInitializeProfile : ScriptableObject
    {
        [SerializeField] private bool isEnabled = true;
        [SerializeReference] private IVInitializable[] initializables = Array.Empty<IVInitializable>();

        public bool IsEnabled => isEnabled;

        public ReadOnlyCollection<IVInitializable> Initializables =>
            Array.AsReadOnly(initializables ?? Array.Empty<IVInitializable>());
    }
}
