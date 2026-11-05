#nullable enable
using System;
using System.Collections.Generic;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// The packages managed by <see cref="DataPersistenceManager"/>: no duplicates, no shared storage key, lookup by type.
    /// </summary>
    internal sealed class PackageRegistry
    {
        private readonly List<IPersistencePackage> _packages = new();
        private readonly Dictionary<Type, IPersistencePackage> _typeCache = new();

        public int Count => _packages.Count;

        public bool Contains(IPersistencePackage package) => _packages.Contains(package);

        /// <summary>
        /// Adds <paramref name="package"/>. Returns false if it is already registered (<paramref name="error"/> null) or
        /// its storage key is taken (<paramref name="error"/> says by whom).
        /// </summary>
        public bool TryAdd(IPersistencePackage package, out string? error)
        {
            error = null;
            if (_packages.Contains(package)) return false;

            foreach (var existing in _packages)
            {
                if (existing.StorageKey != package.StorageKey) continue;

                error = $"Package {package.GetType().Name} uses the storage key '{package.StorageKey}' of " +
                        $"{existing.GetType().Name}; it is not registered, as they would overwrite each other's save.";
                return false;
            }

            _packages.Add(package);
            return true;
        }

        public bool Remove(IPersistencePackage package)
        {
            if (!_packages.Remove(package)) return false;

            // Any cached type may point at it.
            _typeCache.Clear();
            return true;
        }

        /// <summary>The package of exactly type <typeparamref name="T"/>, else the first assignable to it.</summary>
        public T? Find<T>() where T : class, IPersistencePackage
        {
            var type = typeof(T);
            if (_typeCache.TryGetValue(type, out var cached)) return (T)cached;

            IPersistencePackage? found = null;
            foreach (var package in _packages)
            {
                if (package.GetType() == type)
                {
                    found = package;
                    break;
                }

                found ??= package as T;
            }

            if (found != null) _typeCache[type] = found;
            return found as T;
        }

        /// <summary>A copy, safe to iterate while packages are (un)registered.</summary>
        public List<IPersistencePackage> Snapshot() => new(_packages);

        /// <summary>The first package not in <paramref name="handled"/>, in registration order.</summary>
        public bool TryGetFirstExcept(Func<IPersistencePackage, bool> handled, out IPersistencePackage package)
        {
            foreach (var candidate in _packages)
            {
                if (handled(candidate)) continue;

                package = candidate;
                return true;
            }

            package = null!;
            return false;
        }

        public void Clear()
        {
            _packages.Clear();
            _typeCache.Clear();
        }
    }
}
