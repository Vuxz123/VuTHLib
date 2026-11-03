using System.Collections.Generic;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using ZLinq;
#if VCONTAINER
using VContainer.Unity;
#endif

namespace _VuTH.Core.GameCycle.Screen.Core
{
    /// <summary>
    /// Owns the Addressables scene cache (AssetGUID -> <see cref="SceneInstance"/>) used by <see cref="ScreenManager"/>:
    /// additive loading (optionally parented to the core VContainer scope), show/hide of scene roots,
    /// and the unload policy (soft cache, smart look-ahead, unloadOnClose).
    /// Log messages are emitted with the owner passed to the constructor as origin.
    /// </summary>
    public sealed class AddressableSceneCache
    {
        private readonly object _logOwner;

        // Addressables cache: AssetGUID -> SceneInstance
        private readonly Dictionary<string, SceneInstance> _cachedAddressableScenes = new();

        /// <param name="logOwner">Origin object used for log messages (normally the ScreenManager).</param>
        public AddressableSceneCache(object logOwner)
        {
            _logOwner = logOwner;
        }

#if VCONTAINER
        /// <summary>
        /// LifetimeScope enqueued as parent while loading scenes, so scene scopes get the core container as parent.
        /// </summary>
        public LifetimeScope CoreLifetimeScope { get; set; }
#endif

        /// <summary>
        /// Loads the scene additively, or re-activates its roots if it is already cached and loaded.
        /// </summary>
        public async UniTask LoadOrShowSceneAsync(AssetReference sceneRef)
        {
            var key = GetAssetGuid(sceneRef);
            if (string.IsNullOrEmpty(key))
            {
                _logOwner.LogError("Addressables sceneRef has no AssetGUID (is it an addressable scene asset?)");
                return;
            }

            if (_cachedAddressableScenes.TryGetValue(key, out var cached))
            {
                if (cached.Scene.isLoaded)
                {
                    SetSceneRootActive(cached.Scene, true);
                    return;
                }

                _cachedAddressableScenes.Remove(key);
            }

#if VCONTAINER
            if (CoreLifetimeScope == null)
            {
                _logOwner.LogError("ScreenManager: Core LifetimeScope is null. Addressable scene loading with VContainer may fail to inject dependencies.");
            }
            // Đưa container của Core vào hàng đợi.
            // Khi Scene mới load và LifetimeScope của nó Awake, nó sẽ tự động lấy cái này làm Parent.
            using (LifetimeScope.EnqueueParent(CoreLifetimeScope))
            {
#endif
                var handle = Addressables.LoadSceneAsync(sceneRef, LoadSceneMode.Additive, activateOnLoad: true);
                await handle.Task;

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    _logOwner.LogError($"Failed to load addressable scene: {sceneRef.RuntimeKey}");
                    return;
                }

                _cachedAddressableScenes[key] = handle.Result;
                SetSceneRootActive(handle.Result.Scene, true);
#if VCONTAINER
            }
#endif
        }

        /// <summary>
        /// Returns the cached loaded scene for the reference, or default if not cached.
        /// </summary>
        public Scene GetLoadedScene(AssetReference sceneRef)
        {
            var key = GetAssetGuid(sceneRef);
            if (string.IsNullOrEmpty(key))
                return default;

            return _cachedAddressableScenes.TryGetValue(key, out var cached) ? cached.Scene : default;
        }

        /// <summary>
        /// Activates the root GameObjects of the screen's main and additive scenes (if cached).
        /// </summary>
        /// <summary>
        /// Deactivates the root objects of <paramref name="screen"/>'s scenes without unloading them.
        /// Scenes also used by <paramref name="keepVisibleFor"/> stay active.
        /// </summary>
        public void HideScreen(ScreenModel screen, ScreenModel keepVisibleFor)
        {
            if (!screen) return;

            if (screen.mainSceneRef != null && screen.mainSceneRef.RuntimeKeyIsValid() &&
                !IsSceneUsedByScreen(keepVisibleFor, GetAssetGuid(screen.mainSceneRef)))
            {
                SetSceneRootActive(screen.mainSceneRef, false);
            }

            if (screen.additiveScene == null) return;
            foreach (var additive in screen.additiveScene)
            {
                if (additive.sceneRef == null || !additive.sceneRef.RuntimeKeyIsValid()) continue;
                if (IsSceneUsedByScreen(keepVisibleFor, GetAssetGuid(additive.sceneRef))) continue;
                SetSceneRootActive(additive.sceneRef, false);
            }
        }

