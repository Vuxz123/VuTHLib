# VuTH Pool System Documentation

The VuTH Pool system is a comprehensive object pooling solution for Unity that reduces instantiation/destruction overhead. It implements a layered interface design with full analytics, category management, and overflow handling.

---

## Table of Contents

1. [PoolManager](#poolmanager)
2. [IPoolable](#ipoolable)
3. [IPoolManager & IPoolLayers](#ipoolmanager--ipoollayers)
4. [PoolConfig](#poolconfig)
5. [PoolExtensions](#poolextensions)
6. [PoolAnalytics](#poolanalytics)
7. [PoolStats](#poolstats)
8. [Overflow Behaviors](#overflow-behaviors)

---

## PoolManager

### Purpose & What It Does

The main pooling system that manages both GameObject and C# class pooling. It's a singleton manager that handles:
- Spawning/despawning pooled objects
- Pre-warming pools on startup
- Tracking object lifecycle
- Managing categories and cleanup
- Providing analytics and metrics

### How Object Pooling Works

1. **Pool Creation**: When a prefab is first spawned, a pool is automatically created (or via explicit warmup)
2. **Object Reuse**: When despawned, objects are not destroyed - they're returned to the queue
3. **Spawn Flow**:
   - Try to get from pool queue → if exists, reuse it (Pool Hit)
   - If pool empty → check overflow behavior → instantiate new or recycle (Pool Miss)
   - Call `OnSpawn()` on all `IPoolable` components
   - Track in spawn order queue for potential recycling
4. **Despawn Flow**:
   - Call `OnDespawn()` on all `IPoolable` components
   - Set inactive and return to pool queue
   - Optionally destroy excess if `maxPoolSize` exceeded

### Analytics & Tracking Features

- **Hit/Miss Tracking**: Records pool hits (reuse) vs misses (new instantiation)
- **Spawn/Despawn Counts**: Total counts per pool
- **Active/Pooled Counts**: Real-time tracking of active and pooled objects
- **Peak Active**: Tracks maximum concurrent active objects
- **Memory Estimation**: Calculates estimated memory usage based on `estimatedSizeBytes` config
- **Events**: `OnObjectSpawned`, `OnObjectDespawned`, `OnPoolOverflow`

---

## IPoolable

### Purpose & What It Does

Interface for poolable objects to receive lifecycle callbacks. Replaces Unity's `Start`/`OnEnable` and `OnDisable`/`Destroy` with explicit pool-aware callbacks.

```csharp
public interface IPoolable
{
    void OnSpawn();   // Called when retrieved from pool (replaces Start/OnEnable)
    void OnDespawn(); // Called when returned to pool (replaces OnDisable/Destroy)
}
```

### How It Works

1. Implement `IPoolable` on any MonoBehaviour that needs pool awareness
2. `OnSpawn()` is called when the object is retrieved from the pool and activated
3. `OnDespawn()` is called when the object is returned to the pool and deactivated
4. PoolManager finds all `IPoolable` components in children via `GetComponentsInChildren`

### Analytics & Tracking

No direct analytics - this is a lifecycle interface. Use `PoolStats` on the manager to track spawn/despawn events.

---

## IPoolManager & IPoolLayers

### Purpose & What It Does

The `IPoolManager` is a facade that combines multiple specialized interfaces into one easy-to-use API. The system is organized into 4 layers:

| Layer | Interface | Responsibility |
|-------|-----------|----------------|
| 1 | `IPoolOperations` | Basic spawn/despawn for GameObjects and C# classes |
| 2 | `IPoolLifecycle` | Warmup, cleanup, pool management |
| 3 | `IPoolOrganization` | Category grouping and batch operations |
| 4 | `IPoolAnalyticsProvider` | Statistics, metrics, debugging |

### Key Methods

**IPoolOperations:**
- `Spawn<T>(prefab, position, rotation, parent)` - Spawn pooled GameObject
- `Despawn(obj, delay)` - Return to pool with optional delay
- `SpawnClass<T>()` / `DespawnClass<T>()` - Pool C# classes

**IPoolLifecycle:**
- `WarmupPool(config)` / `WarmupPools()` - Pre-spawn objects
- `ClearPool(prefab)` / `ClearAllPools()` - Remove pools
- `TrimExcess(keepMinimum)` - Reduce pool sizes
- `CleanupUnused(seconds)` - Auto-cleanup unused pools

**IPoolOrganization:**
- `DespawnCategory(category, delay)` - Batch despawn by category
- `ClearCategory(category)` - Remove category
- `GetCategories()` / `GetCategoryCount(category)` - Query categories

**IPoolAnalyticsProvider:**
- `GetPrefabStats(prefab)` / `GetAllStats()` - Get per-pool stats
- `GetMetrics()` - Aggregate metrics
- `GetDebugInfo()` - Dictionary for debugging
- Events: `OnObjectSpawned`, `OnObjectDespawned`, `OnPoolOverflow`

### PoolSpawnOptions

Fluent options pattern for clean spawn API:

```csharp
// Basic
pool.Spawn(bulletPrefab, position, rotation, parent);

// With options
pool.Spawn(bulletPrefab, new PoolSpawnOptions 
{
    Position = Vector3.zero,
    Rotation = Quaternion.identity,
    Parent = transform,
    Category = "Bullets",
    AutoRecycleTime = 5f
});

// Static helpers
pool.Spawn(bulletPrefab, PoolSpawnOptions.At(position));
pool.Spawn(bulletPrefab, PoolSpawnOptions.At(position, rotation).WithCategory("Effects"));
```

---

## PoolConfig

### Purpose & What It Does

Per-prefab configuration that controls pool behavior, limits, and overflow handling.

### Configuration Properties

| Property | Type | Description |
|----------|------|-------------|
| `prefab` | GameObject | The prefab to pool |
| `preloadCount` | int | Number of objects to pre-spawn on warmup |
| `maxSize` | int | Max active objects (0 = unlimited) |
| `maxPoolSize` | int | Max inactive objects in pool (0 = unlimited) |
| `overflowBehavior` | OverflowBehavior | What to do when `maxSize` exceeded |
| `category` | string | Optional category for grouping |
| `enableAutoCleanup` | bool | Auto-cleanup when unused |
| `cleanupInterval` | float | Seconds before cleanup (default 60s) |
| `estimatedSizeBytes` | long | Memory estimate per object (for analytics) |

### How It Works

1. Create `PoolConfig` with desired settings
2. Add to `PoolManager.poolConfigs` list in Inspector
3. Call `WarmupPools()` to apply configurations
4. Or use programmatic warmup: `WarmupPool(prefab, preloadCount, maxSize)`

---

## PoolExtensions

### Purpose & What It Does

Helper extension methods and utilities that make pool usage more convenient and expressive.

### Extension Methods

**GameObject Extensions:**
```csharp
// Return to pool (with optional delay)
obj.ReturnToPool();
obj.ReturnToPool(2f); // 2 second delay

// Auto despawn when inactive
obj.AutoDespawnWhenInactive();

// Despawn after ParticleSystem finishes
particleObj.ReturnToPoolWhenParticleDone();

// Spawn multiple at once
var bullets = bulletPrefab.SpawnMultiple(10, positions);

// Spawn at transform position
spawnPoint.SpawnAt(bulletPrefab);
```

**Component Extensions:**
```csharp
// Spawn as child of component
component.SpawnChild(effectPrefab);

// Despawn this object's GameObject
component.DespawnSelf();
```

**Collection Extensions:**
```csharp
// Despawn all in collection
enemyList.DespawnAll();
projectileList.DespawnAndClear();
```

### Utility Classes

| Class | Purpose |
|-------|---------|
| `AutoDespawnBehaviour` | Auto-returns object to pool when inactive |
| `AutoDespawnParticle` | Auto-returns after ParticleSystem completes |
| `AutoDespawnTimer` | Auto-returns after set lifetime |
| `PoolStatsTracker` | OnGUI display of pool statistics |
| `LazyPool<T>` | Lazy initialization wrapper |
| `PooledObject<T>` | IDisposable pattern for pooled objects |
| `PooledWaveSpawner` | Wave spawning with pool optimization |

---

## PoolAnalytics

### Purpose & What It Does

Tracks performance metrics across all pools to help identify optimization opportunities.

### Analytics Metrics

| Metric | Description |
|--------|-------------|
| `Hits` | Number of times object was retrieved from pool (reuse) |
| `Misses` | Number of times new object was instantiated |
| `Spawns` | Total spawn calls |
| `Despawns` | Total despawn calls |
| `Overflows` | Number of times overflow behavior triggered |

### Key Methods

```csharp
// Per-prefab analytics
analytics.GetHitRate(prefabId);           // 0-1 float
analytics.GetLowestHitRatePools(5);        // Bottom 5 pools to optimize
analytics.GetMostOverflowPools(5);          // Pools with most overflows

// Global analytics
analytics.GetOverallHitRate();              // Overall system hit rate
analytics.GetSummary();                      // AnalyticsSummary struct
analytics.Reset();                           // Clear all data
```

### AnalyticsSummary

```csharp
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
```

---

## PoolStats

### Purpose & What It Does

Per-pool statistics tracking for monitoring individual pool performance.

### Stats Properties

| Property | Type | Description |
|----------|------|-------------|
| `totalSpawned` | int | Total times this prefab was spawned |
| `totalDespawned` | int | Total times returned to pool |
| `activeCount` | int | Currently active objects |
| `pooledCount` | int | Objects waiting in pool |
| `peakActive` | int | Peak concurrent active objects |
| `poolHits` | int | Times reused from pool |
| `poolMisses` | int | Times had to instantiate new |
| `lastAccessTime` | float | Last spawn/despawn timestamp |
| `createdTime` | float | When pool was created |

### Calculated Properties

```csharp
// Hit rate: ratio of pool reuse (higher = better)
poolStats.HitRate;  // poolHits / (poolHits + poolMisses)

// Reuse rate: ratio of despawns to spawns
poolStats.ReuseRate; // totalDespawned / totalSpawned
```

### Usage

```csharp
var stats = poolManager.GetPrefabStats(bulletPrefab);
Debug.Log($"Hit Rate: {stats.HitRate:P0}");      // e.g., "Hit Rate: 87%"
Debug.Log($"Peak Active: {stats.peakActive}");

var allStats = poolManager.GetAllStats();
foreach (var kvp in allStats)
{
    Debug.Log($"{kvp.Key}: {kvp.Value.activeCount} active");
}
```

---

## Overflow Behaviors

### Purpose & What It Does

Defines what happens when a pool's `maxSize` limit is reached and a new spawn is attempted.

### Available Behaviors

| Behavior | Enum Value | Description |
|----------|------------|-------------|
| **Expand** | `OverflowBehavior.Expand` | (Default) Create new object beyond maxSize |
| **ReturnNull** | `OverflowBehavior.ReturnNull` | Return null, don't spawn |
| **RecycleOldest** | `OverflowBehavior.RecycleOldest` | Despawn oldest active, spawn new |

### How Each Works

**Expand (Default):**
```csharp
// If maxSize reached, just create more
// Safe but may exceed memory limits
config.overflowBehavior = OverflowBehavior.Expand;
```

**ReturnNull:**
```csharp
// Returns null - caller must handle
// Good for strict memory management
var bullet = pool.Spawn(bulletPrefab);
if (bullet == null) {
    Debug.LogWarning("Pool exhausted!");
}
config.overflowBehavior = OverflowBehavior.ReturnNull;
```

**RecycleOldest:**
```csharp
// Despawns the oldest active object (FIFO from SpawnOrder)
// Maintains exact maxSize limit
// Useful for particle effects, bullets, etc.
config.overflowBehavior = OverflowBehavior.RecycleOldest;
```

### Event Handling

```csharp
poolManager.OnPoolOverflow += prefabId => {
    Debug.LogWarning($"Pool {prefabId} overflowed! Consider increasing maxSize.");
};
```

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    IPoolManager                             │
│              (Facade - combines all layers)                 │
├─────────────────────────────────────────────────────────────┤
│  Layer 1     │  Layer 2      │  Layer 3    │  Layer 4       │
│  IPoolOps    │  IPoolLife    │  IPoolOrg   │  IPoolAnalytics│
├──────────────┼───────────────┼─────────────┼────────────────┤
│  Spawn/      │  Warmup/      │  Categories │  Stats/        │
│  Despawn     │  Cleanup      │  Batch ops  │  Metrics       │
└──────┬───────┴───────┬───────┴──────┬──────┴───────┬────────┘
       │               │              │              │
       ▼               ▼              ▼              ▼
┌──────────────────────────────────────────────────────────────┐
│                      PoolManager                             │
│  - _prefabPools (Dictionary<int, PoolData>)                  │
│  - _classPools (Dictionary<Type, ClassPoolData>)             │
│  - _categories (Dictionary<string, HashSet<int>>)           │
│  - _analytics (PoolAnalytics)                                │
└──────────────────────────────────────────────────────────────┘
```

---

## Quick Usage Examples

### Basic Spawn/Despawn
```csharp
var bullet = poolManager.Spawn(bulletPrefab, firePoint.position, firePoint.rotation);
poolManager.Despawn(bullet.gameObject);
```

### With PoolSpawnOptions
```csharp
poolManager.Spawn(effectPrefab, new PoolSpawnOptions 
{
    Position = transform.position,
    Rotation = transform.rotation,
    Category = "VFX",
    AutoRecycleTime = 3f  // Auto-return after 3 seconds
});
```

### Using Extensions
```csharp
bulletPrefab.SpawnMultiple(5, positions);
effectPrefab.SpawnAt(spawnPoint);
bullet.ReturnToPool(2f);
```

### Category-based Cleanup
```csharp
poolManager.DespawnCategory("Enemies");
poolManager.ClearCategory("LevelEffects");
```

### Analytics
```csharp
var metrics = poolManager.GetMetrics();
Debug.Log($"Hit Rate: {metrics.OverallHitRate:P0}");
Debug.Log($"Active Objects: {metrics.TotalActiveObjects}");

var summary = poolManager.GetAnalytics().GetSummary();
```
