# Migration Plan: System.Text.Json to Newtonsoft.Json

## Overview

Migration of save system serialization from [`System.Text.Json`](Assets/_VuTH/Core/Persistant/SaveSystem/Serialize/JsonSerializer.cs:1) to [`Newtonsoft.Json`](Assets/_VuTH/Core/Persistant/SaveSystem/JsonSerializer.cs:1) for the VuTH persistence layer.

---

## Current State Analysis

### Active Serializer (System.Text.Json)
**Location:** [`Assets/_VuTH/Core/Persistant/SaveSystem/Serialize/JsonSerializer.cs`](Assets/_VuTH/Core/Persistant/SaveSystem/Serialize/JsonSerializer.cs:1)

**Current Settings:**
| Setting | Value |
|---------|-------|
| WriteIndented | false |
| PropertyNamingPolicy | CamelCase |
| PropertyNameCaseInsensitive | true |
| IgnoreReadOnlyProperties | false |
| IncludeFields | true |

### Legacy Serializer (Newtonsoft.Json - Not Registered)
**Location:** [`Assets/_VuTH/Core/Persistant/SaveSystem/JsonSerializer.cs`](Assets/_VuTH/Core/Persistant/SaveSystem/JsonSerializer.cs:1)

**Current Settings:**
| Setting | Value |
|---------|-------|
| Formatting | None |
| DateFormatString | `yyyy-MM-ddTHH:mm:ss.fffZ` |

### Save Pipeline Flow
```
Serialize<T> → SavePayloadWrapper (schema version + payload) → Encrypt → Backend (PlayerPrefs/File)
```

### DI Registration
**Location:** [`SaveServiceVContainerConfigurator.cs`](Assets/_VuTH/Core/Persistant/SaveSystem/SaveServiceVContainerConfigurator.cs:35-36)

```csharp
builder.Register<JsonSerializer>(Lifetime.Singleton)
    .As<ISerializer>();
```

---

## Step 1: Package Dependency Addition

### Action Items:
1. **Add Newtonsoft.Json Unity package** to [`VuTH.Core.Persistant.asmdef`](Assets/_VuTH/Core/Persistant/SaveSystem/VuTH.Core.Persistant.asmdef:1)
2. **Add reference** in Assembly Definition References section:
   ```
   "Newtonsoft.Json"
   ```
3. **Alternative:** Use Unity Package Manager manifest to add:
   ```json
   "com.unity.nnewtonsoft.json": "3.2.1"
   ```

---

## Step 2: Update Newtonsoft.Json Settings

### Target Settings (Match System.Text.Json Behavior)

**File:** [`Assets/_VuTH/Core/Persistant/SaveSystem/JsonSerializer.cs`](Assets/_VuTH/Core/Persistant/SaveSystem/JsonSerializer.cs:1)

```csharp
new JsonSerializerSettings
{
    // Output formatting
    Formatting = Formatting.None,
    
    // Date format
    DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",
    
    // CamelCase naming policy
    ContractResolver = new CamelCasePropertyNamesContractResolver(),
    
    // Case-insensitive property matching (default in Newtonsoft)
    // This is the default behavior, explicit for clarity
    
    // Include fields
    DefaultMemberHandling = MemberHandling.Include,
    
    // Don't ignore read-only properties
    PropertyHandling = PropertyHandling.Default,
    
    // Include null values (matches System.Text.Json default)
    NullValueHandling = NullValueHandling.Include,
    
    // Reference loop handling (safeguard)
    ReferenceLoopHandling = ReferenceLoopHandling.Error
}
```

### Required Using Statements:
```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
```

---

## Step 3: DI Registration Changes

### Option A: Update Existing Registration
**File:** [`SaveServiceVContainerConfigurator.cs`](Assets/_VuTH/Core/Persistant/SaveSystem/SaveServiceVContainerConfigurator.cs:34-36)

