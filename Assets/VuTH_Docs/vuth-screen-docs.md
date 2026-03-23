# VuTH Unity Framework - Screen & ScreenFlow Documentation

The VuTH Screen system provides graph-based screen navigation with support for additive scenes, loading tasks, and smooth transitions.

---

## Table of Contents

1. [Screen System Overview](#screen-system-overview)
2. [ScreenManager](#screenmanager)
3. [ScreenModel](#screenmodel)
4. [Screen Navigation](#screen-navigation)
5. [Screen Events](#screen-events)
6. [Screen Loading](#screen-loading)
7. [Screen Transitions](#screen-transitions)
8. [ScreenFlow System](#screenflow-system)

---

## Screen System Overview

```
ScreenManager (VBootstrapManager)
    ├── ScreenModel[] (from ScreenModelContainer)
    ├── Navigation Stack (Push/Pop)
    ├── Override Slot (interrupts)
    └── Loading Tasks
```

**Key Concepts:**
- **Base Screen**: Current "normal" screen (Enter replaces this)
- **Stack**: Pushed screens (Pop returns to previous)
- **Override**: Single interrupt slot (PopOverride clears it)

---

## ScreenManager

Core screen navigation system managing scene loading, unloading, and transitions.

### Key Responsibilities
- Load/unload scenes via Addressables
- Manage navigation stack (Enter, Push, Pop, Override)
- Coordinate loading tasks
- Fire lifecycle events (pre/post enter/exit)
- Support MessagePipe integration

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Current` | ScreenModel | Effective current screen |
| `Previous` | ScreenModel | Previous screen |
| `IsTransitioning` | bool | Whether transition is in progress |
| `CanPop` | bool | Whether stack has more than 1 screen |
| `IsOverriding` | bool | Whether override is active |

### Key Methods

| Method | Description |
|--------|-------------|
| `Enter(screen)` | Replace base screen (from bootstrap/home) |
| `Push(screen)` | Push screen onto stack |
| `Pop()` | Pop top of stack |
| `PushOverride(screen)` | Push interrupt screen |
| `PopOverride()` | Clear override |

### Navigation Flow

```
Bootstrap → Enter(Home) → Push(Gameplay) → Pop() → Push(Settings)
         → PushOverride(Pause) → PopOverride() → ...
```

---

## ScreenModel

ScriptableObject defining a screen's configuration.

```csharp
[CreateAssetMenu(fileName = "New Screen Model", menuName = "Screen/Screen Model")]
public class ScreenModel : ScriptableObject, IScreenDefinition
{
    [Header("Identifier")]
    public ScreenIdentifier screenID;
    
    [Header("Scene")]
    public AssetReference sceneRef;
    public bool unloadOnClose = true;
    
    [Header("Additive Scenes")]
    public AdditiveSceneAddressableData[] additiveScenes;
    
    [Header("Loading Tasks")]
    public ScreenLoadingTask[] loadingTasks;
    
    [Header("Camera")]
    public CameraProfile cameraProfile;
}
```

### ScreenModelContainer

Container for all ScreenModels in the game:

```csharp
public class ScreenModelContainer : ScriptableObject
{
    public ScreenModel[] screens;
    public ScreenModel bootstrapScreen;   // Initial screen
    public ScreenModel homeScreen;       // Home screen
    public ScreenModel gameplayScreen;   // Main gameplay
}
```

---

## Screen Navigation

### Transition Kinds

| Kind | Description |
|------|-------------|
| `Enter` | Replace base screen |
| `Push` | Add to stack |
| `Pop` | Remove from stack |
| `PushOverride` | Interrupt with overlay |
| `PopOverride` | Clear override |

### Transition Context

Metadata describing transition origin:

```csharp
var context = new TransitionContext("screenflow", "player_died");
// Source: screenflow, ui, debug, bootstrap
// Reason: optional description
```

### Transition Completed Event

```csharp
screenManager.OnTransitionCompleted += args =>
{
    Debug.Log($"{args.Kind}: {args.From} → {args.To}");
    // args.Context contains source and reason
};
```

---

## Screen Events

### Global Events

Register listeners that fire for all screens:

```csharp
// GlobalScreenEventProfile (ScriptableObject)
public class GlobalScreenEventProfile : ScriptableObject
{
    public List<IScreenEventListener> configuredListeners;
}

// Listener interface
public interface IScreenEventListener
{
    void OnPreScreenEnter(ScreenEventArgs args);
    void OnPostScreenEnter(ScreenEventArgs args);
    void OnPreScreenExit(ScreenEventArgs args);
    void OnPostScreenExit(ScreenEventArgs args);
}
```

### Local Events

Per-screen events via LocalScreenEventContainer:

```csharp
manager.LocalEventRegistration.RegisterOnScreenOpening(args => { });
manager.LocalEventRegistration.RegisterOnScreenClosing(args => { });
```

### MessagePipe Events

Decoupled events via MessagePipe:

```csharp
// Subscribe
subscriber.Subscribe<PreScreenEnterEvent>(args => { });

// Event classes (marked with [MessagePipeEvent])
public class PreScreenEnterEvent : ScreenMessagePipeEventArgs { }
public class PostScreenEnterEvent : ScreenMessagePipeEventArgs { }
public class PreScreenExitEvent : ScreenMessagePipeEventArgs { }
public class PostScreenExitEvent : ScreenMessagePipeEventArgs { }
```

---

## Screen Loading

### ScreenLoadingTask

Abstract base for async loading operations:

```csharp
public abstract class ScreenLoadingTask : ScriptableObject
{
    public string description;
    
    public abstract int AggregateTask(LoadingContext context);
    public abstract UniTask ExecuteAsync(LoadingContext context, IProgress<float> progress);
}
```

### Built-in Tasks

| Task | Purpose |
|------|---------|
| `VInitializableTask` | Run `IVInitializable` objects in scene |
| `AddressableLoadTask` | Load Addressable assets |
| `CustomTask` | User-defined loading |

### Loading Handler

Progress reporting during transitions:

```csharp
var handler = new LoadingHandler(progressReporter, totalTasks, startProgress);
handler.Report(current, "Loading...");

// Progress: 0.0 to 1.0
```

### ILoadingController

Interface for loading UI integration:

```csharp
public interface ILoadingController
{
    Task ShowAsync();
    Task HideAsync();
    void SetProgress(float progress, string message);
}
```

---

## Screen Transitions

### Transition Settings

Data-driven transition configuration:

```csharp
[Serializable]
public abstract class UITransitionSettings
{
    public float duration = 0.3f;
    public abstract IUITransition Create();
}
```

### Built-in Transitions

| Transition | Description |
|------------|-------------|
| `FadeTransition` | Alpha fade in/out |
| `ScaleTransition` | Scale animation |
| `SlideTransition` | Slide from direction |

### Transition Execution

```csharp
var runner = new UITransitionRunner();
await runner.RunIn(view, new FadeTransition(0.3f));
await runner.RunOut(view, new ScaleTransition(0.25f));
```

---

## ScreenFlow System

Graph-based navigation that decides which screen to navigate to based on events.

### Architecture

```
ScreenFlowManager (VBootstrapManager)
    ├── ScreenFlowGraph (ScriptableObject)
    │       ├── Nodes (ScreenModel + GUID)
    │       └── Transitions (from → to + event + condition)
    ├── ScreenFlowStateContainer
    │       ├── CurrentNode
    │       ├── PreviousNode
    │       └── History
    └── ScreenFlowActor
            ├── Handles Trigger(event)
            └── Calls ScreenManager.Enter/Push
```

### ScreenFlowGraph

Visual graph definition:

```csharp
[CreateAssetMenu(fileName = "ScreenFlowGraph", menuName = "Screen/Screen Flow/Screen Flow Graph")]
public class ScreenFlowGraph : ScriptableObject
{
    public string startNodeGuid;
    public List<ScreenFlowNode> nodes;
    public List<ScreenFlowTransition> transitions;
}
```

### ScreenFlowNode

Single node in the flow:

```csharp
[Serializable]
public class ScreenFlowNode
{
    public string guid;
    public ScreenModel screen;
    public Vector2 editorPosition;  // For visual editor
}
```

### ScreenFlowTransition

Edge between nodes:

```csharp
[Serializable]
public class ScreenFlowTransition
{
    public string fromNodeGuid;
    public string toNodeGuid;
    public string eventName;         // Event that triggers this
    public TransitionCondition condition;  // Optional condition
}
```

### Transition Conditions

| Condition | Description |
|-----------|-------------|
| `AlwaysTrueCondition` | Always passes |
| `AndCondition` | All conditions must pass |
| `OrCondition` | Any condition passes |
| `NotCondition` | Negates child condition |

```csharp
// Custom condition
public class LevelCondition : TransitionCondition
{
    public int requiredLevel;
    public override bool Evaluate() => player.Level >= requiredLevel;
}
```

### Using ScreenFlow

```csharp
// Trigger event to move to next screen
ScreenFlowManager.Instance.Trigger("PlayPressed");
ScreenFlowManager.Instance.Trigger("LevelComplete");
ScreenFlowManager.Instance.Trigger("PlayerDied");

// Get current screen
var current = ScreenFlowManager.Instance.Current;

// Access state
var state = ScreenFlowManager.Instance.State;
Debug.Log($"Last event: {state.LastEvent}");
Debug.Log($"History: {string.Join(" → ", state.History.Select(n => n.Screen.ScreenID.name))}");
```

---

## Component Interactions

### Bootstrap Flow

```
BootstrapManagerCentral
    └── ScreenFlowManager.InitializeBootstrap()
            └── ScreenFlowGraphResolver loads graph
            └── ScreenFlowStateContainer.SetStartNode()
            └── Ready for triggers
```

### Navigation Flow

```
ScreenFlowManager.Trigger("event")
    └── ScreenFlowActor.Trigger(event)
            └── ScreenFlowGraphResolver.Resolve(current, event)
                    └── ScreenFlowStateContainer.TransitionTo(node)
                            └── ScreenManager.Enter(screen)
                                    ├── LoadingHandler.Show()
                                    ├── SceneManager.LoadSceneAsync()
                                    ├── Execute loading tasks
                                    └── Fire events
```

### Camera Integration

```
ScreenManager enters screen
    └── screen.CameraProfile != null
            └── CameraManager.ApplyProfile(screen.CameraProfile)
                    └── Smooth transition via PrimeTween
```

---

## Quick Usage Examples

### Basic Screen Navigation

```csharp
// From any MonoBehaviour
ScreenManager.Instance.Enter(homeScreen);
ScreenManager.Instance.Push(gameplayScreen);
ScreenManager.Instance.Pop();
```

### With Transition Context

```csharp
var context = new TransitionContext("ui", "main_menu");
ScreenManager.Instance.Enter(homeScreen, context);
```

### Using ScreenFlow

```csharp
// Setup in Inspector:
// Bootstrap → Home → (PlayPressed) → Gameplay
//                    → (SettingsPressed) → Settings

// Trigger navigation
ScreenFlowManager.Instance.Trigger("PlayPressed");
```

### Custom Loading Task

```csharp
public class MyLoadingTask : ScreenLoadingTask
{
    public override int AggregateTask(LoadingContext context) => 1;
    
    public override async UniTask ExecuteAsync(LoadingContext context, IProgress<float> progress)
    {
        progress?.Report(0);
        await LoadAssetAsync();
        progress?.Report(1);
    }
}
```
