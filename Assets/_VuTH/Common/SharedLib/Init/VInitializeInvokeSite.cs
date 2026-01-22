using System;
using System.Reflection;
using Common.Log;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Common.Init
{
    /// <summary>
    /// Delegate for handling initialization completion of IVInitializable objects.
    /// </summary>
    public delegate void OnInitializableInitialized(IVInitializable initializable, Type type);
    
    public class VInitializeInvokeSite : MonoBehaviour
    {
        [SerializeField] private VInitializeProfile initializeProfile;
        
        public event OnInitializableInitialized OnInitializableInitializedEvent;
        
        public async UniTask InvokeInitialize()
        {
            if (!initializeProfile)
            {
                this.LogError("Initialize Profile is not assigned.");
                return;
            }

            if (!initializeProfile.IsEnabled)
            {
                this.LogWarning("Initialize Profile is disabled. Skipping initialization.");
                return;
            }

            var initializes = initializeProfile.Initializables;

            foreach (var initializer in initializes)
            {
                var type = initializer.GetType();
                this.Log($"Initializing {type.Name}...");
                await initializer.VInitialize();
                
                this.Log($"Initialized {type.Name}.");
                OnInitializableInitializedEvent?.Invoke(initializer, type);
            }
        }

        // Public helper to assign initializables found in the scene into this invoke site.
        public void AssignInitializables(IVInitializable[] initializables)
        {
            if (initializables == null) return;

            // Create a profile instance and set its private serialized field via reflection.
            var profile = ScriptableObject.CreateInstance<VInitializeProfile>();

            var field = typeof(VInitializeProfile).GetField("initializables", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(profile, initializables);
            }

            // Assign the profile to the private field on this component.
            initializeProfile = profile;

#if UNITY_EDITOR
            // Mark objects dirty so Unity will serialize the changes to the scene.
            UnityEditor.EditorUtility.SetDirty(profile);
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    public static class SceneVInitializeInvokeSiteExtensions
    {
        public static bool TryGetVInitializeInvokeSite(this UnityEngine.SceneManagement.Scene scene, out VInitializeInvokeSite invokeSite)
        {
            var rootGameObjects = scene.GetRootGameObjects();
            foreach (var go in rootGameObjects)
            {
                invokeSite = go.GetComponentInChildren<VInitializeInvokeSite>(true);
                if (invokeSite)
                {
                    return true;
                }
            }
            invokeSite = null;
            return false;
        }
    }
}