**Change:**
```csharp
// OLD (System.Text.Json)
builder.Register<Serialize.JsonSerializer>(Lifetime.Singleton)
    .As<ISerializer>();

// NEW (Newtonsoft.Json) - Update namespace
builder.Register<JsonSerializer>(Lifetime.Singleton)
    .As<ISerializer>();
```

### DI Registration: Option B (Approved)
**Create:** [`Assets/_VuTH/Core/Persistant/SaveSystem/Serialize/NewtonsoftJsonSerializer.cs`](Assets/_VuTH/Core/Persistant/SaveSystem/Serialize/NewtonsoftJsonSerializer.cs:1)

```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace _VuTH.Core.Persistant.SaveSystem.Serialize
{
    public class NewtonsoftJsonSerializer : ISerializer
    {
        private readonly JsonSerializerSettings _settings;

        public NewtonsoftJsonSerializer()
        {
            _settings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DefaultMemberHandling = MemberHandling.Include,
                PropertyHandling = PropertyHandling.Default,
                NullValueHandling = NullValueHandling.Include,
                ReferenceLoopHandling = ReferenceLoopHandling.Error
            };
        }

        public string Serialize<T>(T data)
        {
            return JsonConvert.SerializeObject(data, _settings);
        }

        public T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, _settings)!;
        }
    }
}
```

**DI Registration:**
```csharp
builder.Register<Serialize.NewtonsoftJsonSerializer>(Lifetime.Singleton)
    .As<ISerializer>();
```

**Recommendation:** Use Option B for clarity and separation of concerns. ✅ APPROVED

---

## Step 4: Backward Compatibility Strategy

### Problem:
Existing saves are serialized with System.Text.Json format. Migration must handle:
- Property naming (camelCase vs PascalCase)
- Date format differences
- Field inclusion

### Backward Compatibility: Schema Version Bump + Fallback (Approved)
**Strategy:**
**File:** [`SavePayloadWrapper.cs`](Assets/_VuTH/Core/Persistant/SaveSystem/SavePayloadWrapper.cs:17)

```csharp
public SavePayloadWrapper()
{
    SchemaVersion = 2; // Bump from 1 to 2
    Payload = string.Empty;
}
```

**Load Logic:** [`SaveService.cs`](Assets/_VuTH/Core/Persistant/SaveSystem/SaveService.cs:97-105)
- If `SchemaVersion == 1`, use System.Text.Json for inner payload deserialization
- If `SchemaVersion >= 2`, use Newtonsoft.Json

#### Option B: Relaxed Casing (Immediate Migration)
Use [`JsonLoadSettings`](Assets/_VuTH/Core/Persistant/SaveSystem/JsonSerializer.cs:1) with `NullValueHandling.Ignore` and handle case-insensitivity at deserialize level:

```csharp
// In legacy load path
var settings = new JsonSerializerSettings
{
    // Allow reading camelCase into PascalCase properties
    // This is default behavior in Newtonsoft
};
```

#### Recommended Approach (Approved):
1. **Bump SchemaVersion to 2** when deploying Newtonsoft.Json migration
2. **Implement dual-deserialization fallback** for existing v1 saves
3. On first load of v1 save, migrate to v2 format

### Migration Implementation:

**File:** [`Assets/_VuTH/Core/Persistant/SaveSystem/SaveService.cs`](Assets/_VuTH/Core/Persistant/SaveSystem/SaveService.cs:94-108)

Add fallback logic in the Load method:

```csharp
// Step 4: Deserialize wrapper
var wrapper = _serializer.Deserialize<SavePayloadWrapper>(wrapperJson);

// Step 5: Migrate if needed
if (wrapper.SchemaVersion < _currentSchemaVersion)
{
    string migratedPayload = _migrationChain.Migrate(
        wrapper.Payload,
        wrapper.SchemaVersion,
        _currentSchemaVersion);
    
    // Fallback for v1 saves (System.Text.Json format)
    if (wrapper.SchemaVersion == 1)
    {
        // Try System.Text.Json as fallback for legacy saves
        try
        {
            var fallbackOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                IncludeFields = true
            };
            migratedPayload = System.Text.Json.JsonSerializer.Deserialize<string>(
                wrapper.Payload, fallbackOptions) ?? wrapper.Payload;
        }
        catch
        {
            // If fallback fails, keep original payload
        }
    }
    
    wrapper.Payload = migratedPayload;
    wrapper.SchemaVersion = _currentSchemaVersion;
}
```

