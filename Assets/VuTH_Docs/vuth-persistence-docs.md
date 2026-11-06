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
5. [Generated Payload Mapping](#generated-payload-mapping)
6. [PersistentField](#persistentfield)
7. [SaveSystem](#savesystem)
8. [Load Safety](#load-safety)
9. [Save Strategies](#save-strategies)
10. [Encryption](#encryption)
11. [Migration](#migration)
12. [Backends](#backends)
13. [Configuration](#configuration)
14. [Events](#events)

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

    event Action<IPersistencePackage> OnSaveBlocked;             // a package's save could not be read: tell the player
    int PackageCount { get; }
}
```

Packages created from the DataPackage profile are made by `Activator`, not by the container. With VContainer their
`[Inject]` members are injected once the root scope is built.

If the manager cannot start (no save service), `WaitUntilReadyAsync()` and the packages' `WaitUntilLoadedAsync()`
throw instead of waiting forever.

The SaveServiceManager must initialize first, so give it a lower `BootstrapOrder`. The defaults are 0 for
SaveService and 100 for DataPersistence.

### Lifecycle Hook

`SaveLifecycleHook` flushes dirty `Debounced` and `OnAppClose` packages. With VContainer it is registered as an
entry point. Without VContainer, or when the manager is not created through the root scope, the manager creates and
disposes its own, so these packages are flushed in every setup:

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
- `WaitUntilLoadedAsync()` after a switch waits for the new slot's data, never for the previous load.
- Switches run one after another. The cancellation token is only honored before the switch starts. Once started,
  every slot-scoped package moves, so they never end up split between two slots.
- A change made while a package is being saved before the switch still goes to the slot being left.
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
    event Action<IPersistencePackage> SaveBlocked; // raised when a load leaves CanSave false
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
`InjectPayload` runs without marking the package dirty.

**Changes during a load.** A change made while the data is still loading is replaced by the loaded data. This is
real data loss, so it is logged as an error. When a change can happen at any time, for example a purchase restored
at startup, use `Modify`, which applies it right after the load. Otherwise await `WaitUntilLoadedAsync()` first.

```csharp
profile.Modify(() => profile.Gold.Value += restoredGold);
```

**Batches.** `BatchChanges()` groups several changes into one. The package is marked dirty once, when the scope
ends, so `Immediate` never saves a state with only some of the changes applied:

```csharp
using (profile.BatchChanges())
{
    profile.Exp.Value -= cost;
    profile.Level.Value++;
}
```

A batch is not a transaction. If the block throws, the changes made before the exception stay in memory and are
saved when the scope ends. Validate first, for example with `TrySetValue`, then make the changes.

**Save blocked.** When a load leaves `CanSave` false, the package raises `SaveBlocked` and the manager raises
`OnSaveBlocked`. Tell the player, for example "your save could not be loaded, contact support". Otherwise the changes
pile up in memory and are lost when the app closes.

The loads, saves and deletes of one package run one after another, never interleaved. A load waits for a save that is
still being written, and two loads never mix.

Override `CreateSchema()` to version the package's data on its own (see [Migration](#migration)).

### Example: PlayerProfile Package

```csharp
[Serializable]
public class PlayerProfileDTO
{
    public string PlayerName = "Player";
    [Min(1), RejectOutOfRange] public int Level = 1;
    [Min(0), RejectOutOfRange] public long Gold;
}

[GeneratePersistentFields] // fields + ExtractPayload / InjectPayload generated from the DTO (see below)
public partial class PlayerProfilePackage : PersistencePackage<PlayerProfileDTO>
{
    public PlayerProfilePackage() : base("player_profile", SaveStrategy.Immediate) { }

    // Gold: a generated RangedPersistentField<long> that refuses to go below 0.
    public bool TrySpend(long price) => Gold.TrySetValue(Gold.Value - price);
}
```

A package needs a public parameterless constructor to be listed in the DataPackage profile. Its `PersistentField`s
register themselves and are disposed with the package, so you do not need to override `Dispose` for them.

---

## Generated Payload Mapping

A source generator writes the boilerplate of a `partial` package. Two attributes select how much:

| Attribute | Generated |
|-----------|-----------|
| `[GeneratePayloadMapping]` | `ExtractPayload` / `InjectPayload` for the `PersistentField`s you declare |
| `[GeneratePersistentFields]` | The same, plus a field for every DTO member you do not declare |

With `[GeneratePersistentFields]`, the DTO is the single source: adding a saved value only takes a DTO member.

```csharp
[Serializable]
public sealed class AudioSettingsPayload
{
    public bool muted;
    [Range(0f, 1f)] public float masterVolume = 1f;
}

[GeneratePersistentFields]
public sealed partial class AudioSettingsPackage : PersistencePackage<AudioSettingsPayload>
{
    public AudioSettingsPackage() : base("audio_settings", SaveStrategy.Debounced) { }
}

// Generated: PersistentField<bool> Muted, RangedPersistentField<float> MasterVolume (0..1),
// ExtractPayload, InjectPayload.
```

### Generated fields

- **Name:** the member name in PascalCase: `masterVolume` → `MasterVolume`, `m_Gold` / `_gold` → `Gold`.
- **Default value:** the member's value in a new DTO.
- **Creation:** on first use, so the package needs no constructor code. To start from other values, call
  `SetValueWithoutMarkingDirty` in the constructor, as `AudioSettingsPackage` does.
- **Declared fields win:** a field you declare yourself is kept and only mapped. Do that when a field needs a custom
  type or logic.

### Unity attributes

The generator reads Unity's attributes on the DTO members:

| Attribute | Effect |
|-----------|--------|
| `[Range(min, max)]` | Generated field: `RangedPersistentField<T>` with the range min..max (any numeric `T`); `Min` / `Max` can drive a slider |
| `[Min(min)]` | Generated field: `RangedPersistentField<T>` from min to `T.MaxValue` |
| `[RejectOutOfRange]` (VuTH) | With `[Range]` / `[Min]`: refuse out-of-range values instead of clamping them (see below) |
| `[ColorUsage(showAlpha, hdr)]` | Generated field: `ColorPersistentField`. Without alpha the alpha is 1; without HDR the channels stay within 0..1 |
| `[NonSerialized]` | The member is never saved, so it gets no field. A declared field mapped to it is a warning |

- On a field you declare as a plain `PersistentField<T>`, the generated `InjectPayload` still applies `[Range]`, `[Min]`
  and `[ColorUsage]` to the loaded value. Only a ranged or color field also constrains the values the game sets.

  If you declare a ranged or color field yourself on a member that also has a constraint, both apply and only values
  inside both ranges are kept (VUTHP010). Keep one of the two.

**Clamp or reject?** By default a ranged field **clamps** the values the game sets. That suits settings: a volume of
1.2 becomes 1. It is wrong for currencies and counters, because it hides logic errors: spending 100 gold with 50 would
silently leave 0, and the purchase would go through.

Add `[RejectOutOfRange]` to such members. The field then **refuses** an out-of-range value: it keeps its current
value, logs an error, and `TrySetValue` returns false, so check it before granting a purchase. Loaded data is still
clamped in both modes, so a damaged save is repaired rather than lost.

### Your own field attributes

Two interfaces let your own attributes on DTO members shape the generated fields. The generator writes
`new YourAttribute(args)` into the package, so there is no reflection at runtime, and it is safe with IL2CPP and code
stripping:

```csharp
// A normalizer: constrains the value; several apply in declaration order, after Range / Min / ColorUsage.
public sealed class StepAttribute : Attribute, IPersistentFieldNormalizer<float>
{
    private readonly float _step;
    public StepAttribute(float step) => _step = step;
    public float Normalize(float value) => Mathf.Round(value / _step) * _step;
}

// A factory: chooses the field type; the generated property has that type, with its own API.
public sealed class CurrencyAttribute : Attribute, IPersistentFieldFactory<long, CurrencyField>
{
    public CurrencyField Create(IPersistencePackage owner, long defaultValue) => new CurrencyField(owner, defaultValue);
}

public class SettingsDto { [Range(0f, 1f), Step(0.05f)] public float volume = 1f; }
public class WalletDto { [Currency] public long gems; }
```

**Rules:**

- The interface's `T` must be the member's type (VUTHP011).
- A member has at most one factory (VUTHP012). A factory builds the whole field, so the member's other constraints and
  normalizers are ignored (VUTHP013).
- The attribute and its constructor must be accessible from the package (VUTHP014). Its arguments are constants (a
  C# rule), so they can always be written back into code, named arguments included.
- On a declared field, normalizers apply to the loaded value, and factories are not used.
- `Normalize` must be pure. `Create` must only construct the field: no subscription, no service lookup.

To expose a type other than the saved one, for example a stored string shown as an enum, declare the field yourself.
Converters between stored and exposed types are not supported.
- The serializer itself follows the remaining serialization attributes:
  - `[FormerlySerializedAs("old")]`: rename a DTO member without losing old saves;
  - `[SerializeField]` private fields are saved.
- Display-only attributes (`[Tooltip]`, `[Header]`, `[Space]`, `[Multiline]`, `[TextArea]`, `[Delayed]`,
  `[HideInInspector]`, `[InspectorName]`) do not change saved data and are ignored.

### Mapping rules

- **Name:** exact name first, then a unique case-insensitive match, so `MasterVolume` maps to `masterVolume`.
- **Rename:** `[PayloadMember("coins")]` maps a declared field to another DTO member name.
- **Exclude:** `[PayloadIgnore]` leaves a declared field out, for example session-only state.
- **Members:** the DTO member may be a field or a property, also inherited. Its type must be exactly `T`.
- **Generated code:**
  - `InjectPayload` uses `SetValueWithoutMarkingDirty` inside `LoadWithoutNotify`, so loading never marks the package
    dirty.
  - If you write one of the two methods yourself, only the other one is generated.

### Diagnostics

Problems show up at compile time:

| Id | Severity | Problem |
|----|----------|---------|
| VUTHP001 | error | The class does not derive from `PersistencePackage<TData>` |
| VUTHP002 | error | The package, or a class containing it, is not `partial` |
| VUTHP003 | warning | A declared field has no DTO member of that name, so it is not saved |
| VUTHP004 | error | The DTO member has another type |
| VUTHP005 | error | The DTO has no parameterless constructor |
| VUTHP006 | warning | The DTO member is read-only or not accessible |
| VUTHP007 | warning | A declared field maps to a `[NonSerialized]` member, so it is not saved |
| VUTHP008 | warning | The name of a field to generate is already a member of the package |
| VUTHP009 | warning | `[Range]` / `[Min]` / `[ColorUsage]` is on a type it cannot constrain |
| VUTHP010 | warning | A declared ranged / color field also gets the DTO member's constraint (values must satisfy both) |
| VUTHP011 | error | A normalizer / factory attribute works on another type than the member's |
| VUTHP012 | error | A member has several field factories |
| VUTHP013 | warning | A field factory ignores the member's constraints / normalizers |
| VUTHP014 | error | A normalizer / factory attribute cannot be instantiated from the package (not accessible, abstract) |
| VUTHP015 | warning | `[RejectOutOfRange]` on a member without `[Range]` / `[Min]` |

The generator is incremental: it keeps no symbol between steps, so editing unrelated code does not regenerate the
packages.

The generator works in `VuTH.Persistent.DataPackage` and in the assemblies that reference it. Its source and its tests
are in `Tools/SourceGenerators` (see the README there).

---

## PersistentField&lt;T&gt;

`PersistentField<T>` wraps a value in an R3 `ReactiveProperty`. Setting `Value` to a different value marks the owner
package dirty, and setting an equal value does nothing.

`SetValueWithoutMarkingDirty` sets the value without saving, for example in `InjectPayload`. Subscribers are still
notified, so the UI shows the loaded value. The old name, `SetValueWithoutNotify`, is obsolete because it was
misleading.

After changing a mutable value in place, for example adding to a list, call `NotifyChanged()`. It notifies the
subscribers and marks the package dirty.

**Constrained fields.** `RangedPersistentField<T>` (min..max, `RangeMode.Clamp` or `Reject`) and `ColorPersistentField`
(`[ColorUsage]` rules) are the field types generated from attributes; they can also be created by hand.

**Refusing values.** `TrySetValue(value)` returns false when the field refuses a value, for example a
`RangeMode.Reject` field going out of range; setting `Value` does the same without reporting it. A subclass decides by
overriding `AcceptsValue`. Only values set by the game are checked: the default and loaded data are normalized instead.
A custom `IEqualityComparer<T>` (constructor argument) decides what counts as a change.

A field created after its package was disposed is disposed at once.

**Normalizer.** Any field can normalize every value it receives: the default, `Value`, and loaded data. A damaged or
edited save then cannot put it out of range, and a generated `InjectPayload` needs no special code:

```csharp
MasterVolume = new PersistentField<float>(this, 1f, normalize: Mathf.Clamp01);
MasterVolume.Value = 3f; // 1
```

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

`Debounced` runs on Unity's update loop in unscaled time (`UnityTimeProvider.UpdateIgnoreTimeScale`), so a paused
game still saves. It is a throttle, not a "wait for quiet" debounce. Data that keeps changing is still saved every
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
| `JsonFileSaveBackend` | One file per key in `persistentDataPath`. It writes to a temporary file and then swaps it in, so a crash never leaves a truncated save. A key with characters that are invalid in file names gets a hash suffix (`a/b` → `a_b-<hash>.json`), so it cannot collide with another key. Files written under the old name are still read. Each write takes a ticket when it starts. An async write that finishes after a newer synchronous flush, for example a focus-lost save racing the pause flush, is dropped instead of replacing the newer data. |

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
