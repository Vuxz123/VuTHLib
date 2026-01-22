using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Pool
{
    /// <summary>
    /// Analytics & Profiling cho Pool system
    /// </summary>
    public class PoolAnalytics
    {
        // Prefab pool metrics
        private readonly Dictionary<int, PoolInternalMetrics> _prefabMetrics = new();
        
        // Class pool metrics
        private readonly Dictionary<Type, PoolInternalMetrics> _classMetrics = new();
        
        // Global stats
        private int _totalSpawns;
        private int _totalDespawns;
        private int _totalPoolHits;
        private int _totalPoolMisses;
        
        #region Recording
        
        public void RecordPoolHit(int prefabId)
        {
            GetOrCreatePrefabMetrics(prefabId).Hits++;
            _totalPoolHits++;
        }
        
        public void RecordPoolMiss(int prefabId)
        {
            GetOrCreatePrefabMetrics(prefabId).Misses++;
            _totalPoolMisses++;
        }
        
        public void RecordClassPoolHit(Type type)
        {
            GetOrCreateClassMetrics(type).Hits++;
            _totalPoolHits++;
        }
        
        public void RecordClassPoolMiss(Type type)
        {
            GetOrCreateClassMetrics(type).Misses++;
            _totalPoolMisses++;
        }
        
        public void RecordSpawn(int prefabId)
        {
            GetOrCreatePrefabMetrics(prefabId).Spawns++;
            _totalSpawns++;
        }
        
        public void RecordDespawn(int prefabId)
        {
            GetOrCreatePrefabMetrics(prefabId).Despawns++;
            _totalDespawns++;
        }
        
        public void RecordOverflow(int prefabId)
        {
            GetOrCreatePrefabMetrics(prefabId).Overflows++;
        }
        
        #endregion
        
        #region Queries
        
        /// <summary>
        /// Hit rate cho prefab cụ thể (0-1)
        /// </summary>
        public float GetHitRate(int prefabId)
        {
            if (!_prefabMetrics.TryGetValue(prefabId, out var metrics)) return 0f;
            var total = metrics.Hits + metrics.Misses;
            return total > 0 ? (float)metrics.Hits / total : 0f;
        }
        
        /// <summary>
        /// Hit rate tổng thể (0-1)
        /// </summary>
        public float GetOverallHitRate()
        {
            var total = _totalPoolHits + _totalPoolMisses;
            return total > 0 ? (float)_totalPoolHits / total : 0f;
        }
        
        /// <summary>
        /// Top N prefabs có hit rate thấp nhất (cần optimize)
        /// </summary>
        public IEnumerable<(int prefabId, float hitRate)> GetLowestHitRatePools(int count = 5)
        {
            return _prefabMetrics
                .Where(kvp => kvp.Value.Hits + kvp.Value.Misses > 10) // Chỉ xét pools có đủ data
                .Select(kvp => (kvp.Key, GetHitRate(kvp.Key)))
                .OrderBy(x => x.Item2)
                .Take(count);
        }
        
        /// <summary>
        /// Top N prefabs có nhiều overflow nhất
        /// </summary>
        public IEnumerable<(int prefabId, int overflows)> GetMostOverflowPools(int count = 5)
        {
            return _prefabMetrics
                .Where(kvp => kvp.Value.Overflows > 0)
                .Select(kvp => (kvp.Key, overflows: kvp.Value.Overflows))
                .OrderByDescending(x => x.overflows)
                .Take(count);
        }
        
        /// <summary>
        /// Lấy summary cho debug
        /// </summary>
        public AnalyticsSummary GetSummary()
        {
            return new AnalyticsSummary
            {
                TotalSpawns = _totalSpawns,
                TotalDespawns = _totalDespawns,
                TotalPoolHits = _totalPoolHits,
                TotalPoolMisses = _totalPoolMisses,
                OverallHitRate = GetOverallHitRate(),
                PrefabPoolCount = _prefabMetrics.Count,
                ClassPoolCount = _classMetrics.Count
            };
        }
        
        #endregion
        
        #region Reset
        
        public void Reset()
        {
            _prefabMetrics.Clear();
            _classMetrics.Clear();
            _totalSpawns = 0;
            _totalDespawns = 0;
            _totalPoolHits = 0;
            _totalPoolMisses = 0;
        }
        
        #endregion
        
        #region Helpers
        
        private PoolInternalMetrics GetOrCreatePrefabMetrics(int prefabId)
        {
            if (!_prefabMetrics.TryGetValue(prefabId, out var metrics))
            {
                metrics = new PoolInternalMetrics();
                _prefabMetrics[prefabId] = metrics;
            }
            return metrics;
        }
        
        private PoolInternalMetrics GetOrCreateClassMetrics(Type type)
        {
            if (!_classMetrics.TryGetValue(type, out var metrics))
            {
                metrics = new PoolInternalMetrics();
                _classMetrics[type] = metrics;
            }
            return metrics;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Internal metrics per pool (renamed to avoid conflict with public PoolMetrics)
    /// </summary>
    internal class PoolInternalMetrics
    {
        public int Hits;
        public int Misses;
        public int Spawns;
        public int Despawns;
        public int Overflows;
    }
    
    /// <summary>
    /// Summary data cho UI/Debug
    /// </summary>
    public struct AnalyticsSummary
    {
        public int TotalSpawns;
        public int TotalDespawns;
        public int TotalPoolHits;
        public int TotalPoolMisses;
        public float OverallHitRate;
        public int PrefabPoolCount;
        public int ClassPoolCount;
    }
}
