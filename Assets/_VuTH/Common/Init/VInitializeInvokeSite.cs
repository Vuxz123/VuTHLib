using System;
using System.Collections.Generic;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _VuTH.Common.Init
{
    /// <summary>
    /// Delegate for handling initialization completion of IVInitializable objects.
    /// </summary>
    public delegate void OnInitializableInitialized(IVInitializable initializable, Type type);

    /// <summary>
    /// Runs the <see cref="IVInitializable"/>s of a scene, in order, when the screen loads (VInitializableTask).
    /// <list type="bullet">
    /// <item><see cref="sceneInitializables"/>: the scene's components, collected automatically when the scene is saved
    /// (VInitializableScenePostprocessorTask). Stored as plain object references.</item>
    /// <item><see cref="initializeProfile"/> (optional): plain C# initializers configured in a profile asset; run first.</item>
    /// </list>
    /// </summary>
    public class VInitializeInvokeSite : MonoBehaviour
    {
        [Tooltip("Optional: plain C# IVInitializable instances (run before the scene components).")]
        [SerializeField] private VInitializeProfile initializeProfile;

        // Plain references: [SerializeReference] cannot store UnityEngine.Object (scene components were silently lost
        // on save, so InvokeInitialize later hit null entries).
        [Tooltip("Scene components implementing IVInitializable. Filled automatically when the scene is saved.")]
        [SerializeField] private MonoBehaviour[] sceneInitializables = Array.Empty<MonoBehaviour>();

        public event OnInitializableInitialized OnInitializableInitializedEvent;

        /// <summary>Scene components currently assigned (read-only view, for tooling / tests).</summary>
        public IReadOnlyList<MonoBehaviour> SceneInitializables => sceneInitializables;

        public async UniTask InvokeInitialize()
        {
            var ran = false;

            if (initializeProfile)
            {
                if (initializeProfile.IsEnabled)
                {
                    foreach (var initializer in initializeProfile.Initializables)
                    {
                        ran |= await Run(initializer);
                    }
                }
                else
                {
                    this.LogWarning("Initialize Profile is disabled. Skipping its initializers.");
                }
            }

            if (sceneInitializables != null)
            {
                foreach (var component in sceneInitializables)
                {
                    // Destroyed / removed since the last save, or not an initializable anymore.
                    if (!component || component is not IVInitializable initializer) continue;
                    ran |= await Run(initializer);
                }
            }

            if (!ran)
            {
                this.LogWarning("No IVInitializable to run (save the scene to collect its components).");
            }
        }

        private async UniTask<bool> Run(IVInitializable initializer)
        {
            if (initializer == null) return false;

            var type = initializer.GetType();
            this.Log($"Initializing {type.Name}...");
            await initializer.VInitialize();
            this.Log($"Initialized {type.Name}.");
            OnInitializableInitializedEvent?.Invoke(initializer, type);
            return true;
        }

        /// <summary>
        /// Assigns the scene's initializable components (non-components are ignored: put plain C# initializers in a
        /// profile). Returns false — and changes nothing — when the list is already identical, so saving a scene does
        /// not re-dirty it every time.
        /// </summary>
        public bool AssignInitializables(IVInitializable[] initializables)
        {
            var components = new List<MonoBehaviour>();
            if (initializables != null)
            {
                foreach (var initializable in initializables)
                {
                    if (initializable is MonoBehaviour behaviour && behaviour && !components.Contains(behaviour))
                    {
                        components.Add(behaviour);
                    }
                }
            }

            if (SameSequence(sceneInitializables, components)) return false;

            sceneInitializables = components.ToArray();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            return true;
        }

        private static bool SameSequence(MonoBehaviour[] current, List<MonoBehaviour> next)
        {
            if (current == null) return next.Count == 0;
            if (current.Length != next.Count) return false;
            for (var i = 0; i < current.Length; i++)
            {
                if (!ReferenceEquals(current[i], next[i])) return false;
            }

            return true;
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