        public void ShowScreen(ScreenModel screen)
        {
            if (!screen) return;

            if (screen.mainSceneRef != null && screen.mainSceneRef.RuntimeKeyIsValid())
                SetSceneRootActive(screen.mainSceneRef, true);

            if (screen.additiveScene == null) return;

            foreach (var additive in screen.additiveScene)
            {
                if (additive.sceneRef == null || !additive.sceneRef.RuntimeKeyIsValid())
                    continue;

                SetSceneRootActive(additive.sceneRef, true);
            }
        }

        /// <summary>
        /// Unloads (or hides) the screen's scenes according to the unload policy.
        /// </summary>
        /// <param name="screen">Screen being closed.</param>
        /// <param name="nextToShow">Screen shown next; additive scenes it also uses are kept (smart look-ahead).</param>
        public async UniTask UnloadScreenAsync(ScreenModel screen, ScreenModel nextToShow)
        {
            if (!screen) return;

            if (screen.mainSceneRef != null && screen.mainSceneRef.RuntimeKeyIsValid())
            {
                await UnloadSceneByPolicyAsync(screen.mainSceneRef, screen, nextToShow, alwaysCheckSmartLookAhead: false);
            }

            if (screen.additiveScene == null) return;

            foreach (var additive in screen.additiveScene)
            {
                if (additive.sceneRef == null || !additive.sceneRef.RuntimeKeyIsValid())
                    continue;

                await UnloadSceneByPolicyAsync(additive.sceneRef, screen, nextToShow, alwaysCheckSmartLookAhead: true, unloadOnClose: additive.unloadOnClose);
            }
        }

        /// <summary>
        /// Fires an unload for every cached scene (not awaited) and clears the cache.
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var kv in _cachedAddressableScenes)
            {
                Addressables.UnloadSceneAsync(kv.Value);
            }
            _cachedAddressableScenes.Clear();
        }

        private async UniTask UnloadSceneByPolicyAsync(
            AssetReference sceneRef,
            ScreenModel ownerScreen,
            ScreenModel nextToShow,
            bool alwaysCheckSmartLookAhead,
            bool unloadOnClose = true)
        {
            var key = GetAssetGuid(sceneRef);
            if (string.IsNullOrEmpty(key)) return;

            if (ownerScreen != null && ownerScreen.softCache)
            {
                SetSceneRootActive(sceneRef, false);
                return;
            }

            if (alwaysCheckSmartLookAhead && nextToShow != null)
            {
                if (IsSceneUsedByScreen(nextToShow, key))
                {
                    _logOwner.Log($"[Smart Keep] Scene {key} is used by next screen ({nextToShow.name}). Skipping unload.");
                    return;
                }
            }

            if (!unloadOnClose) return;

            if (_cachedAddressableScenes.TryGetValue(key, out var instance))
            {
                await Addressables.UnloadSceneAsync(instance).Task;
                _cachedAddressableScenes.Remove(key);
            }
        }

        private static string GetAssetGuid(AssetReference reference) => reference?.AssetGUID;

        private static bool IsSceneUsedByScreen(ScreenModel screen, string assetGuid)
        {
            if (!screen) return false;

            if (screen.mainSceneRef != null && screen.mainSceneRef.RuntimeKeyIsValid() && GetAssetGuid(screen.mainSceneRef) == assetGuid)
                return true;

            if (screen.additiveScene == null) return false;

            return screen.additiveScene.AsValueEnumerable()
                .Any(x => x.sceneRef != null && x.sceneRef.RuntimeKeyIsValid() && GetAssetGuid(x.sceneRef) == assetGuid);
        }

        private void SetSceneRootActive(AssetReference sceneRef, bool active)
        {
            var key = GetAssetGuid(sceneRef);
            if (string.IsNullOrEmpty(key)) return;

            if (_cachedAddressableScenes.TryGetValue(key, out var instance))
            {
                SetSceneRootActive(instance.Scene, active);
            }
        }

        private static void SetSceneRootActive(Scene scene, bool active)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            var roots = scene.GetRootGameObjects();
            foreach (var t in roots)
            {
                if (t) t.SetActive(active);
            }
        }
    }
}
