using System.Collections.Generic;
using _VuTH.Common.Log;
using System;
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
        private readonly Func<IEnumerable<ScreenModel>> _owners;
        private readonly HashSet<ScreenModel> _hiddenScreens = new();
        private readonly Dictionary<int, Dictionary<GameObject, bool>> _hiddenRootStates = new();

        // Addressables cache: AssetGUID -> SceneInstance
        private readonly Dictionary<string, SceneInstance> _cachedAddressableScenes = new();

        /// <param name="logOwner">Origin object used for log messages (normally the ScreenManager).</param>
        public AddressableSceneCache(object logOwner, Func<IEnumerable<ScreenModel>> owners = null)
        {
            _logOwner = logOwner;
            _owners = owners;
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

                _hiddenRootStates.Remove(cached.Scene.handle);
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
        /// Deactivates the root objects of <paramref name="screen"/>'s scenes without unloading them.
        /// Scenes also used by any visible navigation owner or <paramref name="keepVisibleFor"/> stay active.
        /// </summary>
        public void HideScreen(ScreenModel screen, ScreenModel keepVisibleFor)
        {
            if (!screen) return;
            _hiddenScreens.Add(screen);

            if (screen.mainSceneRef != null && screen.mainSceneRef.RuntimeKeyIsValid() &&
                !IsSceneNeededVisible(GetAssetGuid(screen.mainSceneRef), keepVisibleFor))
            {
                SetSceneRootActive(screen.mainSceneRef, false);
            }

            if (screen.additiveScene == null) return;
            foreach (var additive in screen.additiveScene)
            {
                if (additive.sceneRef == null || !additive.sceneRef.RuntimeKeyIsValid()) continue;
                if (IsSceneNeededVisible(GetAssetGuid(additive.sceneRef), keepVisibleFor)) continue;
                SetSceneRootActive(additive.sceneRef, false);
            }
        }

        public void ShowScreen(ScreenModel screen)
        {
            if (!screen) return;
            _hiddenScreens.Remove(screen);

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
        /// <param name="nextToShow">Screen shown next; its main and additive scenes stay visible.</param>
        public async UniTask UnloadScreenAsync(ScreenModel screen, ScreenModel nextToShow)
        {
            if (!screen) return;

            if (screen.mainSceneRef != null && screen.mainSceneRef.RuntimeKeyIsValid())
            {
                await UnloadSceneByPolicyAsync(screen.mainSceneRef, screen, nextToShow);
            }

            if (screen.additiveScene == null) return;

            foreach (var additive in screen.additiveScene)
            {
                if (additive.sceneRef == null || !additive.sceneRef.RuntimeKeyIsValid())
                    continue;

                await UnloadSceneByPolicyAsync(additive.sceneRef, screen, nextToShow, unloadOnClose: additive.unloadOnClose);
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
            _hiddenRootStates.Clear();
            _hiddenScreens.Clear();
        }

        private async UniTask UnloadSceneByPolicyAsync(
            AssetReference sceneRef,
            ScreenModel ownerScreen,
            ScreenModel nextToShow,
            bool unloadOnClose = true)
        {
            var key = GetAssetGuid(sceneRef);
            if (string.IsNullOrEmpty(key)) return;

            // Ownership precedes soft-cache and unloadOnClose, for main and additive scenes alike.
            if (IsSceneNeededVisible(key, nextToShow)) return;

            var owners = _owners?.Invoke();
            if (owners != null)
            {
                var retained = false;
                foreach (var owner in owners)
                {
                    if (!IsSceneUsedByScreen(owner, key)) continue;
                    retained = true;
                }
                if (retained)
                {
                    SetSceneRootActive(sceneRef, false);
                    return;
                }
            }

            if (ownerScreen != null && ownerScreen.softCache)
            {
                SetSceneRootActive(sceneRef, false);
                return;
            }

            if (!unloadOnClose) return;

            if (_cachedAddressableScenes.TryGetValue(key, out var instance))
            {
                await Addressables.UnloadSceneAsync(instance).Task;
                _hiddenRootStates.Remove(instance.Scene.handle);
                _cachedAddressableScenes.Remove(key);
            }
        }

        private static string GetAssetGuid(AssetReference reference) => reference?.AssetGUID;

        private bool IsSceneNeededVisible(string key, ScreenModel keepVisibleFor)
        {
            if (IsSceneUsedByScreen(keepVisibleFor, key)) return true;
            var owners = _owners?.Invoke();
            if (owners == null) return false;
            foreach (var owner in owners)
            {
                if (!_hiddenScreens.Contains(owner) && IsSceneUsedByScreen(owner, key)) return true;
            }
            return false;
        }

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

        private void SetSceneRootActive(Scene scene, bool active)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            if (active)
            {
                // First/repeated shows preserve authored state; only undo a hide performed by this cache.
                if (!_hiddenRootStates.TryGetValue(scene.handle, out var previous)) return;
                _hiddenRootStates.Remove(scene.handle);
                foreach (var entry in previous) if (entry.Key) entry.Key.SetActive(entry.Value);
                return;
            }

            if (_hiddenRootStates.ContainsKey(scene.handle)) return;
            var roots = scene.GetRootGameObjects();
            var states = new Dictionary<GameObject, bool>();
            _hiddenRootStates.Add(scene.handle, states);
            foreach (var root in roots) if (root) states[root] = root.activeSelf;
            foreach (var t in roots)
            {
                if (t) t.SetActive(active);
            }
        }
    }
}
