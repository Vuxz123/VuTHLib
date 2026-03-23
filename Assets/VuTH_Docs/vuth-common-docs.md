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
  - `bool IsEnabledSystem` - Whether the manager is active
  - `void EnableSystem(bool enable)` - Enable/disable the system
  - `void ToggleSystem()` - Toggle system state

#### `VSingleton<T, TI>` (Generic MonoBehaviour Singleton)
- **Purpose**: Generic singleton base class with interface support and optional VContainer integration
- **Key Features**:
  - `static TI Instance` - Access the singleton instance
  - `static bool HasInstance` - Check if instance exists
  - `protected virtual void Awake()` - Handles singleton instantiation, `DontDestroyOnLoad`
  - Duplicate instances are automatically destroyed
- **VContainer Integration**: Implements `IBootstrapVContainerConfigurator` when `VCONTAINER` is defined
  - Registers itself in root scope via `ConfigureRootScope(IContainerBuilder builder)`
- **Pattern**: `where T : MonoBehaviour, TI` and `where TI : class`

#### `VManager<T, TI>` (Manager System Base)
- **Purpose**: Extends VSingleton with enable/disable system lifecycle
- **Key Members**:
  - `bool enableSystem` - Serialized field to toggle system
  - `bool customLifecycleManagement` - Opt-out of auto Awake initialization
  - `virtual bool IsEnabledSystem` - Property getter
  - `virtual void EnableSystem(bool enable)` - Enable/disable with initialization callbacks
  - `void ToggleSystem()` - Toggle between states
  - `abstract void InitializeManager()` - Called when system is enabled
  - `abstract void DeinitializeManager()` - Called when system is disabled
- **Pattern**: `where T : VManager<T, TI>, TI, new()` and `where TI : ICommonManager`

#### `VBootstrapManager<T, TI>` (Bootstrap Manager)
- **Purpose**: Specialized manager that is always enabled (bootstrap/initialization phase)
- **Key Features**:
  - `IsEnabledSystem` always returns `true` (setter does nothing)
  - `EnableSystem(bool enable)` is overridden to do nothing
  - Ideal for initialization systems that must run at startup
- **Pattern**: Same generic constraints as `VManager`

### How They Interact

```
ICommonManager (interface)
    ↑
    ├── VSingleton<T, TI> (basic singleton)
    │       ↑
    │       ├── VManager<T, TI> (enable/disable system)
    │       │       ↑
    │       │       └── VBootstrapManager<T, TI> (always-enabled manager)
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

A pub/sub event system built on [MessagePipe](https://github.com/Cysharp/MessagePipe) with scope management (Global/Scene), automatic registration via source generators, and VContainer integration.

### Key Classes

#### Attributes

**`[MessagePipeEvent]`** (`EventScope scope, string sceneName, bool registerAsyncBroker`)
- Marks a class/struct as a MessagePipe event
- `Scope`: `EventScope.Global` (anywhere) or `EventScope.Scene` (specific scene)
- `SceneName`: Required for Scene-scoped events
- `RegisterAsyncBroker`: Whether to also register async version

**`[VuTHMessagePipeEventAssembly]`** (Assembly-level Attribute)
- Marks an assembly to be scanned for events (alternative to whitelist)

#### Configuration

**`MessagePipeAssemblyWhitelist`** (ScriptableObject)
- Configurable list of assembly names to scan
- Default: `["VuTH.Gameplay", "VuTH.Core", "VuTH.Common"]`
- Uses HashSet cache for O(1) lookup

**`MessagePipeOptionsConfig`** (ScriptableObject)
- Runtime configuration for MessagePipe behavior
- `enableCaptureStackTrace`: Debug stack traces
- `preserveRegistrar`: Prevent code stripping

**`MessagePipeConstants`**
- Asset path constants for Resources folder

#### Core

**`EventScopeEntry`** (Serializable)
- Stores: `typeFullName`, `scope`, `sceneName`, `registerAsyncBroker`

**`EventScopeLookup`** (ScriptableObject)
- Baked lookup asset with event metadata
- Version, checksum, bakedAt timestamp
- Runtime cache for fast type lookup
- Methods: `GetScope(Type)`, `GetEntriesByScope(EventScope)`, `GetEntriesByScopeAndScene(EventScope, string)`

#### Editor

**`EventScanner`**
- Scans whitelisted assemblies for `[MessagePipeEvent]` attributes
- Uses `AppDomain.CurrentDomain.GetAssemblies()`
- Validates Scene-scoped events have non-empty `sceneName`

**`MessagePipeEventBaker`** (Menu: VuTH/MessagePipe/Bake Event Scope Lookup)
- Bakes events to `EventScopeLookup` asset
- Generates optimized `MessagePipeRegistrar.cs`
- Validate Bake (checks if stale)
- Clear Baked

**`RegistrarGenerator`**
- Generates `Core.Generated.MessagePipeRegistrar.cs`
- Provides zero-reflection runtime registration

#### Integration

**`MessagePipeHelper`**
- Runtime helper for event registration
- `GetConfiguredOptions()` - Load config asset
- `RegisterGlobalEvents(IContainerBuilder)` - For RootScopeContainer
- `RegisterSceneEvents(IContainerBuilder)` - For SceneScopeContainer
- Falls back to reflection-based lookup if generated registrar not available
- Non-VContainer support via `InitializeGlobalBroker()`

### How They Interact

```
[MessagePipeEvent] (on event class)
    │
    ▼
EventScanner.ScanAssembliesForEvents() (Editor-time)
    │
    ▼
MessagePipeEventBaker.Bake() → EventScopeLookup.asset
    │
    ▼
RegistrarGenerator → MessagePipeRegistrar.cs (optimized)

Runtime:
MessagePipeHelper
    │
    ├── RootScopeContainer → RegisterGlobalEvents()
    │       └── Uses MessagePipeRegistrar.RegisterGlobal() or fallback
    │
    └── SceneScopeContainer → RegisterSceneEvents()
            └── Uses MessagePipeRegistrar.RegisterScene() or fallback
```

### Patterns Used

- **Baked Lookup**: Serialize event metadata to ScriptableObject
- **Source Generation Alternative**: Generated code for zero-reflection performance
- **Scope Hierarchy**: Global vs Scene-scoped events
- **VContainer Integration**: Registers brokers directly in DI container

---

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

**ImGui Drag & Drop (UI/DragableGrid)**
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
