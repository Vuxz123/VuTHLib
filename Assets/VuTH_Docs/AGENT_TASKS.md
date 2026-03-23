# VuTH Docs - Agent Task Prompts

## Task 1: Common Layer
```
Analyze and document the Common layer of the VuTH Unity framework at C:\Users\DPC00176\VuTH Lib\Assets\_VuTH\Common

Document these components:
1. DI - VContainer integration (RootScopeContainer, SceneScopeContainer, IBootstrapVContainerConfigurator, IVContainerConfigurator)
2. Editor utilities (FlagSettingWindow, PreviewDemoWindow, Field/FlagData/Helpers/Init/ScenePostprocessor/Settings/Tools/UI)
3. Log system (LogUtils, DevLogExtensions - log levels, threading, colors, rich text)
4. MessagePipe (Attributes, Configuration, Core, Editor, Integration)
5. Scene utilities (SceneField, SceneSelectorAttribute, EditorSceneUtil)
6. Init system (IVInitializable, VInitializeProfile, VInitializeInvokeSite)
7. Base classes (VSingleton, VManager, VBootstrapManager, ICommonManager)

For each component, document:
- Purpose and what it does
- Key classes and their roles
- How it interacts with other components
- Patterns used

Output to a markdown file in C:\Users\DPC00176\.openclaw\workspace\vuth-common-docs.md
```

## Task 2: Bootstrap & Camera
```
Analyze and document the Bootstrap and Camera systems of the VuTH Unity framework at C:\Users\DPC00176\VuTH Lib\Assets\_VuTH\Core

Document:
1. Bootstrap System - BootstrapManagerCentral, BootstrapProfile, BootstrapProfileUtilities, BoostrapManagerCentralConst
2. Camera System - CameraManager, CameraProfile, VirtualCamera, ICameraManager

For each component:
- Purpose and what it does
- Key classes and their roles
- How components interact
- Patterns used

Output to C:\Users\DPC00176\.openclaw\workspace\vuth-bootstrap-camera-docs.md
```

## Task 3: Screen & ScreenFlow
```
Analyze and document the Screen and ScreenFlow systems of the VuTH Unity framework at C:\Users\DPC00176\VuTH Lib\Assets\_VuTH\Core\GameCycle

Document:
1. Screen System (Core) - ScreenManager, ScreenModel, ScreenModelContainer, IScreenManager, ScreenMetaData
2. Screen Events - Global (GlobalScreenEventProfile, IScreenEventListener), Local (LocalScreenEventContainer), MessagePipe events
3. Screen Loading - LoadingHandler, LoadingContext, ScreenLoadingTask, VInitializableTask, ILoadingController
4. Screen Transition - TransitionContext, TransitionKind, TransitionCompletedEventArgs, IUITransition
5. ScreenFlow - ScreenFlowManager, ScreenFlowGraph, ScreenFlowActor, ScreenFlowStateContainer, IScreenFlowResolver
6. ScreenFlow Conditions - TransitionCondition, AlwaysTrueCondition, AndCondition, OrCondition, NotCondition

For each:
- Purpose and what it does
- Key classes and roles
- Navigation flow (Enter, Push, Pop, Override)
- Event patterns

Output to C:\Users\DPC00176\.openclaw\workspace\vuth-screen-docs.md
```

## Task 4: Persistence & SaveSystem
```
Analyze and document the Persistence and SaveSystem of the VuTH Unity framework at C:\Users\DPC00176\VuTH Lib\Assets\_VuTH\Core\Persistant

Document:
1. DataPersistenceManager - IPersistencePackage, PersistencePackage, PersistentField, SaveLifecycleHook
2. SaveSystem - SaveService, ISaveManager, ISaveService, SaveServiceManager
3. Persistence Package - SaveStrategy (Immediate, Debounced, Manual), PlayerProfile, PlayerProfileDTO
4. Backends - ISaveBackend, JsonFileSaveBackend, PlayerPrefsSaveBackend
5. Encryption - IEncryptor (AesEncryptor, XorEncryptor, Rot13Encryptor, NoOpEncryptor, etc.)
6. Migration - ISaveMigrator, SaveMigrationChain, DefaultSaveMigrator
7. Serialization - ISerializer, JsonSerializer, NewtonsoftJsonSerializer
8. Events - SaveEvent (via MessagePipe)

For each:
- Purpose and what it does
- Save/load pipeline
- How encryption, migration work
- R3 ReactiveProperty usage in PersistentField

Output to C:\Users\DPC00176\.openclaw\workspace\vuth-persistence-docs.md
```

## Task 5: Pool System
```
Analyze and document the Pool system of the VuTH Unity framework at C:\Users\DPC00176\VuTH Lib\Assets\_VuTH\Core\Pool

Document:
- PoolManager - main pooling system
- IPoolable - interface for poolable objects (OnSpawn, OnDespawn)
- IPoolManager - interface with operations, lifecycle, organization, analytics
- PoolConfig - per-prefab configuration (warmup, size limits, overflow)
- PoolExtensions - helper extension methods
- PoolAnalytics - analytics and profiling
- PoolStats - statistics tracking
- Overflow behaviors: Expand, ReturnNull, RecycleOldest

For each:
- Purpose and what it does
- How object pooling works
- Analytics and tracking features

Output to C:\Users\DPC00176\.openclaw\workspace\vuth-pool-docs.md
```

## Task 6: Window/UI System
```
Analyze and document the Window/UI system of the VuTH Unity framework at C:\Users\DPC00176\VuTH Lib\Assets\_VuTH\Core\Window

Document:
1. Base Classes - UIViewBase, PopupBase, FullScreenBase
2. Window Management - WindowManager, IWindowManager, WindowOptions, UIViewConfig
3. Window Types - WindowType (FullScreenPopup, Popup, System)
4. UI Layers - UILayer (Screen, Popup, System, Tutorial, Debug)
5. Transitions - IUITransition, FadeTransition, ScaleTransition, SlideTransition, UITransitionFactory, UITransitionRunner, UITransitionSettings
6. Input Blocking - UIInputBlocker, IUIInputBlocker
7. IWindowDefinition - per-window defaults interface
8. PrimeTween usage for animations

For each:
- Purpose and what it does
- How window stacking works
- Transition system
- Input blocking during transitions

Output to C:\Users\DPC00176\.openclaw\workspace\vuth-window-docs.md
```
