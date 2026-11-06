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
        public int poolHits;    // taken from the pool (reused)
        public int poolMisses;  // had to be created
        
        // Timing
        public float lastAccessTime;
        public float createdTime;
        
        /// <summary>
        /// Share of spawns served from the pool; higher is better
        /// </summary>
        public float HitRate => (poolHits + poolMisses) > 0 
            ? (float)poolHits / (poolHits + poolMisses) 
            : 0f;
        
        /// <summary>
        /// Despawns per spawn
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