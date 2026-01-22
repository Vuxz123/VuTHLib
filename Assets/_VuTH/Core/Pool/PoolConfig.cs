using System;
using UnityEngine;

namespace Core.Pool
{
    /// <summary>
    /// Overflow behavior khi pool đầy
    /// </summary>
    public enum OverflowBehavior
    {
        [Tooltip("Tạo thêm object mới (mặc định)")]
        Expand,
        [Tooltip("Trả về null khi pool đầy")]
        ReturnNull,
        [Tooltip("Recycle object cũ nhất đang active")]
        RecycleOldest
    }
    
    /// <summary>
    /// Configuration cho từng prefab pool - Warmup, Size limits, Overflow handling
    /// </summary>
    [Serializable]
    public class PoolConfig
    {
        [Header("Prefab")]
        public GameObject prefab;
        
        [Header("Pool Size")]
        [Tooltip("Số lượng objects pre-spawn khi warmup")]
        public int preloadCount = 5;
        
        [Tooltip("Giới hạn tối đa objects ACTIVE cùng lúc. 0 = unlimited")]
        public int maxSize = 100;
        
        [Tooltip("Giới hạn tối đa objects trong pool (inactive). 0 = unlimited")]
        public int maxPoolSize = 50;
        
        [Header("Overflow Handling")]
        [Tooltip("Hành vi khi vượt quá maxSize")]
        public OverflowBehavior overflowBehavior = OverflowBehavior.Expand;
        
        [Header("Category & Cleanup")]
        [Tooltip("Category để group pools (optional)")]
        public string category;
        
        [Tooltip("Tự động cleanup pool không sử dụng")]
        public bool enableAutoCleanup = false;
        
        [Tooltip("Thời gian không sử dụng trước khi cleanup (seconds)")]
        public float cleanupInterval = 60f;
        
        [Header("Memory")]
        [Tooltip("Ước tính size của 1 object (bytes) - dùng cho analytics")]
        public long estimatedSizeBytes = 1024;
    }
    
    /// <summary>
    /// Configuration cho C# class pool
    /// </summary>
    [Serializable]
    public class ClassPoolConfig
    {
        public int maxSize = 100;
        public bool throwOnOverflow = false;
    }
}