using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace VuTH.Persistent.Generators
{
    internal static class Diagnostics
    {
        private const string Category = "VuTH.Persistence";

        public static readonly DiagnosticDescriptor NotAPackage = new(
            "VUTHP001", "Not a persistence package",
            "'{0}' has [GeneratePayloadMapping] / [GeneratePersistentFields] but does not derive from PersistencePackage<TData>",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor NotPartial = new(
            "VUTHP002", "Package must be partial",
            "'{0}' (and its containing types) must be declared partial for the payload generator",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor NoDtoMember = new(
            "VUTHP003", "No matching DTO member",
            "PersistentField '{0}' is not saved: {1} has no member named '{2}'. Rename it, use [PayloadMember(\"name\")] or [PayloadIgnore].",
            Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor TypeMismatch = new(
            "VUTHP004", "DTO member type mismatch",
            "PersistentField '{0}' holds {1}, but {2}.{3} is {4}; the field is not mapped",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor NoConstructor = new(
            "VUTHP005", "DTO has no parameterless constructor",
            "{0} needs an accessible parameterless constructor for the code generated for '{1}'",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor NotAccessible = new(
            "VUTHP006", "DTO member not accessible",
            "{0}.{1} cannot be written or read by the generated mapping of field '{2}' (read-only, or not public / internal)",
            Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor NonSerialized = new(
            "VUTHP007", "DTO member is not serialized",
            "PersistentField '{0}' maps to {1}.{2}, which is [NonSerialized] and never saved; the field is not mapped",
            Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor NameTaken = new(
            "VUTHP008", "Generated field name taken",
            "No field is generated for {0}.{1}: '{2}' is already a member of '{3}'. Declare the field yourself or rename one of them.",
            Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor UnsupportedConstraint = new(
            "VUTHP009", "Unity attribute not applied",
            "[{0}] on '{1}' is not applied to the saved value: it supports numeric types ([Range], [Min]) or Color ([ColorUsage]), not {2}",
            Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor DoubleConstraint = new(
            "VUTHP010", "Constraint applied twice",
            "Field '{0}' is a {1} with its own constraint, and {2}.{3} also has [{4}]: values must satisfy both. Keep one of them.",
            Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor ExtensionTypeMismatch = new(
            "VUTHP011", "Field attribute type mismatch",
            "[{0}] works on {1}, but {2}.{3} is {4}; it is not applied",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor SeveralFactories = new(
            "VUTHP012", "Several field factories",
            "{0}.{1} has several attributes implementing IPersistentFieldFactory ({2}); keep one",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor FactoryIgnoresConstraints = new(
            "VUTHP013", "Constraints ignored by a field factory",
            "{0}.{1} uses the field factory [{2}], which builds the whole field: its other constraints / normalizers ({3}) are ignored",
            Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor ExtensionNotAccessible = new(
            "VUTHP014", "Field attribute not accessible",
            "[{0}] on {1}.{2} cannot be instantiated by the code generated for '{3}' (not accessible, or abstract)",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor RejectWithoutRange = new(
            "VUTHP015", "[RejectOutOfRange] without a range",
            "[RejectOutOfRange] on '{0}' has no effect: the member has no [Range] or [Min]",
            Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor FactoryOnDeclaredField = new(
            "VUTHP016", "Field factory not used",
            "[{0}] on {1}.{2} is not used: the package declares the field '{3}' itself. Remove the declaration to let the factory create it, or remove the attribute.",
            Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor NormalizerOnlyOnLoad = new(
            "VUTHP017", "Normalizer applied to loaded data only",
            "The package declares the field '{0}' itself: [{1}] on {2}.{3} only normalizes loaded data, not the values the game sets. Remove the declaration to let the generator create the field, or pass the normalization to the field you create.",
            Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor BoundOutOfType = new(
            "VUTHP018", "Range bound outside the member type",
            "[{0}] bound {1} on '{2}' does not fit in {3}; {4} is used instead",
            Category, DiagnosticSeverity.Warning, true);

        public static readonly IReadOnlyDictionary<string, DiagnosticDescriptor> ById = new Dictionary<string, DiagnosticDescriptor>
        {
            [NotAPackage.Id] = NotAPackage,
            [NotPartial.Id] = NotPartial,
            [NoDtoMember.Id] = NoDtoMember,
            [TypeMismatch.Id] = TypeMismatch,
            [NoConstructor.Id] = NoConstructor,
            [NotAccessible.Id] = NotAccessible,
            [NonSerialized.Id] = NonSerialized,
            [NameTaken.Id] = NameTaken,
            [UnsupportedConstraint.Id] = UnsupportedConstraint,
            [DoubleConstraint.Id] = DoubleConstraint,
            [ExtensionTypeMismatch.Id] = ExtensionTypeMismatch,
            [SeveralFactories.Id] = SeveralFactories,
            [FactoryIgnoresConstraints.Id] = FactoryIgnoresConstraints,
            [ExtensionNotAccessible.Id] = ExtensionNotAccessible,
            [RejectWithoutRange.Id] = RejectWithoutRange,
            [FactoryOnDeclaredField.Id] = FactoryOnDeclaredField,
            [NormalizerOnlyOnLoad.Id] = NormalizerOnlyOnLoad,
            [BoundOutOfType.Id] = BoundOutOfType,
        };
    }
}
