using System.Collections.Generic;
using System.Linq;

namespace _VuTH.Core.Pool
{
    /// <summary>
    /// Groups prefab pools into named categories (category name → set of prefab ids).
    /// Pure C#; it only stores ids and knows nothing about the pools themselves.
    /// </summary>
    public sealed class PoolCategoryRegistry
    {
        private readonly Dictionary<string, HashSet<int>> _categories = new();

        /// <summary>Number of known categories.</summary>
        public int Count => _categories.Count;

        /// <summary>Names of all known categories (live view, do not mutate the registry while enumerating).</summary>
        public IEnumerable<string> Names => _categories.Keys;

        /// <summary>Adds <paramref name="prefabId"/> to <paramref name="category"/>, creating the category if needed.</summary>
        public void Add(string category, int prefabId)
        {
            if (!_categories.TryGetValue(category, out var prefabIds))
            {
                prefabIds = new HashSet<int>();
                _categories[category] = prefabIds;
            }
            prefabIds.Add(prefabId);
        }

        /// <summary>Gets the prefab ids registered under <paramref name="category"/>.</summary>
        public bool TryGetPrefabIds(string category, out IReadOnlyCollection<int> prefabIds)
        {
            if (_categories.TryGetValue(category, out var set))
            {
                prefabIds = set;
                return true;
            }

            prefabIds = null;
            return false;
        }

        /// <summary>Removes <paramref name="category"/> (the pools themselves are not affected).</summary>
        public bool Remove(string category) => _categories.Remove(category);

        /// <summary>Snapshot of all category names.</summary>
        public string[] GetNames() => _categories.Keys.ToArray();

        /// <summary>Removes all categories.</summary>
        public void Clear() => _categories.Clear();
    }
}
