# VuTH Unity Framework - Persistence & SaveSystem Documentation

The VuTH Persistence system has two layers:

- **SaveSystem**: a save pipeline with pluggable backend, serializer, encryptor and schema migrations.
- **DataPackage**: game data held in packages of reactive fields, saved automatically according to a strategy.

Above all, it never loses a save silently. Data that cannot be read is reported, and it is never replaced unless
it was first backed up.

---

## Table of Contents

1. [DataPersistenceManager](#datapersistencemanager)
2. [Save Slots](#save-slots)
3. [Deleting Data](#deleting-data)
4. [Persistence Package](#persistence-package)
5. [PersistentField](#persistentfield)
6. [SaveSystem](#savesystem)
7. [Load Safety](#load-safety)
8. [Save Strategies](#save-strategies)
9. [Encryption](#encryption)
10. [Migration](#migration)
11. [Backends](#backends)
12. [Configuration](#configuration)
13. [Events](#events)

---

## DataPersistenceManager

Bootstrap manager that owns the packages and wires each of them to the save service according to its strategy.

- Packages listed in the DataPackage profile are created at startup. They are registered in the root scope as
  `IPersistencePackage` and as their concrete type, so they can be injected directly.
- Packages load asynchronously. Await `WaitUntilReadyAsync()` before reading their data.
- `GetPackage<T>()` matches the exact type first, then any package assignable to `T`, such as a base class or an
  interface.
- Two packages cannot share a `StorageKey`. The second one is rejected with an error, because otherwise each would
  overwrite the other's save.

```csharp
public interface IDataPersistenceManager : ICommonManager
{
    void RegisterPackage(IPersistencePackage package);
    void UnregisterPackage(IPersistencePackage package);
    T? GetPackage<T>() where T : class, IPersistencePackage;
    bool TryGetPackage<T>(out T? package) where T : class, IPersistencePackage;
    void SaveAll();
    UniTask SaveAllAsync();
    void LoadAll(bool discardUnsavedChanges = false);
    UniTask LoadAllAsync(bool discardUnsavedChanges = false); // skips dirty packages unless discarding
    UniTask WaitUntilReadyAsync(CancellationToken ct = default); // throws if init failed (e.g. no save service)
    UniTask DeleteAllAsync();                                    // see Deleting Data

    string? ActiveSlot { get; }                                  // see Save Slots
    UniTask SwitchSlotAsync(string? slot, CancellationToken ct = default);
    UniTask DeleteSlotAsync(string? slot);

    new bool IsInitialized { get; }
    int PackageCount { get; }
}
```

If the manager cannot start (no save service), `WaitUntilReadyAsync()` and the packages' `WaitUntilLoadedAsync()`
throw instead of waiting forever.

The SaveServiceManager must initialize first, so give it a lower `BootstrapOrder`. The defaults are 0 for
SaveService and 100 for DataPersistence.

### Lifecycle Hook

`SaveLifecycleHook` is registered as an entry point. It flushes dirty `Debounced` and `OnAppClose` packages:

| Event | Save |
|-------|------|
| Focus lost | async |
| Pause (home button / app switch) | **synchronous**: the OS may kill the app right after |
| Quit | **synchronous**: Unity does not wait for async work |

A synchronous save needs a backend implementing `ISyncSaveBackend`, which both built-in backends do. Otherwise the
hook falls back to a best-effort async save. Call `FlushAllImmediate()` yourself before a forced restart.

---

## Save Slots

A package overriding `IsSlotScoped => true` (game progress, e.g. `PlayerProfilePackage`) has one copy of its data per
save slot. Other packages (settings) keep a single copy whatever the slot.

```csharp
await manager.SwitchSlotAsync("2");  // saves pending changes to the current slot, then loads slot "2"
await manager.SwitchSlotAsync(null); // back to the default slot
await manager.DeleteSlotAsync("2");
```

- A slot-scoped package stores under `slot-{slot}/{key}`. The default slot (null) uses the plain key, so saves made
  before slots existed are the default slot.
- Switching resets the slot-scoped packages to their defaults, then loads the new slot. A slot that was never saved
  starts from defaults.
- A package registered later joins the active slot.

---

## Deleting Data

`package.DeleteAsync()` deletes the stored data, and the `.corrupt` backup if one exists. It then resets the package to
its defaults without saving and re-enables saving if a failed load had disabled it. `manager.DeleteAllAsync()` does
this for every package: use it for "reset progress" or a player's data deletion request.

The defaults come from `CreateDefaultPayload()`, which by default is a new DTO (its field initializers). Override it
when the package defaults differ from the DTO's.

---

## Persistence Package

### IPersistencePackage

```csharp
public interface IPersistencePackage
{
    string StorageKey { get; }     // BaseStorageKey, prefixed with the save slot when slot scoped
    string BaseStorageKey { get; }
    bool IsSlotScoped { get; }
    void SetSlot(string? slot);    // called by the manager
    SaveStrategy Strategy { get; }
    bool IsDirty { get; }
    Observable<bool> DirtyObservable { get; }
    float DebounceSeconds { get; }

    SaveLoadStatus? LoadStatus { get; } // null until the first load completed
    bool CanSave { get; }               // false when overwriting would lose stored data (see Load Safety)
    UniTask WaitUntilLoadedAsync(CancellationToken ct = default); // throws if the package cannot load (no save service)

    void SetSaveService(ISaveService saveService);
    void MarkDirty();
    object ExtractPayload();
    void InjectPayload(object data);
    void SaveNow();
    UniTask SaveNowAsync();       // completes once the newest data is written, even when it joins a running save
    void Load();
    UniTask LoadAsync();
    UniTask DeleteAsync();        // see Deleting Data
}
```

### PersistencePackage&lt;TData&gt;

This is the base class. Implement `ExtractPayload` and `InjectPayload` to map between your fields and a DTO.
`InjectPayload` runs without marking the package dirty. A change made while the data is still loading is replaced by
the loaded data and logs a warning, so await `WaitUntilLoadedAsync()` before changing a package.

The loads, saves and deletes of one package run one after another, never interleaved. A load waits for a save that is
still being written, and two loads never mix.

Override `CreateSchema()` to version the package's data on its own (see [Migration](#migration)).

### Example: PlayerProfile Package

```csharp
[Serializable]
public class PlayerProfileDTO
{
    public string PlayerName = "Player";
    public int Level = 1;
    public long Gold;
}

public class PlayerProfilePackage : PersistencePackage<PlayerProfileDTO>
{
    public PersistentField<string> PlayerName { get; }
    public PersistentField<int> Level { get; }
    public PersistentField<long> Gold { get; }

    public PlayerProfilePackage() : base("player_profile", SaveStrategy.Immediate)
    {
        PlayerName = new PersistentField<string>(this, "Player");
        Level = new PersistentField<int>(this, 1);
        Gold = new PersistentField<long>(this, 0);
    }

    public override PlayerProfileDTO ExtractPayload() => new()
    {
        PlayerName = PlayerName.Value, Level = Level.Value, Gold = Gold.Value
    };

    public override void InjectPayload(PlayerProfileDTO data)
    {
        PlayerName.SetValueWithoutNotify(data.PlayerName);
        Level.SetValueWithoutNotify(data.Level);
        Gold.SetValueWithoutNotify(data.Gold);
    }
}
```

A package needs a public parameterless constructor to be listed in the DataPackage profile.

---

## PersistentField&lt;T&gt;

`PersistentField<T>` wraps a value in an R3 `ReactiveProperty`. Setting `Value` to a different value marks the owner
package dirty. Setting an equal value does nothing, and `SetValueWithoutNotify` never marks it dirty.

After changing a mutable value in place, for example adding to a list, call `NotifyChanged()`. It notifies the
subscribers and marks the package dirty.

```csharp
playerProfile.Gold.Observable
    .Subscribe(gold => goldText.text = gold.ToString("N0"))
    .AddTo(this);
```

---

## SaveSystem

### Pipeline

```
Save: Serialize → wrap {schemaVersion, payload} → Encrypt → Backend (atomic write)
Load: Backend → Decrypt → unwrap → Migrate → Deserialize
```

### ISaveService

```csharp
public interface ISaveService
{
    UniTask SaveAsync<T>(string key, T data, CancellationToken ct = default);          // throws on failure
    UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, CancellationToken ct = default);
    UniTask<T> LoadAsync<T>(string key, T defaultValue, CancellationToken ct = default); // default on any failure
    UniTask<bool> ExistsAsync(string key, CancellationToken ct = default);
    UniTask DeleteAsync(string key, CancellationToken ct = default);
}

public interface ISyncSaveService { bool TrySaveImmediate<T>(string key, T data); }

// Data with its own schema (see Migration)
public interface IVersionedSaveService
{
    UniTask SaveAsync<T>(string key, T data, SaveSchema schema, CancellationToken ct = default);
    UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, SaveSchema schema, CancellationToken ct = default);
    bool TrySaveImmediate<T>(string key, T data, SaveSchema schema);
}

public interface ISaveManager : ISaveService, ISyncSaveService, IVersionedSaveService, ICommonManager { }
```

Prefer `TryLoadAsync` whenever you will save back to the same key. Its result tells you whether that is safe.
`DeleteAsync` also removes the key's `.corrupt` backup.

---

## Load Safety

`TryLoadAsync` returns a `SaveLoadResult<T>` with one of the following statuses:

| Status | Meaning | `CanOverwrite` |
|--------|---------|----------------|
| `Loaded` | Data read, decoded and migrated | yes |
| `NotFound` | Nothing stored | yes |
| `Corrupted` | Could not be decrypted or deserialized; raw data copied to `key + ".corrupt"` first | yes (no if the backup failed) |
| `ReadFailed` | Storage could not be read (I/O error); data untouched | **no** |
| `UnsupportedVersion` | Written by a newer schema version (e.g. the game was downgraded) | **no** |
| `MigrationFailed` | A migrator is missing or failed | **no** |

When `CanOverwrite` is false, the package keeps working in memory and sets `CanSave` to false. It refuses every save
and logs it, so the stored data survives until the cause is fixed. A later successful `LoadAsync()` enables saving
again. Failures are logged and published as `LoadFailed` events.

---

## Save Strategies

```csharp
public enum SaveStrategy
{
    Immediate,  // save on every change (sensitive data: IAP, currency)
    Debounced,  // save at most every DebounceSeconds (first change starts the timer); flushed on pause / quit
    ManualOnly, // only when SaveNow / SaveAll is called (checkpoints)
    OnAppClose  // only on pause / quit / focus loss (settings)
}
```

`Debounced` is a throttle, not a "wait for quiet" debounce. Data that keeps changing is still saved every
`DebounceSeconds`, which defaults to 3 seconds. Override the `DebounceSeconds` property to change it.

---

## Encryption

```csharp
public interface IEncryptor
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText); // must throw on data it cannot decode
}
```

| Encryptor | Description |
|-----------|-------------|
| `XorEncryptor` | XOR + Base64 obfuscation. This is the default. |
| `Base64Encryptor`, `Rot13Encryptor` | Encoding only. |
| `NoOpEncryptor` | Plain text (debugging). |
| `AesEncryptor` | AES-256-CBC with a random IV per save and an HMAC-SHA256 tag, so modified saves are rejected. It still reads saves in the previous format (fixed IV, no tag). |
| `DefaultAesEncryptor` | `AesEncryptor` with a public built-in password. Subclass `AesEncryptor` with your own password and salt (8+ bytes) before shipping. |
| `UnityObfuscationEncryptor` | Device-bound XOR. ⚠️ `deviceUniqueIdentifier` can change (OS reset, or on iOS when every app of the vendor is uninstalled), and the save then becomes unreadable. |
| `CompositeEncryptor` | Chains encryptors, e.g. `new CompositeEncryptor(new XorEncryptor(), new DefaultAesEncryptor())`. |

The key always ships inside the game build, so treat every encryptor as obfuscation against casual save editing.
None of them protects server-trusted data.

Large saves are encrypted and decrypted on a worker thread when the encryptor implements `IThreadSafeEncryptor`. That
means no Unity API calls and no shared mutable state. All built-in encryptors implement it, and a `CompositeEncryptor`
qualifies when all its encryptors do.

The threshold is `SaveService.ThreadPoolEncryptionThreshold`, 64K characters by default. Serialization stays on the
calling thread, because the payload may reference live game state. WebGL always stays on the main thread.

---

## Migration

Every save stores the schema version it was written with. When the current version is higher, the save goes through
the migration chain before it is deserialized.

### Per-package schema (recommended)

The service-wide version in the profile is shared by every key. Bumping it for one package would need a migrator for
all the others, and a migrator cannot tell which package's payload it receives. Instead, give the package that changes
format its own schema:

```csharp
public class PlayerProfilePackage : PersistencePackage<PlayerProfileDTO>
{
    protected override SaveSchema CreateSchema() => new(2, new RenameGoldToCoins());
}

public sealed class RenameGoldToCoins : JsonSaveMigrator // edits the payload as a JObject
{
    public RenameGoldToCoins() : base(1, 2) { }

    protected override void Migrate(JObject data)
    {
        data["Coins"] = data["Gold"];
        data.Remove("Gold");
    }
}
```

The package then saves and loads through `IVersionedSaveService` with that schema; other packages are not affected.
Saves written before the package had a schema carry the service-wide version (1 by default), so start the package's
own version there.

### Service-wide schema

Set `schemaVersion` and `migrators` in the profile (or `WithSchemaVersion` / `WithMigrator`). They apply to every key
saved without its own schema.

```csharp
[Serializable] // + public parameterless constructor to be selectable in the profile
public class V1ToV2Migrator : ISaveMigrator
{
    public int FromVersion => 1;
    public int ToVersion => 2;   // may skip versions (e.g. 1 → 3)

    public string Migrate(string rawPayload)
    {
        var data = JObject.Parse(rawPayload);
        data["gems"] = 0;
        return data.ToString(Formatting.None);
    }
}
```

### Rules

- **Every step needs a migrator.** A missing step is reported as `MigrationFailed` and the save is left untouched.
  It is no longer silently passed through. If the format did not change, register `new DefaultSaveMigrator(1, 2)`.
- Two migrators cannot start at the same version.
- A save written with a newer version than the current one is reported as `UnsupportedVersion`.

```csharp
var chain = new SaveMigrationChain();
chain.AddMigrator(new V1ToV2Migrator());
chain.AddMigrator(new DefaultSaveMigrator(2, 3));
var migrated = chain.Migrate(rawPayload, currentVersion: 1, targetVersion: 3);
```

---

## Backends

```csharp
public interface ISaveBackend
{
    UniTask SaveRawAsync(string key, string data, CancellationToken ct = default);
    UniTask<string> LoadRawAsync(string key, CancellationToken ct = default); // null ONLY when missing; throws on I/O errors
    UniTask<bool> ExistsAsync(string key, CancellationToken ct = default);
    UniTask DeleteAsync(string key, CancellationToken ct = default);
}

public interface ISyncSaveBackend { void SaveRaw(string key, string data); } // for pause / quit flushes
```

| Backend | Description |
|---------|-------------|
| `PlayerPrefsSaveBackend` | This is the default, the same one shipped builds have always used. It suits small saves only: data goes to the registry on Windows, and WebGL limits it to about 1 MB. `PlayerPrefs.Save()` rewrites every key, so async writes made in the same frame share one flush at the end of the frame. Each write still completes only once its data is on disk. Synchronous writes, used on pause and quit, flush immediately. |
| `JsonFileSaveBackend` | One file per key in `persistentDataPath`. It writes to a temporary file and then swaps it in, so a crash never leaves a truncated save. A key with characters that are invalid in file names gets a hash suffix (`a/b` → `a_b-<hash>.json`), so it cannot collide with another key. Files written under the old name are still read. |

---

## Configuration

`SaveServiceManager` builds the service from the following sources, highest priority first:

1. **Code**: `WithBackend`, `WithSerializer`, `WithEncryptor`, `WithEventPublisher`, `WithSchemaVersion` and
   `WithMigrator`. These can be called at any time, and an initialized manager rebuilds its service immediately.
   Passing null restores the profile's value.
2. **Profile**: `Resources/SaveServiceAdapterProfile`, edited under *Settings → Save System*. It holds the adapters,
   the schema version and the migrators.
3. **Defaults**: `PlayerPrefsSaveBackend`, `NewtonsoftJsonSerializer`, `XorEncryptor` and schema version 1. These are
   the same in the Editor and in builds.

```csharp
SaveServiceManager.Instance
    .WithBackend(new JsonFileSaveBackend())
    .WithSchemaVersion(2)
    .WithMigrator(new DefaultSaveMigrator(1, 2));
```

---

## Events

`SaveEvent` is published through MessagePipe when the manager is created by the DI root scope.

```csharp
[MessagePipeEvent]
public class SaveEvent
{
    public string Key { get; set; }
    public string DataType { get; set; }
    public SaveEventType EventType { get; set; } // SaveSuccess, SaveFailed, LoadSuccess, LoadFailed
    public string Error { get; set; }            // e.g. "Corrupted: ... (raw data backed up to 'key.corrupt')"
}

subscriber.Subscribe(e =>
{
    if (e.EventType is SaveEventType.SaveFailed or SaveEventType.LoadFailed)
        Debug.LogError($"{e.EventType} {e.Key}: {e.Error}");
});
```

A subscriber that throws is logged. It does not change the result of the save or the load.

---

## Architecture Overview

```
DataPersistenceManager (bootstrap)
 ├─ packages ── PersistentField (R3)      ── strategy pipeline (Immediate / Debounced / …)
 ├─ SaveLifecycleHook (focus / pause / quit flush)
 └─ ISaveManager = SaveServiceManager (bootstrap)
      └─ SaveService: Serializer → Encryptor → Backend, SaveMigrationChain, ISaveEventPublisher
```
