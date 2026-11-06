# VuTH Unity Framework - Window/UI System Documentation

The Window system opens popups and other overlays on top of the current Screen. It stacks them, plays their
transitions and returns their results.

---

## Table of Contents

1. [Overview](#overview)
2. [Opening and Closing](#opening-and-closing)
3. [Base Classes](#base-classes)
4. [Window Options](#window-options)
5. [Window Types and Sorting](#window-types-and-sorting)
6. [Transitions](#transitions)
7. [Input Blocking](#input-blocking)
8. [Back Button](#back-button)
9. [Stack State](#stack-state)
10. [Duplicates](#duplicates)
11. [Queue](#queue)
12. [Toasts](#toasts)
13. [Screen Changes](#screen-changes)
14. [Kept Instances](#kept-instances)
15. [Typed Windows](#typed-windows)
16. [Tabs and Safe Area](#tabs-and-safe-area)
17. [Window Events](#window-events)
18. [Prefabs, Pooling and Preloading](#prefabs-pooling-and-preloading)
19. [Debugging](#debugging)
20. [Configuration](#configuration)

---

## Overview

```
WindowManager (bootstrap manager, WindowManager.prefab)
    ├── Window stack (bottom → top)
    ├── IWindowPrefabProvider (Addressables by default)
    ├── IPoolManager (instances; Instantiate / Destroy without a pool)
    ├── IUIInputBlocker (blocks input during transitions)
    └── IUITransitionFactory (creates transitions)
```

- Operations run one after another, so an open and a close never interleave. Overlays (`IWindowOverlay`, such
  as toasts) are the exception: they never wait for, nor delay, the other windows.
- A window that fails to open, throws, or is destroyed is always removed from the stack, and whoever awaits it is
  released. Event handlers and view callbacks that throw are logged; they do not break the flow.
- Windows close on a Screen change only through the optional Screen integration (see
  [Screen Changes](#screen-changes)).

---

## Opening and Closing

```csharp
public interface IWindowManager : ICommonManager
{
    // Completes when the window CLOSES, with its result.
    UniTask<TResult> Open<TWindow, TResult>(object data = null, CancellationToken ct = default);
    UniTask<TResult> Open<TWindow, TResult>(WindowOptions options, CancellationToken ct = default);
    UniTask Open<TWindow>(object data = null, CancellationToken ct = default);

    // Completes once the window is SHOWN (transition in finished); returns it, or null if it could not open.
    UniTask<TWindow> Show<TWindow>(object data = null);
    UniTask<TWindow> Show<TWindow>(WindowOptions options);

    UniTask Close(UIViewBase window, object result = null); // completes once closed
    UniTask CloseTop(object result = null);
    UniTask CloseAll(bool immediate = false, bool forceCleanup = false);
    UniTask<bool> HandleBackPressAsync();

    UniTask<TResult> Enqueue<TWindow, TResult>(WindowOptions options, int priority = 0, CancellationToken ct = default);
    UniTask Enqueue<TWindow>(object data = null, int priority = 0, CancellationToken ct = default);
    int QueuedCount { get; }
    void ClearQueue();

    UniTask ShowToast<TToast>(object data = null, float? duration = null);
    UniTask CloseForScreenChangeAsync(bool immediate = true);

    UniTask PreloadAsync<TWindow>(int instances = 1);
    IReadOnlyList<WindowInfo> GetOpenWindows(); // bottom → top

    UIViewBase TopWindow { get; }              // topmost window that is not an overlay (never a toast)
    int WindowCount { get; }                   // every open window, toasts included
    bool IsTransitioning { get; }
    bool HasWindow<TWindow>();                 // open and not closing; derived types match
    TWindow GetWindow<TWindow>();              // the topmost such window
    void ReleasePrefab<TWindow>();
    void ClearPrefabCache();

    WindowStackState StackState { get; }
    event Action<WindowStackState> OnStackChanged;
    event Action OnBackPressUnhandled;

    event Action<UIViewBase> OnWindowOpened;
    event Action<UIViewBase> OnWindowClosed;
    event Action<WindowEvent> OnWindowEvent;
}
```

> `await Open<X>()` waits until the window is closed. To continue as soon as it is visible, use `Show<X>()`.

```csharp
// Ask and wait for the answer
var confirmed = await windowManager.Open<ConfirmPopup, bool>("Delete the save?");

// Show and keep going
var shop = await windowManager.Show<ShopPopup>();
```

**Results.** `Open<TWindow, TResult>` returns the value the window was closed with: `Close(result)` in the view, or
`Close(window, result)` / `CloseTop(result)` on the manager (`CloseTop` skips toasts). It returns `default` in these cases:

- the window is closed without a result, by the back button, or by `CloseAll`;
- the window cannot be opened (for example, its prefab is missing);
- the result has another type (this is also logged as an error).

**Cancellation.** Canceling the token closes the window with its out transition, then throws
`OperationCanceledException`.

**CloseAll.**

| Call | Behavior |
|------|----------|
| `CloseAll()` | Every window plays its out transition. |
| `CloseAll(immediate: true)` | Windows close without their transitions. |
| `CloseAll(forceCleanup: true)` | Windows are removed at once, without waiting for operations in progress. |

Each window is closed exactly once, whatever the mode.

---

## Base Classes

### UIViewBase

```csharp
public abstract class UIViewBase : MonoBehaviour, IUIView
{
    Canvas Canvas { get; }           // added if missing, with a GraphicRaycaster (needed for clicks)
    CanvasGroup CanvasGroup { get; } // added if missing
    bool IsOpen { get; }

    virtual void Setup(object data);  // before the window is shown
    virtual void OnViewShown();       // after the transition in
    virtual void OnViewHidden();      // after the transition out, before returning to the pool
    virtual void OnBackPressed();     // default: an IWindowBackHandler child (e.g. tabs) first, else Close()

    protected WindowOptions Options { get; } // resolved options of the current opening (null when closed)
    protected virtual void OnOpening();      // each opening, after Options is set, before Setup

    protected void Close(object result = null);
    bool TryRequestClose(object result = null); // false when not open
    void ForceClose();                          // closes at once, without transition; Open returns default
}
```

Instances are pooled, so the same object is opened many times. Before each opening the manager puts it back in its
rest state: hidden, not interactable, and with the transition root at its original scale and position. Reset your
own state in `Setup`.

### WindowBase

The shared base of `PopupBase` and `FullScreenBase`. It holds the optional transition settings (Inspector) and the
per-window defaults that can be overridden:

- `TransitionPreset`, which defaults to `WindowOptions.DefaultTransitionPreset`;
- `BlockInput`;
- `AllowBackClose` (set it to false for confirmations that must be answered);
- `DuplicatePolicy`, `KeepOnScreenChange`, `KeepInstance` and `CloseOnOutsideClick`.

### PopupBase / PopupBase&lt;TResult&gt;

A modal dialog with a dimmed background (`WindowType.Popup`, `"Scale"` preset).

- **Inspector:** an optional `transitionRoot`, and an optional `dimmerCanvasGroup` driven by `FadeTransition`.
- **Outside click:** with `CloseOnOutsideClick` (override it, or set it in the options per opening), a click on the
  dimmer, or on the `outsideClickArea` graphic, closes the popup. Override `OnOutsideClick()` to do something else.
- `PopupBase<TResult>` adds `Close(TResult result)`; `PopupBase<TData, TResult>` also types the data (see
  [Typed Windows](#typed-windows)).

### FullScreenBase

A full-screen dialog (`WindowType.FullScreenPopup`), such as settings or inventory. It has the same options as
`PopupBase` (see `WindowBase`), and the same typed variants: `FullScreenBase<TResult>` and
`FullScreenBase<TData, TResult>`.

Once it is shown, the windows below it stop rendering: their canvases are disabled, which saves overdraw and canvas
rebuilds on mobile. They render again as soon as it starts closing. `StackState.IsScreenCovered` becomes true, so
the game can stop rendering the Screen too (see [Stack State](#stack-state)).

### ToastBase

A short non-blocking message; see [Toasts](#toasts).

### IWindowDefinition

Per-window defaults, implemented by `PopupBase` and `FullScreenBase`: `WindowType`, `TransitionPreset`,
`TransitionInSettings`, `TransitionOutSettings`, `BlockInput`, `CloseOnBackPress`, `DuplicatePolicy`,
`KeepOnScreenChange`, `KeepInstance` and `CloseOnOutsideClick`.

---

## Window Options

```csharp
public class WindowOptions
{
    public object Data { get; set; }
    public WindowType? WindowType { get; set; }
    public string TransitionPreset { get; set; }
    public UITransitionSettings TransitionInSettings { get; set; }
    public UITransitionSettings TransitionOutSettings { get; set; }
    public bool? CloseOnBackPress { get; set; }
    public bool? BlockInput { get; set; }
    public WindowDuplicatePolicy? Duplicate { get; set; }
    public bool? KeepOnScreenChange { get; set; }
    public bool? KeepInstance { get; set; }
    public bool? CloseOnOutsideClick { get; set; }
}
```

Each field is taken from the first source that sets it, in this order:

1. The value set in the options, whatever it is (even `WindowType.Popup` or `"Scale"`).
2. The window's `IWindowDefinition`.
3. The built-in default: `Popup`, `"Scale"`, `true`, `true`, `Allow`, `false`, `false`, `false`.

The manager works on a copy, so one options object can be reused for several windows.

---

## Window Types and Sorting

| WindowType | Base sorting order |
|------------|--------------------|
| `Popup`, `FullScreenPopup` | `popupBaseSortingOrder` (1000) |
| `System` (toasts, loading) | `systemBaseSortingOrder` (2000) |
| `Tutorial` | `systemBaseSortingOrder + 1000` |

Each window adds `sortingOrderStep` for every window below it in the stack, so the newest window is on top.

---

## Transitions

```csharp
public interface IUITransition
{
    UniTask In(IUIView view);
    UniTask Out(IUIView view);
    float Duration { get; }
}
```

For each direction, the manager uses the transition settings when they are set, and the preset otherwise.

| Settings (`[SerializeReference]`) | Preset | Effect |
|---|---|---|
| `FadeTransitionSettings` | `Fade` | Alpha, and the dimmer if the view has one |
| `ScaleTransitionSettings` (optional `scaleCurve`) | `Scale` | Scale from zero with alpha; the default eases are OutBack and InBack |
| `SlideTransitionSettings` (direction, distance) | `Slide`, `Slide:Left` / `Right` / `Top` / `Bottom` | Slide from off screen |
| `SequenceTransitionSettings` (steps, parallel) | — | Several transitions together or one after another, e.g. Fade + Slide |

- **Pause-safe:** every built-in transition runs on unscaled time, so it also plays while the game is paused
  (`Time.timeScale = 0`).
- **Failure-safe:** a transition that throws is logged, and the window gets its final state anyway.
- **Factory:** `UITransitionFactory` creates them. It resolves a preset name in this order:
  1. its preset assets (`UITransitionPresetAsset`, *Create → VuTH → Window → Transition Preset*: a name and any
     transition settings, made by designers);
  2. workers registered with `RegisterWorker`;
  3. the built-in Fade, Scale and Slide workers.

  A preset asset overrides a built-in preset of the same name.
- **Element animations:** add `UITransitionElement` to a child (a title, a button) to animate it after the window's
  transition in. It can fade, scale or slide, with its own delay and duration, for example to make buttons pop in
  one after another. Elements are hidden while the window opens, and the window's in transition (and input blocking)
  lasts until they finish.
- **Optional view hooks:** `ITransitionTarget` (which RectTransform to animate), `IBackgroundDimmer` and
  `ITransitionInputBlocker`.

---

## Input Blocking

While a window transitions in or out, `IUIInputBlocker` blocks all UI input. Opt out with the window's or the
options' `BlockInput`, or with the profile's `blockInputDuringTransitions`.

**Input is never blocked while a window is simply open**, so its own buttons can be clicked.

`UIInputBlocker` is reference counted: input stays blocked until every `Block(reason)` has a matching `Unblock`. It
uses a full-screen invisible overlay canvas.

---

## Back Button

`HandleBackPressAsync()` gives the press to the top window (the topmost that is not an overlay; a toast never takes
it). It calls the window's `OnBackPressed()`, which closes it by default. Nothing happens in these cases:

- the window opted out: `CloseOnBackPress` is false. This is the only switch. It comes from the window's
  `AllowBackClose` unless the caller set it in the options, and the caller's value wins.
- the window is still transitioning in.

A child implementing `IWindowBackHandler`, such as `WindowTabGroup`, can consume the press before the window closes.
The method returns whether a window received the press.

**Who reads the back input:** choose one of these two setups, so that a single press is never handled twice.

- **The manager (default).** It reads the optional `backAction` (`InputActionReference`) and Escape (`escapeIsBack`).
  A press that reaches no window raises `OnBackPressUnhandled`, for example to go back a Screen or to ask "quit the
  game?":

  ```csharp
  windowManager.OnBackPressUnhandled += () => screenFlow.GoBack();
  ```
- **The game.** Turn off `handleBackInput` (Inspector, or the `HandleBackInput` property) and call the method from your
  own input handling:

  ```csharp
  if (!await windowManager.HandleBackPressAsync())
      screenFlow.GoBack();
  ```

---

## Stack State

`StackState` (`WindowStackState`) summarizes the stack, and `OnStackChanged` reports every change:

| Field | Meaning |
|-------|---------|
| `ModalCount` / `HasModal` | Open windows that are not overlays (toasts do not count; a loading overlay does) |
| `IsScreenCovered` | A shown full screen window covers the Screen |
| `Top` | Same as `TopWindow` |

```csharp
windowManager.OnStackChanged += state =>
{
    gameplay.SetPaused(state.HasModal);
    screenCamera.enabled = !state.IsScreenCovered;
};
```

---

## Duplicates

What opening a window does when a window of the **same type** is already open. Set it on the window by overriding
`DuplicatePolicy`, or per call with `WindowOptions.Duplicate`.

| Policy | Effect |
|--------|--------|
| `Allow` (default) | Open another instance. |
| `Ignore` | Keep the open window. `Show` returns it, and `Open` completes with its result. Prevents double-tap duplicates. |
| `BringToFront` | Like `Ignore`, and move the window to the top of the stack. |
| `ReplaceExisting` | Close the open window (its `Open` returns default), then open a new one. |

A call that shares an open window does not own it: canceling its token only stops waiting.

```csharp
public sealed class ShopPopup : PopupBase
{
    public override WindowDuplicatePolicy DuplicatePolicy => WindowDuplicatePolicy.Ignore;
}
```

---

## Queue

Some windows must not pile up, such as a daily reward, an offer or a rating prompt. Queue them instead of opening
them:

```csharp
windowManager.Enqueue<DailyRewardPopup>(priority: 10).Forget();
windowManager.Enqueue<RateAppPopup>().Forget();
```

- A queued window opens only when no window is open or opening. One opens at a time: the next one waits until the
  previous one closes.
- The highest priority opens first; equal priorities open in call order.
- `Enqueue` completes when the window closes, with its result.
- Canceling the token removes a waiting window from the queue, or closes it if it is already open.
- With `Duplicate = Ignore` in the options, a window type that is already queued is not queued again.
- `ClearQueue()` cancels every window still waiting. `CloseAll` does not touch the queue.

---

## Toasts

```csharp
public sealed class SavedToast : ToastBase { }

await windowManager.ShowToast<SavedToast>("Saved", duration: 1.5f); // completes when it closes
```

- A toast closes by itself after `duration` seconds (unscaled), or after the toast's `Duration` (2 s by default).
- It is a `System` window and an overlay (`IWindowOverlay`), so:
  - it opens and closes right away: it never waits for, nor delays, a popup transition;
  - it is never the `TopWindow`, and `CloseTop` skips it;
  - it never blocks input, not even during its transitions;
  - it does not receive clicks once shown, unless you override `Interactable`;
  - it never takes the back press;
  - it does not hold back queued windows;
  - it stays open across Screen changes.
- Several toasts can be open at once. Position them in `Setup` if they must not overlap.

---

## Screen Changes

Each window says whether it stays open when the Screen changes, through `KeepOnScreenChange` (default `false`;
`true` for toasts). It can be overridden per call with `WindowOptions.KeepOnScreenChange`.

`CloseForScreenChangeAsync(immediate)` closes every window that does not stay.

To call it automatically, add **`CloseWindowsOnScreenChange`** to the global Screen event profile. It runs when the
current Screen starts exiting and has two options:

- `immediate` (default true): skip the windows' out transitions;
- `clearQueue`: also cancel the queued windows.

It lives in the `VuTH.Window.Screen` assembly, so the Window and Screen modules stay independent of each other.

---

## Kept Instances

By default, a closed window returns to the pool, and the next opening may get another instance, so `Setup` resets
the state. When a window should reopen as it was left (scroll position, selected tab, typed text), override
`KeepInstance => true` or set `WindowOptions.KeepInstance`. The closed instance is then deactivated and reused by the
next opening of its type.

The manager keeps one instance per window type. `DestroyKeptInstances()` releases them, and releasing a prefab
releases its kept instance too.

---

## Typed Windows

```csharp
public sealed class RenamePopup : PopupBase<string, string> // data: current name, result: new name
{
    protected override void OnSetup(string currentName) => input.text = currentName;
    private void OnConfirm() => Close(input.text);
}

// Data and result types are checked at compile time.
var newName = await windowManager.Open<RenamePopup, string, string>(player.Name);
```

These are extension methods (`WindowManagerExtensions`) for windows implementing `ITypedWindow<TData, TResult>`:
`PopupBase<TData, TResult>` and `FullScreenBase<TData, TResult>`. If data of another type arrives through the untyped
API, it is logged and the window receives `default`.

---

## Tabs and Safe Area

**`WindowTabGroup`** (in `_VuTH.Core.Window.UI`) shows one of its `tabs` at a time:

- `SelectTab(i)` switches tab; `TabChanged` reports it.
- The back button returns to the previously selected tab before it closes the window.
- Each opening starts on `defaultTab`, unless `resetOnOpen` is off. Combine that with a kept instance to remember
  the tab.

**`SafeAreaFitter`** fits a RectTransform to `Screen.safeArea` (notch, rounded corners, home indicator). It follows
rotation and resolution changes, and each edge can be turned off. Put it on a full-screen child that holds the
window content, and keep backgrounds and dimmers outside it.

---

## Window Events

`OnWindowEvent` and MessagePipe (`ISubscriber<WindowEvent>`, when the manager is created through DI) report each
window being shown and closed, for analytics or tutorials. Each `WindowEvent` contains:

- `Type`: `Opened` or `Closed`;
- `WindowName`, `WindowType` and `Window`;
- for a close only:
  - `ShownSeconds`: unscaled time the window was shown;
  - `HasResult`: whether it was closed with a result;
  - `CloseCause`: `Closed`, `BackPress`, `CloseAll`, `Forced`, `Canceled` or `Replaced`.

```csharp
windowManager.OnWindowEvent += e =>
{
    if (e.Type == WindowEventType.Closed)
        Analytics.Log("window_closed", e.WindowName, e.CloseCause, e.ShownSeconds);
};
```

---

## Prefabs, Pooling and Preloading

- Prefabs are loaded through `IWindowPrefabProvider`, which defaults to `WindowPrefabCache` (Addressables). The key is
  the class name, or `[WindowAddress("key")]` when two window classes share a name.
- `ReleasePrefab<T>()` releases a prefab together with its pooled and kept instances, because they reference the
  prefab's assets. It is skipped, with a warning, while a window of that type is open. `ClearPrefabCache()` does
  this for every loaded type that is not open.
- Instances come from the `IPoolManager`. Without a pool, windows are instantiated and destroyed instead.
- Destroying an open window (for example on scene unload) removes it from the stack.
- `WithPrefabProvider`, `WithInputBlocker` and `WithTransitionFactory` replace the defaults, for example in tests.
- `PreloadAsync<T>(instances)` loads the prefab and creates pooled instances ahead of time, so the first opening does
  not hitch. It does nothing when the prefab already has a pool.

```csharp
[WindowAddress("UI/Shop/ConfirmPopup")]
public sealed class ConfirmPopup : PopupBase<bool>
{
    [SerializeField] private Button yes, no;

    protected override void Awake()
    {
        base.Awake();
        yes.onClick.AddListener(() => Close(true));
        no.onClick.AddListener(() => Close(false));
    }

    public override bool AllowBackClose => false; // must be answered
}
```

---

## Debugging

In Play Mode, the **Window** tab of the Settings window shows the live stack:

- every window, top first, with its type, sorting order and state (`Opening`, `Open`, `Closing`);
- the number of queued windows;
- whether input is blocked, and why.

Each window has **Select**, **Close** and **Force** buttons, and the tab has **Close All**, **Force Close All** and
**Clear Queue**. From code, `GetOpenWindows()` returns the same snapshot.

---

## Configuration

`Resources/Window/WindowManagerCentralProfile` (Settings window → Window):

| Field | Default | Meaning |
|-------|---------|---------|
| `popupBaseSortingOrder` | 1000 | Sorting order of the bottom Popup / FullScreenPopup |
| `systemBaseSortingOrder` | 2000 | Sorting order of the bottom System window (Tutorial adds 1000) |
| `sortingOrderStep` | 10 | Added per window below in the stack |
| `blockInputDuringTransitions` | true | Block UI input during transitions |

If the profile is missing, the manager logs an error and uses these defaults.
