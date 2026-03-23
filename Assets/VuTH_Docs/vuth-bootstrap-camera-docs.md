# VuTH Unity Framework - Bootstrap and Camera Systems Documentation

## Table of Contents
1. [Bootstrap System](#bootstrap-system)
2. [Camera System](#camera-system)
3. [Common Infrastructure](#common-infrastructure)

---

## Bootstrap System

The Bootstrap system is responsible for initializing and managing core game managers at startup. It uses a profile-based approach to define which managers to instantiate.

### Architecture Overview

```
BootstrapManagerCentral (MonoBehaviour)
    └── Loads BootstrapProfile
            └── Contains array of manager prefabs (VBootstrapManager)
```

### Key Components

#### 1. BootstrapManagerCentral

**Purpose:** Central bootstrapper that loads and initializes all game managers defined in a BootstrapProfile.

**Key Responsibilities:**
- Loads bootstrap manager prefabs from a BootstrapProfile
- Instantiates manager prefabs at runtime
- Supports both runtime and VContainer dependency injection modes
- Provides error logging when profile is missing

**Key Properties/Fields:**
| Field | Type | Description |
|-------|------|-------------|
| `boostrapProfile` | BootstrapProfile | Reference to the profile asset (ReadOnly, SerializeField) |
| `_vBootstrapManager` | ICommonManager[] | Array of instantiated managers |

**Key Methods:**
| Method | Description |
|--------|-------------|
| `LoadBootstrapManagers()` | Loads and instantiates all manager prefabs from profile |
| `EnsureProfileSet()` | Ensures profile is assigned; tries to load from Resources if null |

**Pattern: IBootstrapVContainerConfigurator**
- Implements `IBootstrapVContainerConfigurator` interface for VContainer DI integration
- `ConfigureRootScope(IContainerBuilder builder)` - Registers managers with VContainer when using DI

**Lifecycle:**
- In non-VContainer mode: Loads managers in `Awake()`
- In VContainer mode: Loads managers in `ConfigureRootScope()`

---

#### 2. BootstrapProfile (ScriptableObject)

**Purpose:** Configuration asset that defines which manager prefabs to bootstrap at game startup.

**Key Responsibilities:**
- Stores array of manager prefabs to instantiate
- Editor-time validation to ensure prefabs contain valid `VBootstrapManager<,>` components

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `boostrapPrefabs` | GameObject[] | Array of prefabs to instantiate as managers |

**Editor Validation (Unity Editor only):**
- `OnValidate()` - Validates each prefab contains exactly ONE `VBootstrapManager<,>` component
- Logs error if:
  - Prefab slot is empty
  - Prefab has zero or more than one VBootstrapManager components

---

#### 3. BootstrapProfileUtilities

**Purpose:** Utility class for loading and creating BootstrapProfile assets.

**Key Responsibilities:**
- Provides `TryGetProfile()` to load the BootstrapProfile
- Creates new profile if it doesn't exist (Editor mode only)
- Handles both runtime (Resources.Load) and Editor (AssetDatabase) loading

**Key Methods:**
| Method | Description |
|--------|-------------|
| `TryGetProfile(out BootstrapProfile profile)` | Attempts to load profile; returns true if found/created |

**Loading Strategy:**
- **Runtime:** Uses `Resources.Load<BootstrapProfile>(path)`
- **Editor (Play Mode):** Uses `AssetDatabase.LoadAssetAtPath<path>()`
- **Editor (Edit Mode):** Creates new profile if not found at configured path

---

#### 4. BootstrapManagerCentralConst

**Purpose:** Constants defining paths for the BootstrapProfile asset.

**Constants:**
| Constant | Value | Description |
|----------|-------|-------------|
| `BootstrapProfilePath` | `"Bootstrap/BootstrapManagerCentralProfile"` | Path for Resources.Load |
| `AbsoluteBootstrapProfilePath` | `"Assets/_VuTH/Core/Resources/Bootstrap/BootstrapManagerCentralProfile.asset"` | Path for AssetDatabase |

---

### Bootstrap System Patterns

1. **Profile-Based Configuration:** Managers to load are defined in a ScriptableObject profile, not hardcoded
2. **Singleton Pattern:** Managers inherit from `VBootstrapManager<>` which uses `VSingleton<>` for singleton access
3. **Interface-Based:** All managers implement `ICommonManager` interface
4. **Lifecycle Hooks:** `InitializeBootstrap()` / `DeinitializeBootstrap()` abstract methods for manager-specific initialization
5. **VContainer Integration:** Optional DI container support via `IBootstrapVContainerConfigurator`

---

## Camera System

The Camera system provides a flexible, profile-driven camera management system with smooth transitions and override capabilities.

### Architecture Overview

```
CameraManager (VBootstrapManager)
    ├── ICameraManager (interface)
    ├── Uses CameraProfile for configuration
    └── Supports Override Stack for temporary camera states

VirtualCamera (MonoBehaviour)
    └── Applies its profile to CameraManager on enable/init
```

---

### Key Components

#### 1. ICameraManager (Interface)

**Purpose:** Defines the contract for camera management functionality.

**Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `MainCamera` | UnityEngine.Camera | Reference to the managed camera |
| `IsTransitioning` | bool | Whether a transition is in progress |
| `IsOverriding` | bool | Whether an override is currently active |

**Methods:**
| Method | Return | Description |
|--------|--------|-------------|
| `ApplyProfile(CameraProfile)` | UniTask | Apply camera config for a Screen |
| `ResetProfile()` | UniTask | Reset camera to default state |
| `PushOverride(CameraProfile)` | UniTask | Push high-priority override (cutscene, etc.) |
| `PopOverride()` | UniTask | Pop override and restore previous state |

**Events:**
| Event | Signature | Description |
|-------|-----------|-------------|
| `OnProfileApplied` | `Action<CameraProfile>` | Fired when profile is applied |
| `OnOverridePushed` | `Action<CameraProfile>` | Fired when override is pushed |
| `OnOverridePopped` | `Action` | Fired when override is popped |

---

#### 2. CameraManager

**Purpose:** Main camera controller that manages the main Unity camera, applies profiles, and handles transitions.

**Key Responsibilities:**
- Manages the main camera (supports assigned or tag-based discovery)
- Applies CameraProfile configurations (position, rotation, FOV/ortho size, projection)
- Provides smooth transitions using PrimeTween
- Maintains override stack for system-level camera control
- Supports pending base profiles during overrides

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `MainCamera` | UnityEngine.Camera | The managed camera |
| `IsTransitioning` | bool | Whether transition animation is active |
| `IsOverriding` | bool | Whether override stack has items |

**Key Methods:**
| Method | Description |
|--------|-------------|
| `InitializeBootstrap()` | Captures default profile, sets up camera baseline |
| `DeinitializeBootstrap()` | Cleans up tweens and clears state |
| `ApplyProfile(CameraProfile)` | Applies profile to camera (queues if overriding) |
| `ResetProfile()` | Resets to captured default profile |
| `PushOverride(CameraProfile)` | Pushes override onto stack |
| `PopOverride()` | Pops override, restores previous or base profile |

**Transition System:**
- Uses **PrimeTween** for smooth animations
- Supports simultaneous position, rotation, and lens (FOV/ortho size) transitions
- Uses `Ease.InOutCubic` easing
- Duration defined per-profile via `transitionDuration`

**Override Stack Behavior:**
1. When overriding, `ApplyProfile()` queues as pending instead of applying
2. `PushOverride()` applies immediately and adds to stack
3. `PopOverride()` removes from stack, applies next override or restores base
4. Pending base profile is applied when override stack becomes empty

---

#### 3. CameraProfile (Serializable)

**Purpose:** Serializable data container for camera configuration.

**Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `useOrthographic` | bool | Use orthographic vs perspective projection |
| `fieldOfView` | float | Field of view (degrees) for perspective |
| `orthographicSize` | float | Orthographic size for orthographic |
| `worldPosition` | Vector3 | Absolute world position |
| `worldEulerRotation` | Vector3 | Absolute world rotation (Euler angles) |
| `transitionDuration` | float | Duration of transition animation |

**Notes:**
- All positions/rotations are in **absolute world space** (not relative)
- `transitionDuration = 0` results in instant (cut) transition

---

#### 4. VirtualCamera

**Purpose:** Component-based camera preset that applies its profile to CameraManager.

**Key Responsibilities:**
- Associates a CameraProfile with a GameObject transform
- Automatically applies its profile to CameraManager on enable/initialize
- Supports optional anchor for relative positioning

**Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `profile` | CameraProfile | Camera configuration to apply |
| `anchor` | Transform | Optional anchor for relative positioning |
| `initStrategy` | VirtualCameraInitStrategy | When to initialize |

**Initialization Strategies:**
| Strategy | Description |
|----------|-------------|
| `OnStart` | Initialize in `OnEnable()` (default) |
| `VInitializeCall` | Initialize via `VInitialize()` (for ordered init) |

**Pattern: IVInitializable**
- Implements `IVInitializable` for integration with VuTH's initialization system

---

### Camera System Patterns

1. **Profile-Driven:** Camera configurations are data (CameraProfile) not code
2. **Override Stack:** System-level camera control via push/pop stack (for cutscenes, menus)
3. **Async/Await:** All operations return `UniTask` for async handling
4. **Tween-Based Transitions:** Smooth animations using PrimeTween
5. **Event-Driven:** Public events for profile application and override changes
6. **Dual Init Strategies:** Supports both Unity lifecycle (OnStart) and custom ordered init (VInitialize)

---

## Common Infrastructure

### ICommonManager

**Interface that all managers implement:**
```csharp
public interface ICommonManager
{
    bool IsEnabledSystem { get; }
    void EnableSystem(bool enable);
    void ToggleSystem();
}
```

---

### VBootstrapManager<T, TI>

**Base class for all bootstrap managers:**
- Inherits from `VManager<T, TI>`
- Always enabled (returns true for `IsEnabledSystem`)
- Provides `InitializeBootstrap()` / `DeinitializeBootstrap()` abstract methods

**Inheritance Chain:**
```
VSingleton<T, TI>
    └── VManager<T, TI>
            └── VBootstrapManager<T, TI>
                    └── CameraManager
```

---

### VSingleton<T, TI>

**Generic singleton base for MonoBehaviours:**
- Implements singleton pattern with `Instance` and `HasInstance` static properties
- `DontDestroyOnLoad` for persistent managers
- Destroys duplicate instances
- Optional VContainer integration via `ConfigureRootScope()`

---

## Component Interactions

### Bootstrap → Camera Flow

```
BootstrapManagerCentral.Awake()
    └── Loads BootstrapProfile
            └── Instantiates CameraManager prefab
                    └── CameraManager.InitializeBootstrap()
                            └── Captures default from Camera.main
                                    └── Ready for VirtualCamera or ScreenManager calls
```

### Screen/Gameplay → Camera Flow

```
ScreenManager.Enter(screen)
    └── CameraManager.ApplyProfile(screen.CameraProfile)
            └── Smooth transition via PrimeTween
                    └── OnProfileApplied event fires
```

### Cutscene/Override Flow

```
CutsceneSystem.Start()
    └── CameraManager.PushOverride(cutsceneProfile)
            └── Applies immediately, pushes to stack
                    └── OnOverridePushed event fires

CutsceneSystem.End()
    └── CameraManager.PopOverride()
            └── Pops from stack, restores previous/base
                    └── OnOverridePopped event fires
```

### VirtualCamera Flow

```
VirtualCamera.OnEnable()
    └── Init() called (if OnStart strategy)
            └── CameraManager.Instance.ApplyProfile(profile)
                    └── Camera transitions to VirtualCamera's profile
```

---

## Summary

| System | Purpose | Key Classes |
|--------|---------|-------------|
| **Bootstrap** | Initialize game managers at startup | `BootstrapManagerCentral`, `BootstrapProfile`, `BootstrapProfileUtilities` |
| **Camera** | Profile-driven camera control with transitions | `CameraManager`, `CameraProfile`, `VirtualCamera`, `ICameraManager` |

**Patterns Used:**
- Profile-based configuration (ScriptableObject)
- Singleton pattern with generic inheritance
- Interface-based contracts
- Async operations with UniTask
- Tween-based animations
- Stack-based state management (overrides)
- Optional dependency injection (VContainer)
