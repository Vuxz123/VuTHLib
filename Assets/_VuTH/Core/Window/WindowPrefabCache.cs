using System;
using System.Collections.Generic;
using System.Reflection;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace _VuTH.Core.Window
{
    /// <summary>Supplies the prefab of each window type to <see cref="WindowManager"/>.</summary>
    public interface IWindowPrefabProvider
    {
        /// <summary>The prefab of <paramref name="windowType"/>, or null (logged) when it cannot be loaded.</summary>
        UniTask<GameObject> GetOrLoad(Type windowType);

        /// <summary>Releases the prefab of <paramref name="windowType"/> if it is loaded.</summary>
        void Release(Type windowType);

        /// <summary>Releases every loaded prefab.</summary>
        void Clear();
    }

    /// <summary>
    /// Sets the Addressables key of a window prefab. Without it the key is the class name, which two classes with the
    /// same name in different namespaces would share.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class WindowAddressAttribute : Attribute
    {
        public string Address { get; }

        public WindowAddressAttribute(string address)
        {
            Address = address;
        }
    }

    /// <summary>
    /// Loads window prefabs through Addressables (key: <see cref="WindowAddressAttribute"/>, else the class name) and
    /// caches the <see cref="AsyncOperationHandle{TObject}"/> per window type so they can be released properly.
    /// </summary>
    public sealed class WindowPrefabCache : IWindowPrefabProvider
    {
        private readonly Dictionary<Type, AsyncOperationHandle<GameObject>> _prefabHandleCache = new();
        private readonly object _logOrigin;

        /// <summary>Creates a cache that logs through <paramref name="logOrigin"/>.</summary>
        public WindowPrefabCache(object logOrigin)
        {
            _logOrigin = logOrigin;
        }

        /// <summary>The Addressables key of <paramref name="windowType"/>.</summary>
        public static string GetAddress(Type windowType)
        {
            var attribute = windowType.GetCustomAttribute<WindowAddressAttribute>();
            return attribute != null && !string.IsNullOrEmpty(attribute.Address) ? attribute.Address : windowType.Name;
        }

        /// <summary>
        /// Returns the cached prefab for <paramref name="windowType"/>, awaiting an in-flight load if needed, or starts
        /// a new Addressables load. Returns null (and drops the handle) on failure.
        /// </summary>
        public async UniTask<GameObject> GetOrLoad(Type windowType)
        {
            var address = GetAddress(windowType);

            if (!_prefabHandleCache.TryGetValue(windowType, out var handle) || !handle.IsValid())
            {
                handle = Addressables.LoadAssetAsync<GameObject>(address);
                _prefabHandleCache[windowType] = handle;
            }

            try
            {
                if (!handle.IsDone) await handle.Task;
            }
            catch (Exception e)
            {
                _logOrigin.LogError($"Addressables load faulted for key '{address}': {e}");
                Drop(windowType, handle);
                return null;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || !handle.Result)
            {
                _logOrigin.LogError($"Addressables prefab not found for key '{address}'.");
                Drop(windowType, handle);
                return null;
            }

            return handle.Result;
        }

        /// <summary>Releases a failed handle once, even when several callers awaited it.</summary>
        private void Drop(Type windowType, AsyncOperationHandle<GameObject> handle)
        {
            if (!_prefabHandleCache.TryGetValue(windowType, out var cached) || !cached.Equals(handle)) return;

            _prefabHandleCache.Remove(windowType);
            if (handle.IsValid()) Addressables.Release(handle);
        }

        /// <summary>
        /// Releases the cached handle for <paramref name="windowType"/> if it finished loading; skips (with a warning)
        /// while it is still loading. Invalid handles are simply dropped.
        /// </summary>
        public void Release(Type windowType)
        {
            if (!_prefabHandleCache.TryGetValue(windowType, out var handle)) return;

            if (!handle.IsValid())
            {
                _prefabHandleCache.Remove(windowType);
                return;
            }

            // A handle still loading is awaited by someone: release it later.
            if (!handle.IsDone)
            {
                _logOrigin.LogWarning($"Release of the {windowType.Name} prefab skipped: it is still loading.");
                return;
            }

            Addressables.Release(handle);
            _prefabHandleCache.Remove(windowType);
        }

        /// <summary>Releases every completed handle and drops invalid ones; handles still loading are kept.</summary>
        public void Clear()
        {
            foreach (var windowType in new List<Type>(_prefabHandleCache.Keys))
            {
                Release(windowType);
            }
        }
    }
}