---

## Step 5: Testing and Validation Checklist

### Unit Tests Required:
- [ ] **Serialization一致性**: Same input produces equivalent output (ignoring formatting)
- [ ] **反序列化一致性**: Deserialized objects match original values
- [ ] **CamelCase命名**: Property `PlayerName` serializes to `playerName`
- [ ] **大小写不敏感**: Deserialize `{ "playerName": "Test" }` into `PlayerName` property
- [ ] **字段包含**: Public fields are serialized
- [ ] **只读属性**: Read-only properties are NOT serialized (matching current behavior)
- [ ] **DateTime处理**: Dates serialize with `yyyy-MM-ddTHH:mm:ss.fffZ` format
- [ ] **空值处理**: Null values are included
- [ ] **复杂对象**: Nested objects, arrays, dictionaries serialize/deserialize correctly
- [ ] **向后兼容**: Legacy System.Text.Json saves can be loaded (if implementing fallback)

### Integration Tests:
- [ ] **Save/Load循环**: Full pipeline test with [`ExamplePlayerData.cs`](Assets/_VuTH/Core/Persistant/SaveSystem/Example/ExamplePlayerData.cs:1)
- [ ] **迁移路径**: Load v1 save, verify schema version bump
- [ ] **加密集成**: Verify encryption works with new JSON format
- [ ] **后端集成**: Test with both PlayerPrefs and JsonFile backends
- [ ] **事件发布**: Verify SaveSuccess/LoadSuccess events fire correctly

### Manual Testing:
- [ ] **新游戏流程**: Create new save, load it, verify data integrity
- [ ] **边界值测试**: Empty data, maximum data sizes
- [ ] **特殊字符**: Unicode characters, emojis in string fields
- [ ] **数值精度**: Float/double precision preservation
- [ ] **跨平台测试**: Test on target platforms (Windows, Android, iOS, etc.)

### Test Data Models to Use:
- [`ExamplePlayerData.cs`](Assets/_VuTH/Core/Persistant/SaveSystem/Example/ExamplePlayerData.cs:1) - Contains:
  - `string`, `int`, `float`, `DateTime`
  - `List<string>` collections
  - Multiple data classes for different scenarios

---

## Implementation Order

```
1. Add Newtonsoft.Json package dependency
2. Update/Update NewtonsoftJsonSerializer.cs with matching settings
3. Update DI registration in SaveServiceVContainerConfigurator.cs
4. Bump SchemaVersion in SavePayloadWrapper.cs
5. Implement backward compatibility fallback (optional)
6. Run unit tests
7. Run integration tests
8. Manual testing and validation
9. Deploy with clear existing saves option
```

---

## Files Modified Summary

| File | Action |
|------|--------|
| `VuTH.Core.Persistant.asmdef` | Add Newtonsoft.Json reference |
| `Serialize/NewtonsoftJsonSerializer.cs` | Create with matching settings |
| `SaveServiceVContainerConfigurator.cs` | Update DI registration |
| `SavePayloadWrapper.cs` | Bump SchemaVersion |
| `SaveService.cs` | Add backward compatibility fallback |
| `Serialize/JsonSerializer.cs` | Mark as obsolete/deprecated |

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Package conflict | Medium | Verify no other System.Text.Json usage |
| Breaking existing saves | High | Implement fallback or clear saves on update |
| Performance regression | Low | Newtonsoft.Json is generally faster/same |
| Date format change | Medium | Use consistent date format in settings |
| Field inclusion difference | High | Verify `DefaultMemberHandling.Include` is set |
