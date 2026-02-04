# SaveSystem Test Plan

This document describes the test strategy for the VuTH SaveSystem.

## Test Structure

Tests are organized into three Unity Test Framework assemblies:

```
Assets/_VuTH/Core/Persistant/SaveSystem/Tests/
├── Runtime/                    # Pure C# unit tests (no Unity dependencies)
│   ├── VuTH.Persistant.SaveSystem.Tests.asmdef
│   ├── SaveServiceTests.cs
│   ├── SaveMigrationChainTests.cs
│   └── TestDoubles.cs
├── EditMode/                   # EditMode integration tests
│   ├── VuTH.Persistant.SaveSystem.Tests.EditMode.asmdef
│   └── JsonFileBackendIntegrationTests.cs
├── PlayMode/                   # PlayMode end-to-end tests
│   ├── VuTH.Persistant.SaveSystem.Tests.PlayMode.asmdef
│   └── SaveServiceManagerPlayModeTests.cs
└── Editor/                     # Editor-only tests
    └── SaveAdapterTypeScannerTests.cs
```

## Test Suites

### 1. Unit Tests (Runtime)

**Location:** `Tests/Runtime/`

**Coverage:**
- `SaveServiceTests` - Core pipeline (SaveAsync, LoadAsync, ExistsAsync, DeleteAsync)
- `SaveMigrationChainTests` - Migration logic and fallback behavior
- `TestDoubles` - InMemorySaveBackend, PassThroughEncryptor, FailingEncryptor, DeterministicSerializer, SpySaveEventPublisher

**Run Command:**
```bash
# In Unity: Window > General > Test Runner > Run All (Runtime)
```

### 2. EditMode Integration Tests

**Location:** `Tests/EditMode/`

**Coverage:**
- `JsonFileBackendIntegrationTests` - Real file I/O with temp directories
- `XorEncryptorTests` - Encryption round-trips
- `NewtonsoftJsonSerializerTests` - JSON serialization edge cases

**Cleanup Rules:**
- All tests use `Path.GetTempPath()` for file operations
- `TearDown` deletes temporary directories
- No file cleanup required from test runner

**Run Command:**
```bash
# In Unity: Window > General > Test Runner > Run All (EditMode)
```

### 3. PlayMode Tests

**Location:** `Tests/PlayMode/`

**Coverage:**
- `SaveServiceManagerPlayModeTests` - Manager lifecycle, scene integration
- Default config path vs profile path testing

**Important Notes:**
- Tests create temporary GameObjects and clean up in `TearDown`
- Avoid tests that require `Resources.Load` (not available in some test contexts)

**Run Command:**
```bash
# In Unity: Window > General > Test Runner > Run All (PlayMode)
```

### 4. Editor Tests

**Location:** `Tests/Editor/` or root `Tests/` folder

**Coverage:**
- `SaveAdapterTypeScannerTests` - Type discovery and filtering
- Validates `[Serializable]` attribute requirement
- Validates empty constructor requirement

**Run Command:**
```bash
# In Unity: Window > General > Test Runner > Editor tests
```

## Adapter Validation Rules

All adapters must satisfy these rules (enforced by `SaveAdapterTypeScanner`):

1. **Must have `[Serializable]` attribute**
2. **Must have public parameterless constructor**

### Testing Adapter Compliance

```csharp
// Example test for adapter validation
[Test]
public void JsonFileSaveBackend_HasRequiredAttributes()
{
    Assert.IsTrue(Attribute.IsDefined(typeof(JsonFileSaveBackend),
        typeof(SerializableAttribute)), "Must have [Serializable]");

    Assert.IsNotNull(typeof(JsonFileSaveBackend).GetConstructor(Type.EmptyTypes),
        "Must have public parameterless constructor");
}
```

## Running Tests

### From Unity Editor

1. Open **Window > General > Test Runner**
2. Select **Run All** for the appropriate category (EditMode, PlayMode, Editor)
3. Filter by suite using the search box

### From Command Line (optional)

```bash
# Batch mode (requires Unity Pro)
Unity -batchmode -quit -executeMethod UnityEditor.TestRunner.TestRunnerApi.Run -projectPath .
```

## CI/CD Considerations

- All tests should pass before merge
- PlayMode tests may timeout in CI; consider running only EditMode tests
- Use `-testSettingsFile` to configure test behavior in CI

## Debugging Tips

1. **Debug Logging:** Use `TestContext.Progress.WriteLine()` for output
2. **Timeouts:** Increase timeout for async tests if needed: `[Timeout(10000)]`
3. **Log Asserts:** Use `LogAssert.Expect()` for expected log messages
4. **Scene Isolation:** Each test should be independent; avoid shared state
