using System;

namespace _VuTH.Core.Pool
{
    /// <summary>
    /// Statistics cho pool monitoring & debugging
    /// </summary>
    [Serializable]
    public class PoolStats
    {
        public int prefabId;
        public string poolName;
        
        // Counters
        public int totalSpawned;
        public int totalDespawned;
        public int activeCount;
        public int pooledCount;
        public int peakActive;
        
        // Pool efficiency
        public int poolHits;    // Lấy từ pool (reuse)
        public int poolMisses;  // Phải tạo mới
        
        // Timing
        public float lastAccessTime;
        public float createdTime;
        
        /// <summary>
        /// Tỷ lệ reuse - cao = tốt
        /// </summary>
        public float HitRate => (poolHits + poolMisses) > 0 
            ? (float)poolHits / (poolHits + poolMisses) 
            : 0f;
        
        /// <summary>
        /// Tỷ lệ sử dụng lại objects
        /// </summary>
        public float ReuseRate => totalSpawned > 0 
            ? (float)totalDespawned / totalSpawned 
            : 0f;
        
        public void RecordSpawn(bool fromPool)
        {
            totalSpawned++;
            activeCount++;
            if (activeCount > peakActive) peakActive = activeCount;
            lastAccessTime = UnityEngine.Time.time;
            
            if (fromPool) poolHits++;
            else poolMisses++;
        }
        
        public void RecordDespawn()
        {
            totalDespawned++;
            activeCount--;
            lastAccessTime = UnityEngine.Time.time;
        }
        
        public void UpdatePooledCount(int count) => pooledCount = count;
    }
}