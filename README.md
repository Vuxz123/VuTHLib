# VuTH Lib

Modular Unity 6 framework (`com.vuth.lib`): VContainer bootstrap, MessagePipe integration, Screen / ScreenFlow,
Window & transitions, Pool, Audio, Camera, Save System (encrypt + migrate) and DataPackage persistence.

The package root is **`Assets/_VuTH`** (it contains `package.json`). Everything else in this repo is the
development / sample project.

---

## Install

### 1. Prerequisites

UPM packages cannot declare git or NuGet dependencies, so add these to the consuming project first.

`Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#73a63b7f672b88f7e9992f6917eb458a8cbb6fa9",
    "com.cysharp.r3": "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#24d04125d68719816e24dfb79c60dd97ca396038",
    "com.cysharp.zlinq": "https://github.com/Cysharp/ZLinq.git?path=src/ZLinq.Unity/Assets/ZLinq.Unity#c6d15be4efecc0afca628fd194f8f62ee0b4bb15",
    "com.cysharp.zstring": "https://github.com/Cysharp/ZString.git?path=src/ZString.Unity/Assets/Scripts/ZString#9fae2bebcf8768398905aba0e64a25f2384b4556",
    "com.cysharp.messagepipe": "https://github.com/Cysharp/MessagePipe.git?path=src/MessagePipe.Unity/Assets/Plugins/MessagePipe#2b49ed40436b89e023df4cc0d39db67de2c78735",
    "com.cysharp.messagepipe.vcontainer": "https://github.com/Cysharp/MessagePipe.git?path=src/MessagePipe.Unity/Assets/Plugins/MessagePipe.VContainer#94ec0872e4f9addcddc05ef4db2286b6691e82b5",
    "jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0",
    "com.github-glitchenzo.nugetforunity": "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity#34432703a177c97631a4eb1d908f086037c67969",
    "com.kyrylokuzyk.primetween": "1.3.8"
  },
  "scopedRegistries": [
    { "name": "npm", "url": "https://registry.npmjs.org", "scopes": ["com.kyrylokuzyk"] }
  ]
}
```

NuGet (install with NuGetForUnity — see this repo's `Assets/packages.config` for the exact versions):
`R3`, `ZLinq`, `ZLinq.Json`, `ObservableCollections`, `ObservableCollections.R3`, `Newtonsoft.Json`, `System.Text.Json`.

### 2. The package

Git URL (pin a tag):

```json
"com.vuth.lib": "https://github.com/Vuxz123/VuTHLib.git?path=Assets/_VuTH#v1.0.0"
```

Or download `com.vuth.lib-<version>.tgz` from GitHub Releases → Package Manager → **+** → **Add package from tarball…**

---

## Release

1. Bump `"version"` in `Assets/_VuTH/package.json`.
2. Commit, then tag and push — the tag must match the version (pre-release suffixes like `-beta` are allowed):
   ```bash
   git tag v1.0.1
   git push origin master v1.0.1
   ```
3. `.github/workflows/release.yml` checks the tag against `package.json`, checks that every script is covered by an
   `.asmdef` (scripts outside one are silently not compiled in a UPM package), builds the tarball and creates the
   GitHub Release.

`.unitypackage` builds are no longer committed (they are git-ignored). For a one-off `.unitypackage`, use
**Window → VuTH → Export Package** (`Assets/Editor/PackageExporter.cs`, dev project only).

---

## Upgrading from the pre-1.0 names

Assemblies and namespaces were renamed:

| Old | New |
|---|---|
| `VuTH.Persistant.*`, `VuTH..Persistant.SaveSystem.Editor` | `VuTH.Persistent.*` |
| `_VuTH.Core.Persistant.*` | `_VuTH.Core.Persistent.*` |
| `_VuTH.Core.Booststrap`, `VuTH.Boostrap.Editor`, `Boostrap*` types | `_VuTH.Core.Bootstrap`, `VuTH.Bootstrap.Editor`, `Bootstrap*` |
| `_VuTH.Core.GameCycle.Screen.Core.A` | `_VuTH.Core.GameCycle.Screen.Core` |
| `VuTH.Common.MessagePipe` | `_VuTH.Common.MessagePipe` |
| `DragableGrid` / `ImGuiDragable*` | `DraggableGrid` / `ImGuiDraggable*` |

What is handled automatically:
- `[SerializeReference]` data (encryptors, backends, serializers, migrators) — the classes carry `[MovedFrom]`.
- Type names stored as strings (DataPackage profile) — resolved through `LegacyTypeResolver`, so old assets keep
  loading.

What you must do in a consuming project:
- Update `using` directives and any `.asmdef` that references the old assembly names by **name**
  (GUID references keep working).
- Re-generate `ScreenIds` from its settings tab. Delete any generated `MessagePipeRegistrar.cs`: MessagePipe no longer
  needs a bake step (see `Assets/VuTH_Docs/vuth-common-docs.md`, section 4).
- The built-in screen asset and trigger were renamed `Boostrap` → `Bootstrap` (`BoostrapCompleted` → `BootstrapCompleted`).
