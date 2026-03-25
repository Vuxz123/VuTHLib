# VuTH Framework Documentation

A Unity game framework providing core infrastructure for game development including dependency injection, scene management, object pooling, UI/window management, screen/flow management, and persistence.

---

## Table of Contents

1. [Common](#common)
   - [DI (Dependency Injection)](#di-dependency-injection)
   - [Editor](#editor)
   - [Log](#log)
   - [MessagePipe](#messagepipe)
   - [Scene](#scene)
   - [Init](#init)
2. [Core](#core)
   - [Bootstrap](#bootstrap)
   - [Camera](#camera)
   - [GameCycle / Screen / ScreenFlow](#gamecycle--screen--screenflow)
   - [Persistence / SaveSystem](#persistence--savesystem)
   - [Pool](#pool)
   - [Window / UI](#window--ui)
3. [TScript](#tscript)
4. [Patterns & Conventions](#patterns--conventions)

---

## Common

### DI (Dependency Injection)

**Purpose:** Provides dependency injection using VContainer framework.

**Key Classes:**

| Class | Role |
|-------|------|
| `RootScopeContainer` | Main DI container that inherits from `LifetimeScope`. Configures global events and registers bootstrap configurators. |
| `IVContainerConfigurator` | Interface for components that need to configure the root VContainer scope. |
| `IBootstrapVContainerConfigurator` | Interface for bootstrap managers to register themselves in the DI container. |
| `SceneScopeContainer` | Container for scene-specific DI scope (if needed). |

**How it Works:**
- `RootScopeContainer` is a `LifetimeScope` that automatically discovers and calls `ConfigureRootScope` on all components implementing `IBootstrapVContainerConfigurator`.
- Bootstrap managers (like `PoolManager`, `WindowManager`, `ScreenManager`) implement this interface to register themselves as services.
- Services are registered using `builder.RegisterComponent(this).AsImplementedInterfaces()`.

**Pattern:** Constructor injection via `[Inject]` attribute on fields or methods.

---

### Editor

**Purpose:** Editor utilities for development workflow.

**Key Classes:**

| Class | Role |
|-------|------|
| `FlagSettingWindow` | Editor window for managing build flags/settings. |
| `PreviewDemoWindow` | Editor window for previewing demos. |

---

### Log

**Purpose:** Unified logging infrastructure with level filtering, threading support, and rich text formatting.

**Key Classes:**

| Class | Role |
|-------|------|
| `LogUtils` | Core logging utility class providing: <br>- Log level filtering (Verbose, Debug, Info, Warning, Error)<br>- Thread-safe logging<br>- Dictionary/List formatting helpers<br>- Rich text formatting (color, bold, italic, size)<br>- Color to HEX conversion with caching |

**Key Features:**
- **Log Levels:** Set minimum log level with `SetMinLogLevel(LogLevel level)`
- **Thread Safety:** Use `LogThreadSafe()` for multi-threaded scenarios
- **Rich Text:** Helpers for bold, italic, color, and size formatting
- **Collections:** `LogDictionaryInline()`, `LogList()`, `LogListInline()` for debugging

**Pattern:** Static utility class used throughout the framework via extension methods on `MonoBehaviour`.

---

### MessagePipe

**Purpose:** Event system built on top of MessagePipe for decoupled communication between systems.

**Key Classes:**

| Class | Role |
|-------|------|
| `MessagePipeHelper` | Registers global MessagePipe events in DI container. |
| `MessagePipeEventAssemblyAttribute` | Assembly-level attribute to whitelist event assemblies. |
| `EventScanner` | Scans assemblies for event types to register. |
| `LookupPersistence` | Persists event lookups for performance. |

**How it Works:**
- Uses MessagePipe for pub/sub messaging
- Events are automatically scanned and registered at startup
- Supports both global (system-wide) and local (per-screen) event registration

---

### Scene

**Purpose:** Scene management utilities including custom scene fields for Unity Inspector.

**Key Classes:**

| Class | Role |
|-------|------|
| `SceneField` | Serializable field for scene reference in Inspector. |
| `SceneSelectorAttribute` | Custom attribute for scene selection in Editor. |
| `EditorSceneUtil` | Editor utilities for scene operations. |

---

### Init

**Purpose:** Initialization system for game startup and configuration.

**Key Classes:**

| Class | Role |
|-------|------|
| `VInitializeProfile` | ScriptableObject containing a list of `IVInitializable` objects to initialize at startup. |
| `IVInitializable` | Interface for objects that need initialization. |
| `VInitializeInvokeSite` | Component that invokes initialization. |

**Pattern:** Implement `IVInitializable` for any component that needs to be initialized via the profile.

---

## Core

### Bootstrap

**Purpose:** Central bootstrapping system that initializes all core managers.

**Key Classes:**

| Class | Role |
|-------|------|
| `BootstrapManagerCentral` | Main bootstrapper that loads and initializes all bootstrap managers from a `BootstrapProfile`. |
| `BootstrapProfile` | ScriptableObject containing references to manager prefabs. |
| `VBootstrapManager<T, TI>` | Base class for bootstrap managers. Always enabled, initializes via `InitializeBootstrap()`. |
| `VManager<T, TI>` | Base class for regular managers with enable/disable functionality. |
| `VSingleton<T, TI>` | Singleton base class for MonoBehaviours with interface support. |
| `ICommonManager` | Common interface for all managers. |

**How it Works:**
1. `BootstrapManagerCentral` loads manager prefabs from `BootstrapProfile`
2. Each prefab must have a component implementing `ICommonManager`
3. Bootstrap managers (inheriting from `VBootstrapManager`) are always enabled
4. They implement `ConfigureRootScope` to register in DI container

**Class Hierarchy:**
```
MonoBehaviour
    └── VSingleton<T, TI>
            └── VManager<T, TI>
                └── VBootstrapManager<T, TI>
```

---

### Camera

**Purpose:** Centralized camera management with profile-based configuration and transitions.

**Key Classes:**

| Class | Role |
|-------|------|
| `CameraManager` | Main camera manager (VBootstrapManager). Manages camera profiles, overrides, and transitions. |
| `ICameraManager` | Interface for camera manager. |
| `CameraProfile` | ScriptableObject containing camera settings (position, rotation, FOV, etc.). |
| `VirtualCamera` | Represents a virtual camera configuration that can be blended to. |

**Key Features:**
- Profile-based camera configuration
- Override stack for temporary camera changes
- Smooth transitions using PrimeTween
- Event-driven profile application (`OnProfileApplied`, `OnOverridePushed`, `OnOverridePopped`)

---

### GameCycle / Screen / ScreenFlow

**Purpose:** Manages game screens/scenes, navigation, transitions, and flow control.

#### Screen Management

**Key Classes:**

| Class | Role |
|-------|------|
| `ScreenManager` | Main screen manager handling screen navigation, transitions, and loading. |
| `IScreenManager` | Interface for screen manager. |
| `ScreenModel` | Represents a screen with metadata, events, and loading info. |
| `ScreenModelContainer` | Container for all screen models. |
| `ScreenMetaData` | Metadata for a screen (name, scene reference, etc.). |
| `IScreenDefinition` | Interface for screens to define their properties. |

**Screen States:**
- **Current:** Active screen
- **Stack:** Navigation stack for push/pop navigation
- **Override:** Interrupting screen (modal, etc.)

**Events (MessagePipe):**
- `PreScreenEnterEvent` / `PostScreenEnterEvent`
- `PreScreenExitEvent` / `PostScreenExitEvent`
- `ScreenTransitionCompletedEvent`

#### Loading

**Key Classes:**

| Class | Role |
|-------|------|
| `ILoadingController` | Interface for loading screen controllers. |
| `LoadingContext` | Context data for loading operations. |
| `LoadingHandler` | Handles loading UI and progress reporting. |
| `DefaultSliderLoadingController` | Default implementation with slider progress. |

#### ScreenFlow

**Key Classes:**

| Class | Role |
|-------|------|
| `ScreenFlowManager` | Manages game flow using a graph-based state machine. |
| `IScreenFlowManager` | Interface for screen flow manager. |
| `ScreenFlowGraph` | Graph representation of screen flow states and transitions. |
| `ScreenFlowActor` | Executes flow transitions based on events. |
| `ScreenFlowStateContainer` | Maintains flow state history. |
| `ScreenFlowGraphResolver` | Resolves flow graph nodes and transitions. |
| `ScreenFlowProfile` | ScriptableObject containing the flow graph. |

**Flow Features:**
- Event-driven state transitions
- Condition-based transitions (And, Or, Not conditions)
- History tracking for back navigation
- Start screen resolution from graph

---

### Persistence / SaveSystem

**Purpose:** Comprehensive save/load system with encryption, migration, and multiple backends.

**Key Classes:**

| Class | Role |
|-------|------|
| `SaveService` | Core save/load service. |
| `ISaveService` | Interface for save service. |
| `SaveServiceManager` | Manages multiple save services. |
| `ISaveManager` | Interface for save manager. |
| `PlayerProfile` | Player data container. |
| `PersistencePackage` | Package for persistent data. |
| `IDataPersistenceManager` | Interface for data persistence management. |
| `DataPersistenceManager` | Manages data persistence operations. |

#### Save Backends

| Class | Role |
|-------|------|
| `ISaveBackend` | Interface for save storage backends. |
| `JsonFileSaveBackend` | Saves to JSON files. |
| `PlayerPrefsSaveBackend` | Saves to Unity PlayerPrefs. |

#### Encryption

| Class | Role |
|-------|------|
| `IEncryptor` | Interface for encryption. |
| `AesEncryptor` | AES encryption implementation. |
| `NoOpEncryptor` | No encryption (passthrough). |
| `XorEncryptor` | XOR encryption. |
| `CompositeEncryptor` | Combines multiple encryptors. |

#### Migration

| Class | Role |
|-------|------|
| `ISaveMigrator` | Interface for save migration. |
| `SaveMigrationChain` | Chain of save migrators for version upgrades. |
| `DefaultSaveMigrator` | Default migration implementation. |

#### Serialization

| Class | Role |
|-------|------|
| `ISerializer` | Interface for serialization. |
| `JsonSerializer` | JSON serialization. |
| `NewtonsoftJsonSerializer` | Newtonsoft-based JSON serialization. |

**Key Features:**
- Multiple storage backends (File, PlayerPrefs)
- Encryption support (AES, XOR, ROT13, etc.)
- Save migration between versions
- Reactive state updates
- MessagePipe event publishing

---

### Pool

**Purpose:** High-performance object pooling system for GameObjects and C# classes.

**Key Classes:**

| Class | Role |
|-------|------|
| `PoolManager` | Main pool manager (VBootstrapManager). Handles all pooling operations. |
| `IPoolManager` | Interface for pool manager. |
| `PoolConfig` | Configuration for pool behavior (preload count, max size, overflow behavior). |
| `IPoolable` | Interface for poolable objects (receives OnSpawn/OnDespawn callbacks). |
| `PoolAnalytics` | Tracks pool usage statistics. |
| `PoolStats` | Statistics for a single pool. |

**Key Features:**
- **Prefab Pooling:** GameObject pooling with configurable limits
- **Class Pooling:** C# class pooling with injection support
- **Auto-tracking:** Automatic spawn/despawn tracking
- **Smart Despawn:** Scheduled despawn with delay
- **Pool Limits:** Max size with overflow handling (Expand, ReturnNull, RecycleOldest)
- **Warmup & Preloading:** Pre-spawn objects on startup
- **Analytics:** Hit rate, memory estimation, category grouping
- **Auto-cleanup:** Cleanup unused pools after timeout

**Overflow Behaviors:**
| Behavior | Description |
|----------|-------------|
| `Expand` | Create new instances when pool is full (default) |
| `ReturnNull` | Return null when pool is full |
| `RecycleOldest` | Recycle oldest active object when pool is full |

---

### Window / UI

**Purpose:** Window/UI management system with transitions, input blocking, and Addressables support.

**Key Classes:**

| Class | Role |
|-------|------|
| `WindowManager` | Main window manager (VBootstrapManager). Handles window stack, transitions, and input blocking. |
| `IWindowManager` | Interface for window manager. |
| `UIViewBase` | Base class for all windows/views. |
| `FullScreenBase` | Base class for full-screen windows. |
| `PopupBase` | Base class for popup windows. |
| `IUIView` | Interface for UI views. |
| `WindowOptions` | Configuration for window opening (data, transitions, sorting). |
| `WindowType` | Enum for window types (FullScreen, Popup, FullScreenPopup, System, Tutorial). |
| `UILayer` | UI layer configuration for sorting. |
| `WindowProfile` | ScriptableObject containing window settings. |

**Key Features:**
- **Window Stack:** Maintains stack of open windows with proper ordering
- **Addressables:** Loads window prefabs via Addressables
- **Pool Integration:** Uses PoolManager for window instantiation
- **Transitions:** Built-in transition system with in/out animations
- **Input Blocking:** Blocks input during transitions
- **Back Button:** Supports system back button (Android/iOS)
- **Sorting Order:** Automatic sorting based on window type and stack position

**Window Types:**
| Type | Sorting Base |
|------|---------------|
| `FullScreen` | popupBaseSortingOrder |
| `Popup` | popupBaseSortingOrder |
| `FullScreenPopup` | popupBaseSortingOrder |
| `System` | systemBaseSortingOrder |
| `Tutorial` | systemBaseSortingOrder + 1000 |

**Transition System:**
- Uses `IUITransitionFactory` to create transitions
- Uses `UITransitionRunner` to execute transitions
- Supports custom transition presets

---

## TScript

**Purpose:** Test scripts and demonstration code for the framework.

The TScript folder contains example scripts for testing various framework features:
- `TestBoostraper.cs` / `TestBoostraperB.cs` - Bootstrap testing
- `TestEvent.cs` - Event system testing
- `TestInjector.cs` - DI injection testing
- `TestPopup.cs` - Window/Popup testing
- `TestSaveMigrator.cs` - Save migration testing
- `TestScript.cs` - General testing
- `DataPackageDemo.cs` - Data package demonstration

---

## Patterns & Conventions

### Dependency Injection Pattern
- Use `VContainer` for DI
- Register services via `IBootstrapVContainerConfigurator.ConfigureRootScope()`
- Inject via `[Inject]` attribute on fields or constructor

### Manager Pattern
- All core systems are managers inheriting from `VManager` or `VBootstrapManager`
- Bootstrap managers are always enabled; regular managers can be toggled
- Singleton pattern via `VSingleton`

### Event Pattern
- Uses MessagePipe for decoupled event communication
- Global events via `GlobalScreenEventHub`
- Local events per screen via `LocalScreenEventContainer`

### Pooling Pattern
- All spawnable prefabs should use `PoolManager`
- Implement `IPoolable` for lifecycle callbacks
- Configure via `PoolConfig` ScriptableObjects

### Screen/Scene Pattern
- Screens are managed by `ScreenManager`
- Use `ScreenModel` to define screen metadata
- Use `ScreenFlowManager` for graph-based flow control

### Window/UI Pattern
- Windows inherit from `UIViewBase`
- Use `WindowManager` for opening/closing
- Configure via `WindowOptions` and `WindowProfile`

### Profile Pattern
- Many systems use ScriptableObject profiles for configuration
- Examples: `BootstrapProfile`, `CameraProfile`, `ScreenFlowProfile`, `WindowProfile`
- Profiles are typically loaded from Resources via `*ProfileUtilities.TryGetProfile()`

---

## Quick Start

1. **Setup Bootstrap:** Create a `BootstrapProfile` and add manager prefabs
2. **Add Root Scope:** Add `RootScopeContainer` to your scene
3. **Create Managers:** Implement managers inheriting from `VBootstrapManager`
4. **Use Services:** Inject and use services via DI

```csharp
// Example: Injecting a service
public class MyComponent : MonoBehaviour
{
    [Inject] private IPoolManager _pool;
    [Inject] private IWindowManager _windowManager;
    
    void Start()
    {
        var obj = _pool.Spawn(myPrefab);
    }
}
```

---

*Generated for VuTH Unity Framework*
