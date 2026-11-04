# VuTH Common Framework - Documentation

The **Common** layer (`Assets/_VuTH/Common`) is the foundational framework for the VuTH Unity project. It provides core infrastructure for dependency injection, logging, message piping, scene management, initialization, and base class patterns.

---

## Table of Contents

1. [Base Classes](#1-base-classes)
2. [Dependency Injection (DI) - VContainer](#2-dependency-injection---vcontainer)
3. [Log System](#3-log-system)
4. [MessagePipe System](#4-messagepipe-system)
5. [Scene Utilities](#5-scene-utilities)
6. [Init System](#6-init-system)
7. [Editor Utilities](#7-editor-utilities)

---

## 1. Base Classes

### Purpose

Provides foundational MonoBehaviour patterns for singleton managers, bootstrap managers, and a common interface for all manager systems in the framework.

### Key Classes

#### `ICommonManager` (Interface)
- **Purpose**: Common contract for all manager systems
- **Key Members**:
  - `bool IsInitialized` - True between initialization and deinitialization (or destruction)

#### `IToggleableManager` (Interface)
- **Purpose**: Managers that can be switched on and off at runtime (implemented by `VManager`, not by bootstrap managers)
- **Key Members**:
  - `bool IsEnabledSystem`
  - `void EnableSystem(bool enable)` - Enabling initializes (if needed), disabling deinitializes; idempotent both ways
  - `void ToggleSystem()`

#### `VSingleton<T, TI>` (Generic MonoBehaviour Singleton)
- **Purpose**: Generic singleton base class with interface support and optional VContainer integration
- **Key Features**:
  - `static TI Instance` / `static bool HasInstance` - Service-locator access; prefer injecting `TI`, use these only where DI is not available
  - `protected virtual void Awake()` - Handles singleton instantiation, `DontDestroyOnLoad`
  - Duplicate instances are destroyed: the whole GameObject when it only holds the singleton, otherwise only the component
- **VContainer Integration**: Implements `IBootstrapVContainerConfigurator` when `VCONTAINER` is defined
  - `ConfigureRootScope(builder)` calls `RegisterSelf(builder)`: `RegisterComponent` as `T`, `TI` and the interfaces `TI` extends (except `ICommonManager` / `IToggleableManager`). `RegisterComponent` injects the instance, so `[Inject]` methods run (the former `RegisterInstance` default never injected)
  - Override `ConfigureRootScope` to register more, calling base
- **Pattern**: `where T : MonoBehaviour, TI`

#### `VManagerCore<T, TI>` (Manager Lifecycle Base)
- **Purpose**: Lifecycle shared by every manager
- **Key Members**:
  - `abstract bool InitializeOnAwake` - Whether the manager initializes by itself in `Awake`
  - `abstract void InitializeManager()` / `abstract void DeinitializeManager()` - Run at most once each, in pairs, only on the primary instance
  - `protected void EnsureInitialized()` / `EnsureDeinitialized()` - Idempotent entry points
  - `virtual bool IsInitialized`
- **Lifecycle**:
  - Initializes in `Awake`, or right after injection when created during root DI configuration (`VManagerInitializationGate`)
  - Deinitializes in `OnDestroy` (scene teardown, leaving Play Mode, quitting); exceptions there are logged, not rethrown

#### `VManager<T, TI>` (Toggleable Manager)
- **Purpose**: `VManagerCore` + `IToggleableManager`
- **Key Members**:
  - `bool enableSystem` - Serialized: whether the system is on (enabled systems initialize in Awake)
  - `bool customLifecycleManagement` - Serialized: no initialization in Awake; call `EnableSystem(true)` yourself
- **Pattern**: `where T : VManager<T, TI>, TI` and `where TI : ICommonManager`

#### `VBootstrapManager<T, TI>` (Bootstrap Manager)
- **Purpose**: Always-on manager created at startup by the bootstrap, ordered by `BootstrapOrder`
- **Key Features**:
  - Always initializes in `Awake` (after injection when created by the bootstrap) and deinitializes when destroyed
  - `InitializeBootstrap()` / `DeinitializeBootstrap()` abstract methods
  - Initialize everything in `InitializeBootstrap()`: injected dependencies are available there. `[Inject]` methods only store dependencies (and apply them when the manager is already initialized, e.g. placed in a scene)
  - Depend on other managers through their interfaces (injected), not through `X.Instance` (fallback only without DI)
- **Pattern**: `where T : VBootstrapManager<T, TI>, TI` and `where TI : ICommonManager`

### How They Interact

```
VSingleton<T, TI>                 (singleton + default DI registration)
    └── VManagerCore<T, TI>       (paired init / deinit, init gate)
            ├── VManager<T, TI>           (IToggleableManager: enable / disable)
            └── VBootstrapManager<T, TI>  (always on, BootstrapOrder)
```

### Patterns Used

- **Singleton Pattern**: Thread-safe singleton with `DontDestroyOnLoad`
- **Template Method Pattern**: Virtual `Awake()` in base, abstract methods for subclasses
- **Generic Type Constraints**: Strongly-typed singletons with interface exposure

---

## 2. Dependency Injection - VContainer

### Purpose

Integrates [VContainer](https://github.com/hadashiA/VContainer) for dependency injection across the framework, providing both root-level (application-wide) and scene-level containers.

### Key Classes

#### `IBootstrapVContainerConfigurator` (Interface)
- **Purpose**: Interface for MonoBehaviours that want to register themselves in the root scope
- **Key Members**:
  - `void ConfigureRootScope(IContainerBuilder builder)` - Register services in root container
- **Usage**: Implement on `VSingleton<T, TI>` subclasses to auto-register with DI

#### `IVContainerConfigurator` (Interface)
- **Purpose**: General-purpose interface for configuring scene-scoped containers
- **Key Members**:
  - `void Configure(IContainerBuilder builder)` - Add services to scene container

#### `RootScopeContainer` (LifetimeScope)
- **Purpose**: Application-wide DI container (placed on a bootstrap scene)
- **Key Features**:
  - Registers global MessagePipe events via `MessagePipeHelper.RegisterGlobalEvents(builder)`
  - Finds all `IBootstrapVContainerConfigurator` implementations via `FindObjectsByType<MonoBehaviour>` and calls their `ConfigureRootScope`
- **Usage**: Place in the initial/boot scene

#### `SceneScopeContainer` (LifetimeScope)
- **Purpose**: Scene-specific DI container (placed on scene root GameObject)
- **Key Features**:
  - Has `[SerializeReference] MonoBehaviour[] configurators` for scene-specific registrations
  - Logs warning if no Parent is assigned (links to parent scope)
  - Registers scene-scoped MessagePipe events via `MessagePipeHelper.RegisterSceneEvents(builder)`
  - Applies all `IVContainerConfigurator` implementations in the `configurators` array

### How They Interact

```
RootScopeContainer (Global/Application-level)
    │
    ├── Finds IBootstrapVContainerConfigurator → ConfigureRootScope()
    │
    └── Registers Global-scoped MessagePipe events

SceneScopeContainer (Scene-level, child of RootScope)
    │
    ├── Logs parent linkage warning if missing
    │
    ├── Has configurators[] field → each is IVContainerConfigurator → Configure()
    │
    └── Registers Scene-scoped MessagePipe events
```

### Patterns Used

- **Hierarchical Scopes**: Parent-child relationship between LifetimeScopes
- **Configuration Pattern**: Separates registration logic via configurator interfaces
- **Convention over Configuration**: Auto-discovery of `IBootstrapVContainerConfigurator` via reflection

---

## 3. Log System

### Purpose

Provides enhanced logging utilities with log levels, threading safety, rich text/color support, and dictionary/collection formatting.

### Key Classes

#### `LogUtils` (Static Utility Class)
- **Purpose**: Core logging utilities with level filtering, thread safety, and rich text

**Log Level Filtering:**
- `enum LogLevel { Verbose, Debug, Info, Warning, Error }`
- `SetMinLogLevel(LogLevel level)` - Set minimum level
- `ShouldLog(LogLevel level)` - Check if should log

**Thread Safety:**
- `LogThreadSafe(string message, Color? color = null)` - Thread-safe logging using `lock (LogLock)`

**Dictionary & Collection Logging:**
- `LogDictionaryInline<TKey, TValue>(Dictionary, color)` - Log as "key1:val1, key2:val2"
- `LogList<T>(IEnumerable<T>, color)` - Log each item on a separate line with index
- `LogListInline<T>(IEnumerable<T>, color)` - Log all items on a single line "[item0, item1, ...]"

**Core Logging Methods:**
- `Log(string message, Color? color = null)` - Standard log with optional color
- `Log(string prefix, string message, Color? color = null)` - Log with prefix
- `LogWarning(string message, Color? color = null)` - Warning log
- `LogError(string message, Color? color = null)` - Error log (always logs)
- `LogTag(string tag, string message, Color? color = null)` - Tagged log for filtering

**Multi-line Processing:**
- Automatically wraps `<color>...</color>` tags across multiple lines

**Hex Color Caching:**
- `ToHex(Color? color = null)` - Convert Unity Color to hex with caching via `ConcurrentDictionary`

**Rich Text Helpers (Nested `RichText` Class):**
- `Bold(string text)` - `<b>text</b>`
- `Italic(string text)` - `<i>text</i>`
- `Size(string text, int size)` - `<size=N>text</size>`
- `Color(string text, Color color)` - `<color=#RRGGBB>text</color>`

#### `DevLogExtensions` (Extension Methods)
- **Purpose**: Provides `Log()`, `LogWarning()`, `LogError()` extension methods on any object with automatic type-based prefixing

**Key Features:**
- Prefix format: `[TypeName] message` with color-coded prefix
- `[Conditional("UNITY_EDITOR")]` - Only compiles in Editor
- `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]` - Verbose logs
- Automatic truncation at 15000 characters (Unity Console safe limit)
- Prefix caching via `ConcurrentDictionary` for performance

**Default Colors:**
- Log: Light Blue `(0.6f, 0.9f, 1f)`
- Warning: Yellow `(1f, 0.85f, 0.2f)`
- Error: Red `(1f, 0.35f, 0.35f)`

### How They Interact

```
DevLogExtensions (extension methods)
    │
    ├── WithOrigin() → resolves Type from object
    │
    ├── GetPrefix() → caches [TypeName] prefix
    │
    └── Calls LogUtils methods:
            ├── Log()
            ├── LogWarning()
            └── LogError()
                │
                └── Uses ZString for zero-allocation string building
```

### Patterns Used

- **Zero-Allocation Logging**: Uses `Cysharp.Text.ZString` for string building
- **Caching**: Color-to-hex conversion cached in `ConcurrentDictionary`
- **Conditional Compilation**: `[Conditional]` attributes for editor-only code
- **Thread Safety**: `lock` and `ConcurrentDictionary` for multi-threaded scenarios

---

## 4. MessagePipe System

### Purpose
Pub/sub messaging through Cysharp MessagePipe, wired into the VuTH DI scopes. Nothing to bake or generate:
events are discovered at runtime.

### How it works
- **Root scope** (`RootScopeContainer`): `RegisterMessagePipe(options)` with `MessagePipeOptionsConfig` applied.
  With VContainer ≥ 1.14 MessagePipe registers *open-generic* brokers, so **any type** can be published/subscribed
  globally — no attribute or registration needed.
- **Scene scope** (`SceneScopeContainer`): every type marked `[MessagePipeEvent(EventScope.Scene)]` gets a broker
  registered in that scope, so publishers and subscribers in one scene never see messages from another scene.
  `[MessagePipeEvent(EventScope.Scene, "Arena")]` limits this to scopes living in the scene named `Arena`
  (the scope's **own** scene — screens are loaded additively, the active scene is not used).
- **Screen scope**: `[MessagePipeEvent(EventScope.Screen, ScreenIds.Names.Arena)]` binds the event to a *screen*
  (ScreenIdentifier name) instead of a scene name: every scene scope belonging to that screen — main and additive
  scenes — gets its own broker. Membership comes from the screen ScreenManager is loading when the scope is built,
  and from `ScreenMetaData` on the scene's root objects (`ScreenSceneMembership`). Use the generated
  `ScreenIds.Names.*` constants so a renamed screen is a compile error.
- **Misuse guard**: resolving the publisher/subscriber of a Scene/Screen event from a scope that has no broker for it
  would silently use the shared global broker. The root registers a guard that reports it — `Warn` (default, once per
  event type), `Error` (throws) or `Ignore` — set in `MessagePipeOptionsConfig.scopedEventMisuse`.
- **Validation**: the settings tab checks every scene name / screen id used in `[MessagePipeEvent]` against the scene
  assets and ScreenIdentifier assets of the project (`MessagePipeTargetValidator`).
- **Without VContainer**: `MessagePipeHelper.GetPublisher<T>() / GetSubscriber<T>()` use one `GlobalMessagePipe`
  provider with a broker for every `[MessagePipeEvent]` type (no scene isolation).

### Key Classes
- **`[MessagePipeEvent(scope, sceneName)]`** — marks a message type. Global marking is optional with VContainer
  (it only lists the type in the settings tab and enables the non-VContainer fallback).
- **`MessagePipeEventCatalog`** — runtime discovery (cached, scans only assemblies referencing
  `VuTH.Common.MessagePipe`). `All`, `ForScene(sceneName)`, `Invalidate()`.
- **`MessagePipeHelper`** — `RegisterGlobalEvents(builder)`, `RegisterSceneEvents(builder, scene)`,
  `ApplyConfiguredOptions(options)`; non-VContainer `GetPublisher/GetSubscriber`.
- **`MessagePipeScopeInstaller`** — plugs the two registrations into `ScopeInstallers` (Common.DI does not depend
  on MessagePipe).
- **`MessagePipeOptionsConfig`** (Resources/MessagePipe/MessagePipeOptionsConfig) — `enableCaptureStackTrace`.
- **Settings tab "MessagePipe"** — options + read-only list of discovered events grouped by scope / scene.

### Usage
```csharp
[MessagePipeEvent]                              // global (marking optional)
public sealed class CoinsChanged { public int Value; }

[MessagePipeEvent(EventScope.Scene)]            // one isolated broker per scene scope
public sealed class EnemyKilled { }

public sealed class Hud
{
    [Inject] public void Construct(ISubscriber<CoinsChanged> coins) => coins.Subscribe(c => Show(c.Value));
}
```

### Migrating from the bake pipeline
The whitelist, `EventScopeLookup` asset, bake/validate menu and generated `MessagePipeRegistrar.cs` were removed.
Delete any leftover `MessagePipeRegistrar.cs` in your project. `[assembly: VuTHMessagePipeEventAssembly]` is obsolete
(no longer needed) and can be removed. `registerAsyncBroker` has no effect: async brokers are always registered.


## 5. Scene Utilities

### Purpose

Provides scene reference handling with editor UI support for selecting scenes from Build Settings.

### Key Classes

#### `SceneSelectorAttribute` (PropertyAttribute)
- **Purpose**: Marks a string field to use custom scene selector drawer
- Usage: `[SceneSelector] public string sceneName;`

#### `SceneField` (Serializable Struct)
- **Purpose**: Serializable scene reference wrapper
- Contains: `[SceneSelector] public string sceneName;`
- Implicit conversion to/from `string`
- Example: `public SceneField myScene;`

#### `EditorSceneUtil` (Editor-Only Static Class)
- **Purpose**: Editor scene management utilities
- Methods:
  - `IsSceneOpen(AssetReference sceneRef)` - Check if scene is loaded
  - `OpenSceneInEditor(AssetReference sceneRef, OpenSceneMode mode)` - Open scene with save prompt

#### `SceneSelectorDrawer` (Custom PropertyDrawer)
- **Purpose**: Draws dropdown populated from `EditorBuildSettings.scenes`
- Features:
  - Caches scene list, updates on `sceneListChanged` event
  - Shows "- None -" option for empty selection
  - Warns if selected scene is missing from Build Settings

### How They Interact

```
SceneField (runtime data)
    │
    ├── [SceneSelector] attribute
    │
    └── SceneSelectorDrawer (editor rendering)
            │
            └── Reads EditorBuildSettings.scenes

EditorSceneUtil (utility)
    │
    └── Used by other systems to load/open scenes
```

### Patterns Used

- **PropertyDrawer Pattern**: Custom UI for serialized fields
- **Caching**: Scene list cached until Build Settings change
- **Implicit Conversion**: Easy interoperability with string type

---

## 6. Init System

### Purpose

Provides a standardized initialization system for managing async initialization across multiple components using profiles.

### Key Classes

#### `IVInitializable` (Interface)
- **Purpose**: Contract for initializable components
- Key Members:
  - `UniTask VInitialize()` - Async initialization method (using Cysharp.Threading.Tasks)

#### `VInitializeProfile` (ScriptableObject)
- **Purpose**: Container for a list of initializable objects
- Fields:
  - `bool isEnabled` - Toggle initialization
  - `IVInitializable[] initializables` - Ordered list
- Methods:
  - `IsEnabled` property
  - `Initializables` - Returns `ReadOnlyCollection<IVInitializable>`

#### `VInitializeInvokeSite` (MonoBehaviour)
- **Purpose**: Executes initialization from a profile
- Fields:
  - `VInitializeProfile initializeProfile` - Profile to execute
- Events:
  - `OnInitializableInitializedEvent` - Fired after each initialization
- Methods:
  - `async UniTask InvokeInitialize()` - Iterates profile's initializables, awaits each
  - `AssignInitializables(IVInitializable[])` - Programmatically assign initializables via reflection
- Extensions:
  - `SceneVInitializeInvokeSiteExtensions.TryGetVInitializeInvokeSite(Scene, out VInitializeInvokeSite)` - Find invoke site in scene

### How They Interact

```
VInitializeProfile (ScriptableObject)
    │
    └── Contains IVInitializable[] (ordered)

VInitializeInvokeSite (MonoBehaviour)
    │
    ├── Has reference to VInitializeProfile
    │
    └── InvokeInitialize() → awaits each VInitialize()
            │
            └── Fires OnInitializableInitializedEvent

IVInitializable (interface)
    │
    └── Implemented by components needing init
```

### Patterns Used

- **Profile Pattern**: ScriptableObject-based configuration
- **Async/Await**: Using UniTask for non-blocking initialization
- **Observer Pattern**: Event for post-init notifications

---

## 7. Editor Utilities

### Purpose

Provides various editor windows, utilities, and tools for VuTH development.

### Key Classes

#### Settings System

**`ISettingsTab`** (Interface)
- **Purpose**: Contract for settings tabs
- Properties: `Id`, `Title`, `Order`
- Methods: `CreateView() → VisualElement`

**`[SettingsTab]`** (Attribute)
- **Purpose**: Auto-registers classes implementing `ISettingsTab`

**`SettingsRegistry`** (Static Class)
- **Purpose**: Auto-discovers and caches settings tabs using `TypeCache.GetTypesWithAttribute<SettingsTabAttribute>()`
- Rebuilds on domain reload

**`SettingsWindow`** (EditorWindow)
- **Purpose**: Main settings UI window (Menu: VuTH/Settings)
- Two-column layout: sidebar (ListView) + content area

#### Flag Settings

**`FlagSettingWindow`** (SettingsTab)
- **Purpose**: Feature flag management (Menu: VuTH/Settings → Feature Flag)
- Features:
  - Toggle `VCONTAINER` flag
  - Auto-updates PlayerSettings scripting define symbols
  - Persists to `Assets/_VuTH/Common/Editor/FlagData/feature_flags.json`

#### Preview System

**`PreviewDemoWindow`** (EditorWindow)
- **Purpose**: Demo window for testing Unity asset previews (Menu: Window/VuTH/Preview Demo)
- Supports: Sprite, Texture2D, RenderTexture, Material, GameObject, UI GameObject

#### Pre-Build Tools

**`PreBuildProfile`** (ScriptableObject)
- **Purpose**: Configurable pre-build tasks

**`PreBuildTools`**
- **Purpose**: Menu item to run all pre-build profiles (Menu: VuTH/PreBuild/Run All PreBuild Tasks)

#### Scene Postprocessor

**`ISceneImportTask`** (Interface)
- **Purpose**: Contract for scene import tasks

**`ScenePostprocessor`** (MonoBehaviour + `IPreprocessBuildWithReport`)
- **Purpose**: Runs import tasks during build

#### UI Custom Drawers

**`SceneSelectorDrawer`** - See Scene Utilities

**`PreviewObjectDrawer`**
- **Purpose**: Draws asset previews in editor

**`ReadOnlyFieldDrawer`**
- **Purpose**: PropertyDrawer for `[ReadOnly]` attribute

**`DictionaryDrawerAttribute`**
- **Purpose**: Custom drawer for dictionary fields

**ImGui Drag & Drop (UI/DraggableGrid)**
- **Purpose**: Custom ImGui-style drag and drop controls for inventory grids

### How They Interact

```
SettingsWindow (main UI)
    │
    └── SettingsRegistry (tab discovery)
            │
            └── Finds [SettingsTab] attributed classes
                    │
                    ├── FlagSettingWindow (feature flags)
                    ├── PreviewDemoWindow (asset preview)
                    └── (other custom tabs)

FlagSettingWindow
    │
    └── Updates PlayerSettings.SetScriptingDefineSymbols()

PreBuildTools
    │
    └── Executes PreBuildProfile.ExecuteAllTasks()
```

### Patterns Used

- **Settings Tab Pattern**: Self-registering settings via attribute
- **Menu Items**: `[MenuItem]` for quick access
- **Type Caching**: `TypeCache.GetTypesWithAttribute<T>()` for fast discovery
- **ScriptableObject Configuration**: Profile-based task execution

---

## Summary

The VuTH Common framework provides:

| Component | Purpose |
|-----------|---------|
| **Base Classes** | Singleton, Manager, BootstrapManager patterns |
| **DI (VContainer)** | Hierarchical dependency injection |
| **Log System** | Rich-text, thread-safe, level-filtered logging |
| **MessagePipe** | Pub/sub events with Global/Scene scopes |
| **Scene Utilities** | Scene reference handling in editor |
| **Init System** | Async initialization via profiles |
| **Editor Utilities** | Settings, flags, previews, build tools |

This architecture promotes **separation of concerns**, **testability**, and **maintainability** across the VuTH Unity project.
