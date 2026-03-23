# VuTH Unity Framework - Window/UI System Documentation

The VuTH Window system provides a comprehensive UI management solution with window stacking, transitions, and input blocking.

---

## Table of Contents

1. [Window System Overview](#window-system-overview)
2. [Base Classes](#base-classes)
3. [WindowManager](#windowmanager)
4. [Window Types & Layers](#window-types--layers)
5. [Transitions](#transitions)
6. [Input Blocking](#input-blocking)
7. [Usage Examples](#usage-examples)

---

## Window System Overview

```
WindowManager
    ├── Window Stack (ordered by UILayer)
    ├── Input Blocker
    └── Transition Factory
```

**Key Concepts:**
- **Windows**: Modal overlays within a Screen
- **Stack**: Multiple windows can be open simultaneously
- **Layers**: Sorting order (Screen → Popup → System → Tutorial → Debug)
- **Transitions**: Smooth animations using PrimeTween

---

## Base Classes

### UIViewBase

Base class for all UI views:

```csharp
public abstract class UIViewBase : MonoBehaviour, IUIView
{
    public GameObject GameObject { get; }
    public Canvas Canvas { get; private set; }
    public CanvasGroup CanvasGroup { get; private set; }
    
    protected bool IsShowing { get; private set; }
    
    protected virtual void Awake();
    public virtual void Setup(object data);
    public virtual UniTask Show(IUITransition transition = null);
    public virtual UniTask Hide(IUITransition transition = null);
    public virtual void OnBackPressed();
}
```

### PopupBase

Modal popup with dimmed background:

```csharp
public abstract class PopupBase : UIViewBase, IWindowDefinition, ITransitionTarget, IBackgroundDimmer
{
    public virtual WindowType WindowType => WindowType.Popup;
    public virtual UILayer Layer => UILayer.Popup;
    public virtual string TransitionPreset => "Scale";
    
    [SerializeField] private CanvasGroup dimmer;
    
    public virtual void Setup(object data);
    public void TryClose();
}
```

### FullScreenBase

Full-screen overlay that blocks interaction below:

```csharp
public abstract class FullScreenBase : UIViewBase, IWindowDefinition
{
    public virtual WindowType WindowType => WindowType.FullScreenPopup;
    public virtual UILayer Layer => UILayer.Popup;
}
```

---

## WindowManager

### IWindowManager

Interface for window operations:

```csharp
public interface IWindowManager : ICommonManager
{
    // Open window and wait for result
    UniTask<TResult> Open<TWindow, TResult>(object data = null) where TWindow : UIViewBase;
    
    // Open with options
    UniTask<TResult> Open<TWindow, TResult>(WindowOptions options) where TWindow : UIViewBase;
    
    // Close operations
    void Close<TWindow>() where TWindow : UIViewBase;
    void CloseTop();
    void CloseTop(object result);
    
    // Query
    bool IsShowing<TWindow>() where TWindow : UIViewBase;
    TWindow GetWindow<TWindow>() where TWindow : UIViewBase;
}
```

### WindowManager

Main window controller:

```csharp
public class WindowManager : VBootstrapManager<WindowManager, IWindowManager>
{
    [SerializeField] private WindowProfile profile;
    [SerializeField] private UITransitionFactory transitionFactory;
    
    // Window stack (top = last)
    private readonly Stack<UIViewBase> _windowStack = new();
}
```

### Window Options

```csharp
public class WindowOptions
{
    public object Data { get; set; }
    public WindowType WindowType { get; set; } = WindowType.Popup;
    public string TransitionPreset { get; set; } = "Scale";
    public IUITransition TransitionInSettings { get; set; }
    public IUITransition TransitionOutSettings { get; set; }
}
```

---

## Window Types & Layers

### WindowType

| Type | Description |
|------|-------------|
| `FullScreenPopup` | Full screen, closes previous content |
| `Popup` | Modal dialog with dimmed background |
| `System` | System overlay (notifications, toasts) |

### UILayer

Sorting layers (higher = on top):

| Layer | Base Order | Description |
|-------|------------|-------------|
| `Screen` | 0 | Base screen content |
| `Popup` | 1000 | Regular popups |
| `System` | 2000 | System notifications |
| `Tutorial` | 3000 | Tutorial overlays |
| `Debug` | 4000 | Debug panels |

### UIViewConfig

Configuration for window prefabs:

```csharp
[Serializable]
public class UIViewConfig
{
    public string id;
    public GameObject prefab;
    public UILayer layer;
    public WindowType windowType = WindowType.Popup;
    public bool cacheable = true;
    public bool blockInput = true;
    public bool closeOnBackPress = true;
    public string transitionPreset = "Scale";
}
```

---

## Transitions

### IUITransition

Interface for transitions:

```csharp
public interface IUITransition
{
    float Duration { get; }
    UniTask In(IUIView view);
    UniTask Out(IUIView view);
}
```

### Built-in Transitions

| Transition | Description |
|------------|-------------|
| `FadeTransition` | Alpha fade in/out |
| `ScaleTransition` | Scale animation (pop in/out) |
| `SlideTransition` | Slide from direction |

### FadeTransition

```csharp
public class FadeTransition : IUITransition
{
    public float Duration { get; }
    
    public FadeTransition() : this(0.3f) { }
    public FadeTransition(float duration);
    
    public async UniTask In(IUIView view)
    {
        await view.CanvasGroup.FadeIn(duration);
    }
    
    public async UniTask Out(IUIView view)
    {
        await view.CanvasGroup.FadeOut(duration);
    }
}
```

### ScaleTransition

```csharp
public class ScaleTransition : IUITransition
{
    public float Duration { get; }
    public float StartScale { get; }
    public float EndScale { get; }
    
    // Default: 0f → 1f (pop in), 1f → 0f (pop out)
}
```

### UITransitionFactory

Worker-based transition factory:

```csharp
public sealed class UITransitionFactory : MonoBehaviour
{
    [SerializeField] private bool useDefaultWorkers = true;
    [SerializeField] private List<UITransitionWorkerBase> customWorkers;
    
    public IUITransition Create(string presetName);
    public IUITransition Create(IUITransitionSettings settings);
}
```

### UITransitionRunner

Centralized transition execution:

```csharp
public sealed class UITransitionRunner
{
    public async UniTask RunIn(IUIView view, IUITransition transition);
    public async UniTask RunOut(IUIView view, IUITransition transition);
}
```

---

## Input Blocking

### UIInputBlocker

Global input blocker with reference counting:

```csharp
public class UIInputBlocker : MonoBehaviour
{
    public bool IsBlocked { get; }
    
    public void Block(string reason);
    public void Unblock(string reason);
}
```

### Usage

Windows automatically block input during transitions:

```csharp
// PopupBase implements ITransitionInputBlocker
public abstract class PopupBase : UIViewBase, ITransitionInputBlocker
{
    public virtual void BlockInput();
    public virtual void UnblockInput();
}
```

---

## IWindowDefinition

Per-window defaults interface:

```csharp
public interface IWindowDefinition
{
    WindowType WindowType { get; }
    UILayer Layer { get; }
    string TransitionPreset { get; }
    IUITransition TransitionInSettings { get; }
    IUITransition TransitionOutSettings { get; }
}
```

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      WindowManager                          │
│                  (VBootstrapManager)                        │
├─────────────────────────────────────────────────────────────┤
│  Window Stack                                              │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ [Debug] (Layer 4000)                                │  │
│  │ [Tutorial] (Layer 3000)                             │  │
│  │ [System] (Layer 2000)                              │  │
│  │ [Popup] (Layer 1000) ← Top                         │  │
│  └─────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│  UITransitionFactory                                       │
│  ├── FadeTransition                                        │
│  ├── ScaleTransition                                       │
│  └── SlideTransition                                       │
├─────────────────────────────────────────────────────────────┤
│  UIInputBlocker (reference counting)                       │
└─────────────────────────────────────────────────────────────┘
```

---

## Quick Usage Examples

### Creating a Popup

```csharp
public class ConfirmPopup : PopupBase
{
    [SerializeField] private Button okButton;
    [SerializeField] private Button cancelButton;
    
    public override void Setup(object data)
    {
        base.Setup(data);
        
        okButton.onClick.AddListener(() => TryClose(true));
        cancelButton.onClick.AddListener(() => TryClose(false));
    }
    
    public void TryClose(bool confirmed)
    {
        if (confirmed)
            TryRequestClose(confirmed);
        else
            TryClose();
    }
}
```

### Opening a Window

```csharp
// Open and wait for result
var result = await WindowManager.Instance
    .Open<ConfirmPopup, bool>(new WindowOptions 
    {
        Data = "Are you sure?"
    });

if (result)
    Debug.Log("User confirmed!");
```

### Opening with Preset

```csharp
var result = await WindowManager.Instance.Open<MyPopup, bool>(
    new WindowOptions 
    {
        Data = data,
        TransitionPreset = "Scale"
    });
```

### Custom Transition

```csharp
public class MyTransition : IUITransition
{
    public float Duration => 0.5f;
    
    public async UniTask In(IUIView view)
    {
        // Custom animation
        await view.CanvasGroup.FadeIn(0.3f);
        await view.transform.ScaleAsync(1f, 0.2f);
    }
    
    public async UniTask Out(IUIView view)
    {
        await view.CanvasGroup.FadeOut(0.3f);
    }
}

// Use
await window.Show(new MyTransition());
```

### Closing Top Window

```csharp
// Close with result
WindowManager.Instance.CloseTop(someResult);

// Close without result
WindowManager.Instance.CloseTop();
```

### Check if Window is Open

```csharp
if (WindowManager.Instance.IsShowing<SettignsPopup>())
{
    // Update existing popup
}
```

---

## Event Flow

```
WindowManager.Open<Popup>()
    └── Create window (instantiate prefab)
            └── Setup(data)
                    └── Show(transition)
                            ├── BlockInput()
                            ├── Run transition In
                            │       └── PrimeTween animation
                            └── OnTransitionCompleted
                                    └── UnblockInput()

WindowManager.CloseTop()
    └── Hide(transition)
            ├── Run transition Out
            ├── OnTransitionCompleted
            └── Destroy/Return to pool
```
