using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace VuTH.Persistent.Generators.Tests
{
    public class PayloadMappingGeneratorTests
    {
        /// <summary>Minimal stand-ins of the Unity / runtime types the generator looks for.</summary>
        private const string Runtime = @"
namespace UnityEngine
{
    using System;
    public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    public sealed class MinAttribute : Attribute { public MinAttribute(float min) { } }
    public sealed class ColorUsageAttribute : Attribute
    {
        public ColorUsageAttribute(bool showAlpha) { }
        public ColorUsageAttribute(bool showAlpha, bool hdr) { }
    }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
}

namespace _VuTH.Core.Persistent.DataPackage
{
    using System;
    public sealed class GeneratePayloadMappingAttribute : Attribute { }
    public sealed class GeneratePersistentFieldsAttribute : Attribute { }
    public sealed class PayloadIgnoreAttribute : Attribute { }
    public sealed class PayloadMemberAttribute : Attribute { public PayloadMemberAttribute(string name) { } }

    public interface IPersistencePackage { void MarkDirty(); }

    public abstract class PersistencePackage<TData> : IPersistencePackage where TData : class
    {
        public void MarkDirty() { }
        public abstract TData ExtractPayload();
        public abstract void InjectPayload(TData data);
        protected void LoadWithoutNotify(Action action) => action();
    }

    public interface IPersistentFieldNormalizer<T> { T Normalize(T value); }
    public interface IPersistentFieldFactory<T, out TField> where TField : PersistentField<T>
    { TField Create(IPersistencePackage owner, T defaultValue); }
    public enum RangeMode { Clamp, Reject }
    public sealed class RejectOutOfRangeAttribute : Attribute { }

    public class PersistentField<T>
    {
        private readonly Func<T, T> _normalize;
        public PersistentField(IPersistencePackage owner, T value = default, bool notifyOnChange = true, Func<T, T> normalize = null)
        { _normalize = normalize; _value = Normalize(value); }
        private T _value;
        private T Normalize(T v) => _normalize != null ? _normalize(v) : v;
        public T Value { get => _value; set => TrySetValue(value); }
        public bool TrySetValue(T value) { if (!AcceptsValue(value)) return false; _value = Normalize(value); return true; }
        protected virtual bool AcceptsValue(T value) => true;
        public void SetValueWithoutMarkingDirty(T value) => _value = Normalize(value);
    }

    public class RangedPersistentField<T> : PersistentField<T> where T : struct, IComparable<T>
    {
        public RangedPersistentField(IPersistencePackage owner, T value, T min, T max, RangeMode mode = RangeMode.Clamp, Func<T, T> normalize = null, bool notifyOnChange = true)
            : base(owner, value, notifyOnChange, v => normalize != null ? normalize(PersistentFieldConstraints.Clamp(v, min, max)) : PersistentFieldConstraints.Clamp(v, min, max))
        { Min = min; Max = max; Mode = mode; }
        public T Min { get; }
        public T Max { get; }
        public RangeMode Mode { get; }
        protected override bool AcceptsValue(T value) => Mode == RangeMode.Clamp || (value.CompareTo(Min) >= 0 && value.CompareTo(Max) <= 0);
    }

    public class ColorPersistentField : PersistentField<UnityEngine.Color>
    {
        public ColorPersistentField(IPersistencePackage owner, UnityEngine.Color value, bool showAlpha = true, bool hdr = false, Func<UnityEngine.Color, UnityEngine.Color> normalize = null, bool notifyOnChange = true)
            : base(owner, value, notifyOnChange, v => PersistentFieldConstraints.Color(v, showAlpha, hdr)) { }
    }

    public static class PersistentFieldConstraints
    {
        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
            => value.CompareTo(min) < 0 ? min : value.CompareTo(max) > 0 ? max : value;
        public static UnityEngine.Color Color(UnityEngine.Color value, bool showAlpha, bool hdr)
        { if (!showAlpha) value.a = 1f; return value; }
    }
}";

        private static CSharpCompilation Compile(string source)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));

            return CSharpCompilation.Create("Tests" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(Runtime), CSharpSyntaxTree.ParseText(source) },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static (ImmutableArray<Diagnostic> Diagnostics, string[] Generated, Compilation Output) Run(string source)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));

            var compilation = CSharpCompilation.Create("Tests" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(Runtime), CSharpSyntaxTree.ParseText(source) },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var driver = CSharpGeneratorDriver.Create(new PayloadMappingGenerator())
                .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

            var generated = driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()).ToArray();
            return (diagnostics, generated, output);
        }

        private static string[] Errors(Compilation output) =>
            output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();

        private static Assembly Load(Compilation output)
        {
            using var stream = new MemoryStream();
            var result = output.Emit(stream);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            return Assembly.Load(stream.ToArray());
        }

        // ---- Mapping of declared fields ----

        [Fact]
        public void MapsFieldsByName_ExactThenIgnoringCase_AndCompiles()
        {
            var (diagnostics, generated, output) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
namespace Game
{
    public class ProfileDto { public string Name; public int level { get; set; } }

    [GeneratePayloadMapping]
    public partial class ProfilePackage : PersistencePackage<ProfileDto>
    {
        public PersistentField<string> Name { get; }
        public PersistentField<int> Level { get; }
        public ProfilePackage() { Name = new PersistentField<string>(this); Level = new PersistentField<int>(this); }
    }
}");

            Assert.Empty(diagnostics);
            Assert.Empty(Errors(output));
            var code = Assert.Single(generated);
            Assert.Contains("Name = this.Name.Value", code);
            Assert.Contains("level = this.Level.Value", code);
            Assert.Contains("this.Level.SetValueWithoutMarkingDirty(data.level);", code);
            Assert.Contains("LoadWithoutNotify", code);
        }

        [Fact]
        public void IgnoreAndRename_AreHonored()
        {
            var (diagnostics, generated, output) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { public long coins; }

[GeneratePayloadMapping]
public partial class P : PersistencePackage<Dto>
{
    [PayloadMember(""coins"")] public PersistentField<long> Gold;
    [PayloadIgnore] public PersistentField<int> Session;
}");

            Assert.Empty(diagnostics);
            Assert.Empty(Errors(output));
            var code = Assert.Single(generated);
            Assert.Contains("coins = this.Gold.Value", code);
            Assert.DoesNotContain("Session", code);
        }

        [Fact]
        public void AMethodWrittenByHand_IsKept()
        {
            var (diagnostics, generated, output) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { public float volume; }

[GeneratePayloadMapping]
public partial class P : PersistencePackage<Dto>
{
    public PersistentField<float> Volume;
    public override void InjectPayload(Dto data) { Volume.SetValueWithoutMarkingDirty(System.Math.Min(1f, data.volume)); }
}");

            Assert.Empty(diagnostics);
            Assert.Empty(Errors(output));
            var code = Assert.Single(generated);
            Assert.Contains("ExtractPayload", code);
            Assert.DoesNotContain("InjectPayload", code);
        }

        [Fact]
        public void NestedPackage_IsGeneratedInsideItsContainers()
        {
            var (_, generated, output) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
namespace A.B
{
    public partial class Outer
    {
        public class Dto { public int X; }
        [GeneratePayloadMapping]
        public partial class P : PersistencePackage<Dto> { public PersistentField<int> X; }
    }
}");

            Assert.Empty(Errors(output));
            Assert.Contains("partial class Outer", Assert.Single(generated));
        }

        [Fact]
        public void DeclaredPlainField_IsClampedOnLoad_ByTheDtoRange()
        {
            var (diagnostics, generated, output) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { [UnityEngine.Range(0f, 1f)] public float volume; }
[GeneratePayloadMapping]
public partial class P : PersistencePackage<Dto> { public PersistentField<float> Volume; }");

            Assert.Empty(diagnostics);
            Assert.Empty(Errors(output));
            Assert.Contains("PersistentFieldConstraints.Clamp(data.volume", Assert.Single(generated));
        }

        // ---- Generated fields ----

        [Fact]
        public void GeneratedFields_FollowTheUnityAttributes()
        {
            var (diagnostics, generated, output) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
using UnityEngine;
public class Dto
{
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Min(1)] public int level = 1;
    [ColorUsage(false)] public Color tint;
    public bool muted;
    public string m_PlayerName = ""Player"";
    [System.NonSerialized] public int cache;
}
[GeneratePersistentFields]
public partial class P : PersistencePackage<Dto> { }");

            Assert.Empty(diagnostics);
            Assert.Empty(Errors(output));
            var code = Assert.Single(generated);
            Assert.Contains("RangedPersistentField<float> MasterVolume", code);
            Assert.Contains("RangedPersistentField<int> Level", code);
            Assert.Contains("int.MaxValue", code);
            Assert.Contains("ColorPersistentField Tint", code);
            Assert.Contains("PersistentField<bool> Muted", code);
            Assert.Contains("PersistentField<string> PlayerName", code);
            Assert.DoesNotContain("cache", code.Replace("__vuthDefaults", string.Empty));
        }

        [Fact]
        public void GeneratedFields_WorkAtRuntime_WithDefaultsAndClamping()
        {
            var (_, _, output) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
using UnityEngine;
public class Dto { [Range(0f, 1f)] public float volume = 0.5f; [Min(0)] public int gold; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Dto> { }");

            var assembly = Load(output);
            var packageType = assembly.GetType("P")!;
            var dtoType = assembly.GetType("Dto")!;
            dynamic package = Activator.CreateInstance(packageType)!;

            Assert.Equal(0.5f, (float)package.Volume.Value);   // default of a new DTO

            dynamic dto = Activator.CreateInstance(dtoType)!;
            dto.volume = 3f;
            dto.gold = -5;
            package.InjectPayload(dto);
            Assert.Equal(1f, (float)package.Volume.Value);     // clamped on load
            Assert.Equal(0, (int)package.Gold.Value);

            package.Volume.Value = -1f;                        // clamped when set
            dynamic extracted = package.ExtractPayload();
            Assert.Equal(0f, (float)extracted.volume);
        }

        [Fact]
        public void DeclaredFields_AreKept_AndOnlyTheOthersAreGenerated()
        {
            var (diagnostics, generated, output) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { public int a; public int b; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Dto>
{
    public PersistentField<int> A { get; }
    public P() { A = new PersistentField<int>(this, 7); }
}");

            Assert.Empty(diagnostics);
            Assert.Empty(Errors(output));
            var code = Assert.Single(generated);
            Assert.Contains("PersistentField<int> B", code);
            Assert.DoesNotContain("PersistentField<int> A ", code);
            Assert.Contains("a = this.A.Value", code);
        }

        [Fact]
        public void GeneratedName_AlreadyTaken_IsAWarning()
        {
            var (diagnostics, _, output) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { public int score; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Dto> { public int Score() => 0; }");

            Assert.Equal("VUTHP008", Assert.Single(diagnostics).Id);
            Assert.Empty(Errors(output));
        }

        [Fact]
        public void ConstraintOnAnUnsupportedType_IsAWarning()
        {
            var (diagnostics, _, _) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { [UnityEngine.Range(0f, 1f)] public string name; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Dto> { }");

            Assert.Equal("VUTHP009", Assert.Single(diagnostics).Id);
        }

        // ---- Diagnostics ----

        [Fact]
        public void FieldMappedToANonSerializedMember_IsAWarning()
        {
            var (diagnostics, _, _) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { [System.NonSerialized] public int cache; }
[GeneratePayloadMapping]
public partial class P : PersistencePackage<Dto> { public PersistentField<int> Cache; }");

            Assert.Equal("VUTHP007", Assert.Single(diagnostics).Id);
        }

        [Fact]
        public void MissingMember_IsAWarning_AndTheFieldIsSkipped()
        {
            var (diagnostics, generated, _) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { public int A; }
[GeneratePayloadMapping]
public partial class P : PersistencePackage<Dto> { public PersistentField<int> A; public PersistentField<int> Missing; }");

            Assert.Equal("VUTHP003", Assert.Single(diagnostics).Id);
            Assert.DoesNotContain("Missing", Assert.Single(generated));
        }

        [Fact]
        public void TypeMismatch_IsAnError()
        {
            var (diagnostics, _, _) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { public double A; }
[GeneratePayloadMapping]
public partial class P : PersistencePackage<Dto> { public PersistentField<float> A; }");

            Assert.Equal("VUTHP004", Assert.Single(diagnostics).Id);
        }

        [Fact]
        public void NonPartialPackage_IsAnError()
        {
            var (diagnostics, generated, _) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { }
[GeneratePayloadMapping]
public class P : PersistencePackage<Dto> { public override Dto ExtractPayload() => null; public override void InjectPayload(Dto d) { } }");

            Assert.Equal("VUTHP002", Assert.Single(diagnostics).Id);
            Assert.Empty(generated);
        }

        [Fact]
        public void NotAPackage_IsAnError()
        {
            var (diagnostics, _, _) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
[GeneratePayloadMapping]
public partial class NotAPackage { }");

            Assert.Equal("VUTHP001", Assert.Single(diagnostics).Id);
        }

        [Fact]
        public void DtoWithoutParameterlessConstructor_IsAnError()
        {
            var (diagnostics, _, _) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { public Dto(int x) { } public int A; }
[GeneratePayloadMapping]
public partial class P : PersistencePackage<Dto> { public PersistentField<int> A; }");

            Assert.Contains(diagnostics, d => d.Id == "VUTHP005");
        }

        // ---- Reject mode ----

        [Fact]
        public void RejectOutOfRange_RefusesGameValues_ButClampsLoadedData()
        {
            var (diagnostics, generated, output) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
using UnityEngine;
public class Wallet { [Min(0), RejectOutOfRange] public long gold = 50; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Wallet> { }");

            Assert.Empty(diagnostics);
            Assert.Contains("RangeMode.Reject", Assert.Single(generated));

            dynamic package = Activator.CreateInstance(Load(output).GetType("P")!)!;
            Assert.False((bool)package.Gold.TrySetValue(-50L));  // spending 100 with 50: refused, not clamped to 0
            Assert.Equal(50L, (long)package.Gold.Value);

            package.Gold.SetValueWithoutMarkingDirty(-7L);        // a damaged save is repaired
            Assert.Equal(0L, (long)package.Gold.Value);
        }

        [Fact]
        public void RejectOutOfRange_WithoutARange_IsAWarning()
        {
            var (diagnostics, _, _) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { [RejectOutOfRange] public long gold; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Dto> { }");

            Assert.Equal("VUTHP015", Assert.Single(diagnostics).Id);
        }

        [Fact]
        public void DeclaredRangedField_WithADtoRange_IsAWarning()
        {
            var (diagnostics, generated, _) = Run(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { [UnityEngine.Range(0f, 1f)] public float volume; }
[GeneratePayloadMapping]
public partial class P : PersistencePackage<Dto>
{
    public RangedPersistentField<float> Volume;
}");

            Assert.Equal("VUTHP010", Assert.Single(diagnostics).Id);
            Assert.DoesNotContain("Clamp(", Assert.Single(generated));
        }

        // ---- User field attributes ----

        private const string UserAttributes = @"
using System;
using _VuTH.Core.Persistent.DataPackage;

public sealed class StepAttribute : Attribute, IPersistentFieldNormalizer<float>
{
    private readonly float _step;
    public StepAttribute(float step) => _step = step;
    public float Normalize(float value) => (float)Math.Round(value / _step) * _step;
}

public sealed class NegateAttribute : Attribute, IPersistentFieldNormalizer<float>
{
    public bool Enabled { get; set; }
    public float Normalize(float value) => Enabled ? -value : value;
}

public sealed class CurrencyField : PersistentField<long>
{
    public CurrencyField(IPersistencePackage owner, long value) : base(owner, value) { }
    public bool TrySpend(long amount) => TrySetValue(Value - amount) && Value >= 0;
}

public sealed class CurrencyAttribute : Attribute, IPersistentFieldFactory<long, CurrencyField>
{
    public CurrencyField Create(IPersistencePackage owner, long defaultValue) => new CurrencyField(owner, defaultValue);
}

public sealed class OtherCurrencyAttribute : Attribute, IPersistentFieldFactory<long, CurrencyField>
{
    public CurrencyField Create(IPersistencePackage owner, long defaultValue) => new CurrencyField(owner, defaultValue);
}
";

        [Fact]
        public void UserNormalizers_RunAfterTheRange_InDeclarationOrder()
        {
            var (diagnostics, generated, output) = Run(UserAttributes + @"
public class Settings { [UnityEngine.Range(0f, 1f), Step(0.25f)] public float volume = 0.5f; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Settings> { }");

            Assert.Empty(diagnostics);
            Assert.Empty(Errors(output));
            var code = Assert.Single(generated);
            Assert.Contains("private static readonly global::StepAttribute __vuthVolumeNormalizer0 = new global::StepAttribute(0.25F);", code);

            dynamic package = Activator.CreateInstance(Load(output).GetType("P")!)!;
            package.Volume.Value = 0.6f;
            Assert.Equal(0.5f, (float)package.Volume.Value);   // rounded to the step
            package.Volume.Value = 3f;
            Assert.Equal(1f, (float)package.Volume.Value);     // clamped, then rounded
        }

        [Fact]
        public void UserNormalizer_NamedArguments_AreRecreated()
        {
            var (diagnostics, generated, output) = Run(UserAttributes + @"
public class Settings { [Negate(Enabled = true)] public float value; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Settings> { }");

            Assert.Empty(diagnostics);
            Assert.Contains("new global::NegateAttribute() { Enabled = true }", Assert.Single(generated));

            dynamic package = Activator.CreateInstance(Load(output).GetType("P")!)!;
            package.Value.Value = 2f;
            Assert.Equal(-2f, (float)package.Value.Value);
        }

        [Fact]
        public void UserNormalizer_OnADeclaredField_AppliesToLoadedData()
        {
            var (diagnostics, generated, output) = Run(UserAttributes + @"
public class Settings { [Step(0.5f)] public float volume; }
[GeneratePayloadMapping]
public partial class P : PersistencePackage<Settings>
{
    public PersistentField<float> Volume { get; }
    public P() { Volume = new PersistentField<float>(this); }
}");

            Assert.Empty(diagnostics);
            Assert.Empty(Errors(output));
            Assert.Contains("__vuthVolumeNormalizer0.Normalize(data.volume)", Assert.Single(generated));
        }

        [Fact]
        public void UserFactory_ChoosesTheGeneratedFieldType()
        {
            var (diagnostics, generated, output) = Run(UserAttributes + @"
public class Wallet { [Currency] public long gold = 100; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Wallet> { }");

            Assert.Empty(diagnostics);
            Assert.Empty(Errors(output));
            Assert.Contains("public global::CurrencyField Gold", Assert.Single(generated));

            dynamic package = Activator.CreateInstance(Load(output).GetType("P")!)!;
            Assert.True((bool)package.Gold.TrySpend(30L));      // the field's own API
            Assert.Equal(70L, (long)package.Gold.Value);
        }

        [Fact]
        public void TwoFactories_AreAnError()
        {
            var (diagnostics, _, _) = Run(UserAttributes + @"
public class Wallet { [Currency, OtherCurrency] public long gold; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Wallet> { }");

            Assert.Contains(diagnostics, d => d.Id == "VUTHP012");
        }

        [Fact]
        public void Factory_WithConstraints_IgnoresThem_WithAWarning()
        {
            var (diagnostics, generated, output) = Run(UserAttributes + @"
public class Wallet { [Currency, UnityEngine.Min(0)] public long gold; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Wallet> { }");

            Assert.Equal("VUTHP013", Assert.Single(diagnostics).Id);
            Assert.Empty(Errors(output));
            Assert.DoesNotContain("RangedPersistentField", Assert.Single(generated));
        }

        [Fact]
        public void Normalizer_OfAnotherType_IsAnError()
        {
            var (diagnostics, _, _) = Run(UserAttributes + @"
public class Settings { [Step(1f)] public int count; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Settings> { }");

            Assert.Equal("VUTHP011", Assert.Single(diagnostics).Id);
        }

        [Fact]
        public void InaccessibleAttribute_IsAnError()
        {
            var (diagnostics, _, _) = Run(@"
using System;
using _VuTH.Core.Persistent.DataPackage;
public class Holder
{
    private sealed class HiddenAttribute : Attribute, IPersistentFieldNormalizer<int> { public int Normalize(int v) => v; }
    public class Dto { [Hidden] public int count; }
}
[GeneratePersistentFields]
public partial class P : PersistencePackage<Holder.Dto> { }");

            Assert.Contains(diagnostics, d => d.Id == "VUTHP014");
        }

        // ---- Incremental caching ----

        [Fact]
        public void AnUnrelatedEdit_DoesNotRegenerateThePackage()
        {
            var compilation = Compile(@"
using _VuTH.Core.Persistent.DataPackage;
public class Dto { public int a; }
[GeneratePersistentFields]
public partial class P : PersistencePackage<Dto> { }");

            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new[] { new PayloadMappingGenerator().AsSourceGenerator() },
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
            driver = driver.RunGenerators(compilation);

            var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated { }"));
            driver = driver.RunGenerators(edited);

            var outputs = driver.GetRunResult().Results.Single().TrackedOutputSteps
                .SelectMany(step => step.Value)
                .SelectMany(step => step.Outputs)
                .ToList();
            Assert.NotEmpty(outputs);
            Assert.All(outputs, output =>
                Assert.True(output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    output.Reason.ToString()));
        }
    }
}
