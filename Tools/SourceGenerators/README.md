# VuTH Source Generators

Roslyn source generators used by the Unity project. Unity cannot build them itself: they are built here with the .NET
SDK and the resulting DLL is committed into `Assets`.

## VuTH.Persistent.Generators

Generates the PersistentFields (from the DTO and its Unity attributes) and `ExtractPayload` / `InjectPayload` of persistence packages marked with `[GeneratePersistentFields]` or `[GeneratePayloadMapping]`. See
`Assets/VuTH_Docs/vuth-persistence-docs.md`, section *Generated Payload Mapping*.

- Targets `netstandard2.0` and **Microsoft.CodeAnalysis.CSharp 4.3.0**: the Roslyn version shipped with
  Unity 6000.4. A newer Roslyn package would make Unity ignore the generator.
- The DLL lives in `Assets/_VuTH/Core/Persistent/DataPackage/Generators/`. Its `.meta` file sets the
  `RoslynAnalyzer` label and disables every platform. Because it sits inside the `VuTH.Persistent.DataPackage`
  assembly folder, Unity runs it only for that assembly and for the assemblies referencing it.

## Build and test

```bash
cd Tools/SourceGenerators
dotnet test VuTH.Persistent.Generators.Tests      # generator unit tests (no Unity needed)
dotnet build VuTH.Persistent.Generators -c Release # builds and copies the DLL into Assets
```

After a Release build, return to Unity to reimport the DLL, then commit it. Keep the existing `.meta` file: it holds
the analyzer settings.
