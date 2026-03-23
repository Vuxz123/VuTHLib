# VuTH Unity Framework - Persistence & SaveSystem Documentation

The VuTH Persistence system provides a comprehensive data persistence solution with encryption, migration, and reactive properties.

---

## Table of Contents

1. [DataPersistenceManager](#datapersistencemanager)
2. [Persistence Package](#persistence-package)
3. [PersistentField](#persistentfield)
4. [SaveSystem](#savesystem)
5. [Save Strategies](#save-strategies)
6. [Encryption](#encryption)
7. [Migration](#migration)
8. [Backends](#backends)
9. [Events](#events)

---

## DataPersistenceManager

Central manager that orchestrates persistence packages and save system.

### Key Responsibilities
- Register/unregister persistence packages
- Coordinate save/load operations
- Support both VContainer DI and non-DI modes
- Handle app lifecycle (save on pause/background)

```csharp
public class DataPersistenceManager : VBootstrapManager<DataPersistenceManager, IDataPersistenceManager>
{
    public void RegisterPackage(IPersistencePackage package);
    public void UnregisterPackage(IPersistencePackage package);
    public void SaveAll();
    public void LoadAll();
    public bool IsInitialized { get; }
    public int PackageCount { get; }
}
```

### Lifecycle Hook

`SaveLifecycleHook` forces save when app is paused or backgrounded:

```csharp
public class SaveLifecycleHook : IInitializable, IDisposable
{
    [Inject]
    public void Initialize()
    {
        ApplicationLifecycleHelper.RegisterOnPausedCallback(OnPaused);
    }
    
    private void OnPaused()
    {
        // Save all dirty packages when app pauses
    }
}
```

---

## Persistence Package

### IPersistencePackage

Interface for data packages:

```csharp
public interface IPersistencePackage
{
    string StorageKey { get; }
    SaveStrategy Strategy { get; }
    bool IsDirty { get; }
    
    Task SaveAsync();
    Task LoadAsync();
    void MarkDirty();
    void ClearDirty();
}
```

### PersistencePackage<TData>

Base class for persistence packages with R3 reactive properties:

```csharp
public abstract class PersistencePackage<TData> : IPersistencePackage<TData>, IDisposable
    where TData : class
{
    public TData Data { get; protected set; }
    
    // Debounce settings
    public float DebounceSeconds { get; set; } = 5f;
    
    // Reactive property for UI binding
    public ReadOnlyReactiveProperty<TData> Observable { get; }
}
```

### Example: PlayerProfile Package

```csharp
public class PlayerProfileDTO
{
    public string PlayerName = "Player";
    public int Level = 1;
    public long Gold = 0;
    public long Exp = 0;
}

public class PlayerProfilePackage : PersistencePackage<PlayerProfileDTO>
{
    // Reactive fields
    public PersistentField<string> PlayerName;
    public PersistentField<int> Level;
    public PersistentField<long> Gold;
    
    public PlayerProfilePackage()
    {
        StorageKey = "player_profile";
        Strategy = SaveStrategy.Debounced;
        
        // Initialize with defaults
        Data = new PlayerProfileDTO();
        
        // Create reactive fields
        PlayerName = new PersistentField<string>(this, nameof(PlayerName), () => Data.PlayerName);
        Level = new PersistentField<int>(this, nameof(Level), () => Data.Level);
        Gold = new PersistentField<long>(this, nameof(Gold), () => Data.Gold);
    }
}
```

---

## PersistentField<T>

Smart data field wrapper with R3 reactive properties:

```csharp
public class PersistentField<T> : IDisposable
{
    // Observable property for UI binding
    public ReadOnlyReactiveProperty<T> Observable { get; }
    
    // Current value
    public T Value { get; set; }
    
    // Implicit conversion
    public static implicit operator T(PersistentField<T> field) => field.Value;
}
```

### Features
- **Reactive**: UI automatically updates when value changes
- **Lazy Loading**: Value loaded on first access
- **Dirty Tracking**: Notifies package when modified
- **Thread-Safe**: Uses R3 for thread-safe observables

### Usage with UI (R3)

```csharp
// In your UI script
playerProfile.Gold
    .Subscribe(gold => goldText.text = gold.ToString("N0"))
    .AddTo(this);
```

---

## SaveSystem

### SaveService

Core save/load pipeline:

```
Save: Serialize → Encrypt → Save to Backend
Load: Load from Backend → Decrypt → Deserialize → Migrate → Return
```

```csharp
public class SaveService
{
    public async Task SaveAsync<T>(string key, T data, CancellationToken ct = default);
    public async Task<T> LoadAsync<T>(string key, CancellationToken ct = default);
}
```

### ISaveService

Interface for save operations:

```csharp
public interface ISaveService
{
    Task SaveAsync<T>(string key, T data, CancellationToken ct = default);
    Task<T> LoadAsync<T>(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key);
    Task DeleteAsync(string key);
}
```

### ISaveManager

Combined interface:

```csharp
public interface ISaveManager : ISaveService, ICommonManager { }
```

---

## Save Strategies

Controls when data is persisted:

```csharp
public enum SaveStrategy
{
    Immediate,   // Save right away (sensitive data like IAP)
    Debounced,   // Wait for X seconds without changes (gameplay data)
    Manual       // Explicit save only
}
```

### Usage

```csharp
// Immediate - for sensitive data
public class IAPDataPackage : PersistencePackage<IAPData>
{
    public IAPDataPackage() { Strategy = SaveStrategy.Immediate; }
}

// Debounced - for gameplay data
public class PlayerProfilePackage : PersistencePackage<PlayerData>
{
    public PlayerProfilePackage() 
    { 
        Strategy = SaveStrategy.Debounced;
        DebounceSeconds = 5f; // Wait 5 seconds
    }
}

// Manual - explicit save only
public class DebugDataPackage : PersistencePackage<DebugData>
{
    public DebugDataPackage() { Strategy = SaveStrategy.Manual; }
    
    public void SaveNow() => MarkDirty();
}
```

---

## Encryption

### IEncryptor

Interface for encryption:

```csharp
public interface IEncryptor
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
```

### Built-in Encryptors

| Encryptor | Description |
|-----------|-------------|
| `NoOpEncryptor` | No encryption (testing) |
| `XorEncryptor` | XOR cipher (fast, weak) |
| `Rot13Encryptor` | ROT13 (simple) |
| `AesEncryptor` | AES-256 (strong) |
| `DefaultAesEncryptor` | AES with default key |

### Configuration

```csharp
// In SaveServiceAdapterProfile
[SerializeReference]
public IEncryptor encryptor = new AesEncryptor();
```

---

## Migration

### ISaveMigrator

Interface for version migration:

```csharp
public interface ISaveMigrator
{
    int FromVersion { get; }
    int ToVersion { get; }
    string Migrate(string rawPayload);
}
```

### Example Migrator

```csharp
public class V1ToV2Migrator : ISaveMigrator
{
    public int FromVersion => 1;
    public int ToVersion => 2;
    
    public string Migrate(string rawPayload)
    {
        // V1 format: {"gold": 100}
        // V2 format: {"gold": 100, "gems": 0}
        
        var data = JsonUtility.FromJson<V1Data>(rawPayload);
        var newData = new V2Data 
        {
            gold = data.gold,
            gems = 0  // Add new field
        };
        return JsonUtility.ToJson(newData);
    }
}
```

### SaveMigrationChain

Chain of migrators for sequential upgrades:

```csharp
var chain = new SaveMigrationChain(serializer);
chain.AddMigrator(new V1ToV2Migrator());
chain.AddMigrator(new V2ToV3Migrator());

// Auto-migrate from v1 to v3
var migrated = chain.Migrate(rawPayload, fromVersion: 1, toVersion: 3);
```

---

## Backends

### ISaveBackend

Interface for storage:

```csharp
public interface ISaveBackend
{
    Task SaveRawAsync(string key, string data, CancellationToken ct = default);
    Task<string> LoadRawAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
```

### Built-in Backends

| Backend | Description |
|---------|-------------|
| `JsonFileSaveBackend` | JSON files in persistentDataPath |
| `PlayerPrefsSaveBackend` | Unity PlayerPrefs |

### JsonFileSaveBackend

```csharp
// Saves to: Application.persistentDataPath/{key}.json
var backend = new JsonFileSaveBackend();
await backend.SaveRawAsync("player", jsonData);
var loaded = await backend.LoadRawAsync("player");
```

### Configuration

```csharp
// In SaveServiceAdapterProfile
[SerializeReference]
public ISaveBackend backend = new JsonFileSaveBackend();
```

---

## Events

### MessagePipe Events

```csharp
[MessagePipeEvent(EventScope.Global)]
public class SaveEvent
{
    public string Key { get; set; }
    public bool IsSave { get; set; }  // true = save, false = load
    public bool Success { get; set; }
    public string Error { get; set; }
}

// Subscribe
subscriber.Subscribe<SaveEvent>(e =>
{
    if (e.Success)
        Debug.Log($"Saved {e.Key}");
    else
        Debug.LogError($"Failed: {e.Error}");
});
```

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    DataPersistenceManager                   │
│                  (VBootstrapManager)                        │
├─────────────────────────────────────────────────────────────┤
│  RegisterPackage()    │  SaveAll()    │  LoadAll()        │
└──────────┬────────────┴───────┬────────┴───────┬────────────┘
           │                    │               │
           ▼                    ▼               ▼
┌──────────────────┐  ┌──────────────┐  ┌──────────────┐
│ PersistencePackage│  │  SaveService │  │ SaveLifecycle│
│  - PlayerProfile │  │    (Pipeline)│  │    Hook     │
│  - SettingsData  │◄─┤ Serialize   │  │ (App pause) │
│  - InventoryData │  │ Encrypt     │  └──────────────┘
└────────┬─────────┘  │ Save Backend│
         │            └──────┬───────┘
         │                   │
         ▼                   ▼
┌──────────────────┐  ┌──────────────┐
│  PersistentField │  │ ISaveBackend  │
│  (R3 Reactive)  │  │ - JsonFile    │
│  - Observable    │  │ - PlayerPrefs│
└──────────────────┘  └──────────────┘
```

---

## Quick Usage Examples

### Creating a Persistence Package

```csharp
public class MyDataPackage : PersistencePackage<MyDataDTO>
{
    public PersistentField<int> Score;
    public PersistentField<string> PlayerName;
    
    public MyDataPackage()
    {
        StorageKey = "my_data";
        Strategy = SaveStrategy.Debounced;
        Data = new MyDataDTO();
        
        Score = new PersistentField<int>(this, nameof(Score), () => Data.Score, v => Data.Score = v);
        PlayerName = new PersistentField<string>(this, nameof(PlayerName), () => Data.PlayerName);
    }
}
```

### Registering a Package

```csharp
var package = new MyDataPackage();
DataPersistenceManager.Instance.RegisterPackage(package);
```

### Reacting to Changes in UI

```csharp
package.Score
    .Subscribe(score => scoreText.text = score.ToString())
    .AddTo(this);
```

### Manual Save

```csharp
// For Manual strategy packages
package.MarkDirty();
DataPersistenceManager.Instance.SaveAll();
```

### Custom Encryptor

```csharp
public class CustomEncryptor : IEncryptor
{
    private const string Key = "my-secret-key";
    
    public string Encrypt(string plainText)
    {
        // Your encryption logic
    }
    
    public string Decrypt(string cipherText)
    {
        // Your decryption logic
    }
}
```
