using System.Collections.Generic;
using Common;
using UnityEngine;

namespace Core.Pool
{
    /// <summary>
    /// =======================================================================
    /// FACADE: Complete Pool Manager
    /// Combines all layers into a single, easy-to-use interface
    /// 
    /// Implements:
    /// - IPoolOperations: Basic spawn/despawn
    /// - IPoolLifecycle: Warmup, cleanup
    /// - IPoolOrganization: Category management
    /// - IPoolAnalyticsProvider: Stats, metrics, events
    /// =======================================================================
    /// </summary>
    public interface IPoolManager : 
        ICommonManager,
        IPoolOperations,
        IPoolLifecycle,
        IPoolOrganization,
        IPoolAnalyticsProvider
    {
        /// <summary>
        /// Extended spawn with options pattern for clean API
        /// </summary>
        T Spawn<T>(T prefab, PoolSpawnOptions options) where T : Component;
    }
}