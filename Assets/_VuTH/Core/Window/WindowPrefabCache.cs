using System;
using System.Collections.Generic;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using ZLinq;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Loads window prefabs through Addressables (keyed by the window type name) and caches the
    /// <see cref="AsyncOperationHandle{TObject}"/> per window type so they can be released properly.
    /// Log messages are emitted with the supplied log origin (the owning <see cref="WindowManager"/>).
    /// </summary>
    public sealed class WindowPrefabCache
    {
        // Cache Addressables prefab handles so we can release them properly.
        private readonly Dictionary<Type, AsyncOperationHandle<GameObject>> _prefabHandleCache = new();
        private readonly object _logOrigin;

        /// <summary>Creates a cache that logs through <paramref name="logOrigin"/>.</summary>
        public WindowPrefabCache(object logOrigin)
        {
            _logOrigin = logOrigin;
        }

        /// <summary>
        /// Returns the cached prefab for <paramref name="type"/>, awaiting an in-flight load if needed, or starts a new
        /// Addressables load keyed by <c>type.Name</c>. Returns null (and drops the handle) on failure.
        /// </summary>
        public async UniTask<GameObject> GetOrLoad(Type type)
        {
            if (_prefabHandleCache.TryGetValue(type, out var cachedHandle) && cachedHandle.IsValid())
            {
                // If handle hasn't completed yet, await completion.
                try
                {
                    if (!cachedHandle.IsDone)
                        await cachedHandle.Task;
                }
                catch (Exception e)
                {
                    _logOrigin.LogError($"Addressables load faulted for key '{type.Name}': {e}");
                    Addressables.Release(cachedHandle);
                    _prefabHandleCache.Remove(type);
                    return null;
                }

                if (!cachedHandle.Result)
                {
                    _logOrigin.LogError($"Addressables prefab not found for key '{type.Name}'");
                    Addressables.Release(cachedHandle);
                    _prefabHandleCache.Remove(type);
                    return null;
                }

                return cachedHandle.Result;
            }

            var handle = Addressables.LoadAssetAsync<GameObject>(type.Name);
            _prefabHandleCache[type] = handle;

            try
            {
                await handle.Task;
            }
            catch (Exception e)
            {
                _logOrigin.LogError($"Addressables load faulted for key '{type.Name}': {e}");
                if (handle.IsValid())
                    Addressables.Release(handle);
                _prefabHandleCache.Remove(type);
                return null;
            }

            if (!handle.Result)
            {
                _logOrigin.LogError($"Addressables prefab not found for key '{type.Name}'");
                // Avoid keeping a failed/empty handle.
                if (handle.IsValid())
                    Addressables.Release(handle);
                _prefabHandleCache.Remove(type);
                return null;
            }

            return handle.Result;
        }

        /// <summary>
        /// Releases the cached handle for <paramref name="type"/> if it finished loading; skips (with a warning)
        /// while it is still loading. Invalid handles are simply dropped.
        /// </summary>
        public void Release(Type type)
        {
            if (_prefabHandleCache.TryGetValue(type, out var handle) && handle.IsValid())
            {
                // If still loading, don't release mid-flight.
                // Caller can retry release after load completes.
                if (handle.IsDone)
                {
                    Addressables.Release(handle);
                    _prefabHandleCache.Remove(type);
                }
                else
                {
                    _logOrigin.LogWarning($"ReleasePrefab<{type.Name}> skipped because the Addressables handle is still loading.");
                }
            }
            else
            {
                _prefabHandleCache.Remove(type);
            }
        }

        /// <summary>
        /// Releases every completed handle and drops invalid ones; handles still loading are kept (with a warning).
        /// </summary>
        public void Clear()
        {
            foreach (var kv in _prefabHandleCache
                         .AsValueEnumerable().Where(kv => kv.Value.IsValid() && kv.Value.IsDone))
            {
                Addressables.Release(kv.Value);
            }

            // Keep any handles that are still loading; they can be cleared later.
            foreach (var kv in _prefabHandleCache
                         .AsValueEnumerable()
                         .Where(kv => kv.Value.IsValid() && !kv.Value.IsDone))
            {
                _logOrigin.LogWarning($"Skipped releasing prefab handle for {kv.Key.Name} because it is still loading.");
            }

            var keysToRemove = _prefabHandleCache
                .AsValueEnumerable()
                .Where(kv => !kv.Value.IsValid() || kv.Value.IsDone)
                .Select(kv => kv.Key)
                .ToArray(); // materialize: removing while lazily enumerating the same dictionary throws

            foreach (var key in keysToRemove)
                _prefabHandleCache.Remove(key);
        }
    }
}
