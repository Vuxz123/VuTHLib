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

1. **Pool creation.** A pool is created the first time its prefab is spawned, or by a warmup.
2. **Per-instance state.** Each instance has its own state (`PooledInstance`): whether it is spawned, its owning
   pool, a generation number, its category, and its cached `IPoolable` components. The pool relies on it to stay
   consistent:
   - **A second despawn is refused**, with a warning. Before this fix, the instance went into the pool twice and two
     spawns handed out the same object.
   - **A scheduled despawn only applies to the life it was made for.** The generation changes on every spawn and
     despawn. `Despawn(obj, 2f)` followed by a manual despawn and a respawn elsewhere no longer despawns the new
     user.
   - **Instances destroyed from outside** (for example by a scene unload) are skipped when spawning. They no longer
     count against `maxSize` and are forgotten.
   - The spawn order used by `RecycleOldest` is a linked list. A despawn removes the instance in O(1).
3. **Spawn flow**:
   - Reuse an inactive instance (pool hit). Otherwise apply the overflow behavior, then create one (pool miss).
   - New instances are instantiated under an inactive staging object. `Awake` and `OnEnable` therefore run only when
     the instance is spawned, never during a warmup.
   - The instance is placed and activated, then `OnSpawn()` is called on its `IPoolable` components.
4. **Placement**:
   - `Spawn(prefab, parent: x)`, with neither a position nor a rotation, keeps the prefab's local transform under
     the parent: local position or anchors, rotation and scale. Use it for UI and child objects. It is correct
     whatever the parent's scale, for example a Canvas scaled by its CanvasScaler.
   - With a position or rotation, the instance is placed in world space and keeps the prefab's scale.
   - A default rotation `(0, 0, 0, 0)` means identity.
5. **Despawn flow**:
   - `OnDespawn()` is called, then the instance is deactivated and moved back under the root without keeping its
     world transform.
   - If `maxPoolSize` inactive instances are already pooled, the despawned instance is destroyed instead.

### Analytics & Tracking Features

- **Hit/Miss Tracking**: Records pool hits (reuse) vs misses (new instantiation)
- **Spawn/Despawn Counts**: Total counts per pool
- **Active/Pooled Counts**: Real-time tracking of active and pooled objects
- **Peak Active**: Tracks maximum concurrent active objects
- **Memory Estimation**: Calculates estimated memory usage based on `estimatedSizeBytes` config
- **Events**: `OnObjectSpawned`, `OnObjectDespawned`, `OnPoolOverflow`
- Turn off `enableAnalytics` on the manager to skip the statistics bookkeeping on every spawn and despawn.

### Manager Settings

| Field | Meaning |
|-------|---------|
| `rootPoolTransform` | Parent of the pooled (inactive) instances; created if empty |
| `poolConfigs` / `warmupOnAwake` | Pools warmed up at initialization |
| `delaysUseUnscaledTime` | Delayed despawns (`Despawn(obj, delay)`, auto recycle) count unscaled time, so they also run while the game is paused. Off by default: they wait with the gameplay |
| `enableAnalytics` | Record statistics and analytics |
| `enableLogging` | Verbose logs |

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
4. The `IPoolable` components in the instance and its children are found once, when the instance is created.
   Components added later are not notified.
5. An exception thrown by a callback is logged; it does not break the spawn or the despawn.

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
- `Spawn<T>(prefab, position, rotation, parent)` - Spawn pooled GameObject (see the placement rules above)
- `Despawn(obj, delay)` - Return to pool with optional delay; an object not spawned by the pool is destroyed
- `IsSpawned(obj)` - Whether the object is currently spawned by the pool
- `SpawnClass<T>()` / `DespawnClass(obj)` - Pool C# classes. The despawn finds the pool from the object, whatever
  static type it is passed as. A second despawn is refused.

**IPoolLifecycle:**
- `WarmupPool(config)` / `WarmupPools()` - Create the pool, or apply the config to the existing pool, and top it up
  to `preloadCount` inactive instances. An existing pool keeps its instances.
- `ClearPool(prefab)` / `ClearAllPools()` - Destroy the inactive instances and forget the pool. Instances still
  spawned are destroyed when they are despawned.
- `ClearPool<T>()` - Forget a class pool; its instances still out are dropped on despawn
- `ConfigureClassPool<T>(ClassPoolConfig)` - `maxSize`: instances kept in a class pool
- `TrimExcess(keepMinimum)` - Reduce pool sizes
- `CleanupUnused(seconds)` - Clear the pools with no active instance for that long

**IPoolOrganization:**
- `DespawnCategory(category, delay)` - Despawn the instances spawned with that category, and every instance of a pool
  configured with it. Other instances of the same prefab are left alone.
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
pool.Spawn(itemPrefab, PoolSpawnOptions.WithParent(listContent)); // UI: prefab's local transform under the parent
```

`KeepPrefabTransform` is the option behind `WithParent`: it keeps the prefab's local transform under `Parent`
instead of placing the instance at `Position` / `Rotation`.

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
| `enableAutoCleanup` | bool | Clear the pool once it has had no active instance for `cleanupInterval` seconds (checked every second) |
| `cleanupInterval` | float | Seconds without any active instance before the automatic cleanup (default 60s) |
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
// Return to pool (with optional delay); destroyed, with a warning, when there is no PoolManager
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
| `AutoDespawnBehaviour` | Returns the object to the pool when game code deactivates it (on the next update) |
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
