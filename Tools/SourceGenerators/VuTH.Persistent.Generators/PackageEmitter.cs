using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VuTH.Persistent.Generators
{
    /// <summary>
    /// Analyzes one package and renders its generated code. Runs inside the pipeline transform: everything it returns is
    /// plain values (<see cref="GenerationResult"/>).
    /// </summary>
    internal static class PackageEmitter
    {
        private const string Namespace = "_VuTH.Core.Persistent.DataPackage";
        public const string MappingAttribute = Namespace + ".GeneratePayloadMappingAttribute";
        public const string FieldsAttribute = Namespace + ".GeneratePersistentFieldsAttribute";
        private const string IgnoreAttribute = Namespace + ".PayloadIgnoreAttribute";
        private const string MemberAttribute = Namespace + ".PayloadMemberAttribute";
        private const string RejectAttribute = Namespace + ".RejectOutOfRangeAttribute";
        private const string PackageBase = Namespace + ".PersistencePackage<TData>";
        private const string FieldType = Namespace + ".PersistentField<T>";
        private const string RangedFieldType = Namespace + ".RangedPersistentField<T>";
        private const string ColorFieldType = Namespace + ".ColorPersistentField";
        private const string NormalizerInterface = Namespace + ".IPersistentFieldNormalizer<T>";
        private const string FactoryInterface = Namespace + ".IPersistentFieldFactory<T, TField>";
        private const string Global = "global::" + Namespace + ".";

        private const string RangeAttribute = "UnityEngine.RangeAttribute";
        private const string MinAttribute = "UnityEngine.MinAttribute";
        private const string ColorUsageAttribute = "UnityEngine.ColorUsageAttribute";
        private const string NonSerializedAttribute = "System.NonSerializedAttribute";
        private const string ColorType = "UnityEngine.Color";

        public static bool HasAttribute(ISymbol symbol, string fullName) => FindAttribute(symbol, fullName) != null;

        private static AttributeData? FindAttribute(ISymbol symbol, string fullName) =>
            symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == fullName);

        #region Model (internal to one emission)

        private sealed class Constraint
        {
            public string Kind = ""; // "range" or "color"
            public string Attribute = "";
            public string Min = "";
            public string Max = "";
            public bool Reject;
            public bool ShowAlpha;
            public bool Hdr;
        }

        /// <summary>An instance of a user attribute (normalizer / factory) the generated code creates once.</summary>
        private sealed class Extension
        {
            public string Name = "";
            public string TypeName = "";
            public string Creation = "";
            public string FieldTypeName = ""; // factories only
        }

        private sealed class Rules
        {
            public Constraint? Constraint;
            public readonly List<Extension> Normalizers = new();
            public Extension? Factory;
        }

        private sealed class Mapping
        {
            public string Field = "";
            public string DtoMember = "";
            public string ValueType = "";
            public bool DtoWritable;
            public bool DtoReadable;
            public bool Generated;
            public bool DeclaredConstrained;
            public Rules Rules = new();
        }

        private sealed class Context
        {
            public Context(INamedTypeSymbol package, Compilation compilation)
            {
                Package = package;
                Compilation = compilation;
            }

            public INamedTypeSymbol Package { get; }
            public Compilation Compilation { get; }
            public readonly List<DiagnosticInfo> Diagnostics = new();
            public readonly List<Extension> Statics = new();

            public void Report(DiagnosticDescriptor descriptor, Location? location, params object?[] args) =>
                Diagnostics.Add(new DiagnosticInfo(descriptor.Id, LocationInfo.From(location),
                    new EquatableArray<string>(args.Select(a => a?.ToString() ?? string.Empty))));
        }

        #endregion

        public static GenerationResult Emit(INamedTypeSymbol package, Compilation compilation, bool generateFields,
            CancellationToken cancellationToken)
        {
            var ctx = new Context(package, compilation);
            var hintName = $"{GetHintName(package)}.PayloadMapping.g.cs";
            var source = Analyze(ctx, generateFields, cancellationToken);
            return new GenerationResult(hintName, source, new EquatableArray<DiagnosticInfo>(ctx.Diagnostics));
        }

        private static string? Analyze(Context ctx, bool generateFields, CancellationToken cancellationToken)
        {
            var package = ctx.Package;
            var location = package.Locations.FirstOrDefault();

            var dto = FindDtoType(package);
            if (dto == null)
            {
                ctx.Report(Diagnostics.NotAPackage, location, package.Name);
                return null;
            }

            if (!IsPartialAll(package))
            {
                ctx.Report(Diagnostics.NotPartial, location, package.Name);
                return null;
            }

            var dtoMembers = GetDtoMembers(dto);
            var mappings = new List<Mapping>();
            var mappedDtoMembers = new HashSet<string>();

            // 1. Fields declared by the package.
            foreach (var member in package.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (member.IsStatic || member.IsImplicitlyDeclared) continue;
                var memberType = member switch
                {
                    IFieldSymbol field => field.Type,
                    IPropertySymbol property => property.Type,
                    _ => null,
                };

                var fieldValueType = GetPersistentFieldValueType(memberType);
                if (fieldValueType == null) continue;

                // Backing fields of auto-properties are reported through the property.
                if (member is IFieldSymbol { AssociatedSymbol: not null }) continue;
                if (HasAttribute(member, IgnoreAttribute)) continue;

                var dtoName = GetPayloadName(member) ?? member.Name;
                var memberLocation = member.Locations.FirstOrDefault() ?? location;

                var dtoMember = FindByName(dtoMembers, dtoName);
                if (dtoMember == null)
                {
                    ctx.Report(Diagnostics.NoDtoMember, memberLocation, member.Name, dto.Name, dtoName);
                    continue;
                }

                if (HasAttribute(dtoMember, NonSerializedAttribute))
                {
                    ctx.Report(Diagnostics.NonSerialized, memberLocation, member.Name, dto.Name, dtoMember.Name);
                    continue;
                }

                var dtoMemberType = TypeOf(dtoMember);
                if (!SymbolEqualityComparer.Default.Equals(dtoMemberType, fieldValueType))
                {
                    ctx.Report(Diagnostics.TypeMismatch, memberLocation, member.Name, fieldValueType.ToDisplayString(),
                        dto.Name, dtoMember.Name, dtoMemberType.ToDisplayString());
                    continue;
                }

                var mapping = new Mapping
                {
                    Field = member.Name,
                    DtoMember = dtoMember.Name,
                    ValueType = dtoMemberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    DtoWritable = CanWrite(dtoMember),
                    DtoReadable = CanRead(dtoMember),
                    Rules = ReadRules(ctx, dtoMember, dtoMemberType, member.Name, memberLocation, declared: true),
                };

                // A declared ranged / color field constrains itself: the DTO constraint would apply on top of it.
                var declaredKind = ConstrainedKind(memberType!);
                if (declaredKind != null && mapping.Rules.Constraint != null)
                {
                    ctx.Report(Diagnostics.DoubleConstraint, memberLocation, member.Name, declaredKind, dto.Name,
                        dtoMember.Name, mapping.Rules.Constraint.Attribute);
                    mapping.DeclaredConstrained = true;
                }

                mappedDtoMembers.Add(dtoMember.Name);
                mappings.Add(mapping);
            }

            var hasDefaultConstructor = HasAccessibleParameterlessConstructor(dto);

            // 2. Fields generated from the remaining DTO members.
            if (generateFields)
            {
                if (!hasDefaultConstructor)
                {
                    ctx.Report(Diagnostics.NoConstructor, location, dto.Name, package.Name);
                    return null;
                }

                var taken = new HashSet<string>(package.GetMembers().Select(m => m.Name)) { package.Name };
                foreach (var dtoMember in dtoMembers)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (mappedDtoMembers.Contains(dtoMember.Name) || HasAttribute(dtoMember, NonSerializedAttribute)) continue;
                    if (!CanRead(dtoMember) || !CanWrite(dtoMember)) continue;

                    var name = ToPropertyName(dtoMember.Name);
                    if (!taken.Add(name))
                    {
                        ctx.Report(Diagnostics.NameTaken, location, dto.Name, dtoMember.Name, name, package.Name);
                        continue;
                    }

                    var type = TypeOf(dtoMember);
                    mappings.Add(new Mapping
                    {
                        Field = name,
                        DtoMember = dtoMember.Name,
                        ValueType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        DtoWritable = true,
                        DtoReadable = true,
                        Generated = true,
                        Rules = ReadRules(ctx, dtoMember, type, name, dtoMember.Locations.FirstOrDefault() ?? location,
                            declared: false),
                    });
                }
            }

            var hasExtract = package.GetMembers("ExtractPayload").OfType<IMethodSymbol>()
                .Any(m => m.Parameters.Length == 0 && !m.IsImplicitlyDeclared);
            var hasInject = package.GetMembers("InjectPayload").OfType<IMethodSymbol>()
                .Any(m => m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, dto));

            var generateExtract = !hasExtract;
            if (generateExtract && !hasDefaultConstructor)
            {
                ctx.Report(Diagnostics.NoConstructor, location, dto.Name, package.Name);
                generateExtract = false;
            }

            foreach (var mapping in mappings)
            {
                if ((generateExtract && !mapping.DtoWritable) || (!hasInject && !mapping.DtoReadable))
                {
                    ctx.Report(Diagnostics.NotAccessible, location, dto.Name, mapping.DtoMember, mapping.Field);
                }
            }

            var hasGeneratedFields = mappings.Any(m => m.Generated);
            if (!generateExtract && hasInject && !hasGeneratedFields) return null;

            return Render(ctx, dto, mappings, generateExtract, !hasInject);
        }

        #region Symbols

        private static ITypeSymbol? FindDtoType(INamedTypeSymbol package)
        {
            for (var type = package.BaseType; type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.OriginalDefinition.ToDisplayString() == PackageBase)
                    return type.TypeArguments[0];
            }

            return null;
        }

        /// <summary>The T of a <c>PersistentField&lt;T&gt;</c> or of any type deriving from it (ranged, color, user fields).</summary>
        private static ITypeSymbol? GetPersistentFieldValueType(ITypeSymbol? type)
        {
            for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.OriginalDefinition.ToDisplayString() == FieldType)
                    return current.TypeArguments[0];
            }

            return null;
        }

        /// <summary>"RangedPersistentField" / "ColorPersistentField" when the declared field constrains itself.</summary>
        private static string? ConstrainedKind(ITypeSymbol type)
        {
            for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
            {
                var name = current.OriginalDefinition.ToDisplayString();
                if (name == RangedFieldType) return "RangedPersistentField";
                if (name == ColorFieldType) return "ColorPersistentField";
            }

            return null;
        }

        private static bool IsPartialAll(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.ContainingType)
            {
                foreach (var reference in current.DeclaringSyntaxReferences)
                {
                    if (reference.GetSyntax() is TypeDeclarationSyntax declaration &&
                        !declaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static string? GetPayloadName(ISymbol member)
        {
            var attribute = FindAttribute(member, MemberAttribute);
            return attribute?.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as string : null;
        }

        /// <summary>Instance fields and properties of the DTO (and its base types) the package can access.</summary>
        private static List<ISymbol> GetDtoMembers(ITypeSymbol dto)
        {
            var members = new List<ISymbol>();
            var seen = new HashSet<string>();
            for (var type = dto; type != null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
            {
                foreach (var member in type.GetMembers())
                {
                    if (member.IsStatic || member.IsImplicitlyDeclared) continue;
                    if (member.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)) continue;
                    if (member is not (IFieldSymbol { AssociatedSymbol: null, IsConst: false } or IPropertySymbol { IsIndexer: false }))
                        continue;
                    if (seen.Add(member.Name)) members.Add(member);
                }
            }

            return members;
        }

        /// <summary>Exact name, else a unique case-insensitive match.</summary>
        private static ISymbol? FindByName(List<ISymbol> members, string name)
        {
            var exact = members.FirstOrDefault(m => m.Name == name);
            if (exact != null) return exact;

            var ignoringCase = members.Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            return ignoringCase.Count == 1 ? ignoringCase[0] : null;
        }

        private static ITypeSymbol TypeOf(ISymbol member) => member is IFieldSymbol f ? f.Type : ((IPropertySymbol)member).Type;

        private static bool CanWrite(ISymbol member) => member switch
        {
            IFieldSymbol field => !field.IsReadOnly && !field.IsConst,
            IPropertySymbol property => property.SetMethod is { DeclaredAccessibility: Accessibility.Public or Accessibility.Internal },
            _ => false,
        };

        private static bool CanRead(ISymbol member) => member switch
        {
            IFieldSymbol => true,
            IPropertySymbol property => property.GetMethod is { DeclaredAccessibility: Accessibility.Public or Accessibility.Internal },
            _ => false,
        };

        private static bool HasAccessibleParameterlessConstructor(ITypeSymbol dto)
        {
            if (dto.IsAbstract || dto is not INamedTypeSymbol named) return false;
            if (named.TypeKind == TypeKind.Struct) return true;

            return named.InstanceConstructors.Any(c =>
                c.Parameters.Length == 0 && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);
        }

        /// <summary><c>masterVolume</c> → <c>MasterVolume</c>; <c>m_Gold</c> / <c>_gold</c> → <c>Gold</c>.</summary>
        private static string ToPropertyName(string memberName)
        {
            var name = memberName;
            if (name.StartsWith("m_", StringComparison.Ordinal) && name.Length > 2) name = name.Substring(2);
            name = name.TrimStart('_');
            if (name.Length == 0) return memberName;
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        #endregion

        #region Rules (Unity constraints, user normalizers / factories)

        private static Rules ReadRules(Context ctx, ISymbol member, ITypeSymbol type, string fieldName, Location? location,
            bool declared)
        {
            var rules = new Rules { Constraint = ReadConstraint(ctx, member, type, location) };

            if (HasAttribute(member, RejectAttribute) && rules.Constraint?.Kind != "range")
            {
                ctx.Report(Diagnostics.RejectWithoutRange, location, member.Name);
            }

            var factories = new List<(AttributeData Attribute, INamedTypeSymbol Interface)>();
            var index = 0;
            foreach (var attribute in member.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass == null) continue;

                var normalizer = attributeClass.AllInterfaces.FirstOrDefault(i =>
                    i.OriginalDefinition.ToDisplayString() == NormalizerInterface);
                var factory = attributeClass.AllInterfaces.FirstOrDefault(i =>
                    i.OriginalDefinition.ToDisplayString() == FactoryInterface);
                if (normalizer == null && factory == null) continue;

                var handled = normalizer ?? factory!;
                if (!SymbolEqualityComparer.Default.Equals(handled.TypeArguments[0], type))
                {
                    ctx.Report(Diagnostics.ExtensionTypeMismatch, location, attributeClass.Name,
                        handled.TypeArguments[0].ToDisplayString(), member.ContainingType.Name, member.Name, type.ToDisplayString());
                    continue;
                }

                if (!IsConstructible(ctx, attribute, attributeClass))
                {
                    ctx.Report(Diagnostics.ExtensionNotAccessible, location, attributeClass.Name,
                        member.ContainingType.Name, member.Name, ctx.Package.Name);
                    continue;
                }

                if (factory != null)
                {
                    factories.Add((attribute, factory));
                    continue;
                }

                var extension = CreateExtension(ctx, attribute, $"__vuth{fieldName}Normalizer{index++}");
                rules.Normalizers.Add(extension);
            }

            if (factories.Count > 1)
            {
                ctx.Report(Diagnostics.SeveralFactories, location, member.ContainingType.Name, member.Name,
                    string.Join(", ", factories.Select(f => f.Attribute.AttributeClass!.Name)));
            }
            else if (factories.Count == 1 && !declared)
            {
                var (attribute, factoryInterface) = factories[0];
                if (rules.Constraint != null || rules.Normalizers.Count > 0)
                {
                    var ignored = rules.Normalizers.Select(n => n.TypeName.Split('.').Last()).ToList();
                    if (rules.Constraint != null) ignored.Insert(0, rules.Constraint.Attribute);
                    ctx.Report(Diagnostics.FactoryIgnoresConstraints, location, member.ContainingType.Name, member.Name,
                        attribute.AttributeClass!.Name, string.Join(", ", ignored));

                    // The factory builds the whole field: drop the statics created for the ignored normalizers.
                    foreach (var normalizer in rules.Normalizers) ctx.Statics.Remove(normalizer);
                    rules.Normalizers.Clear();
                    rules.Constraint = null;
                }

                var extension = CreateExtension(ctx, attribute, $"__vuth{fieldName}Factory");
                extension.FieldTypeName = factoryInterface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                rules.Factory = extension;
            }

            return rules;
        }

        private static bool IsConstructible(Context ctx, AttributeData attribute, INamedTypeSymbol attributeClass)
        {
            if (attributeClass.IsAbstract) return false;
            if (!ctx.Compilation.IsSymbolAccessibleWithin(attributeClass, ctx.Package)) return false;
            if (attribute.AttributeConstructor != null &&
                !ctx.Compilation.IsSymbolAccessibleWithin(attribute.AttributeConstructor, ctx.Package))
            {
                return false;
            }

            // Named arguments are set in an object initializer: they must be writable from the package.
            foreach (var named in attribute.NamedArguments)
            {
                var target = attributeClass.GetMembers(named.Key).FirstOrDefault();
                if (target == null || !ctx.Compilation.IsSymbolAccessibleWithin(target, ctx.Package)) return false;
            }

            return true;
        }

        /// <summary>
        /// The attribute re-created in code from its arguments (always constants in C#), as a static of the package.
        /// </summary>
        private static Extension CreateExtension(Context ctx, AttributeData attribute, string name)
        {
            var typeName = attribute.AttributeClass!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var arguments = string.Join(", ", attribute.ConstructorArguments.Select(Constant));
            var creation = $"new {typeName}({arguments})";
            if (attribute.NamedArguments.Length > 0)
            {
                creation += " { " + string.Join(", ", attribute.NamedArguments.Select(a => $"{a.Key} = {Constant(a.Value)}")) + " }";
            }

            var extension = new Extension { Name = name, TypeName = typeName, Creation = creation };
            ctx.Statics.Add(extension);
            return extension;
        }

        /// <summary>
        /// An attribute argument as C# code of its exact type (<c>TypedConstant.ToCSharpString</c> drops the float suffix,
        /// so <c>0.25f</c> would become a double).
        /// </summary>
        private static string Constant(TypedConstant constant)
        {
            if (constant.IsNull) return "null";

            switch (constant.Kind)
            {
                case TypedConstantKind.Array:
                    var elementType = ((IArrayTypeSymbol)constant.Type!).ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    return $"new {elementType}[] {{ {string.Join(", ", constant.Values.Select(Constant))} }}";
                case TypedConstantKind.Type:
                    return $"typeof({((ITypeSymbol)constant.Value!).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
                case TypedConstantKind.Enum:
                    return $"({constant.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})({Primitive(constant.Value!)})";
                default:
                    return Primitive(constant.Value!);
            }
        }

        private static string Primitive(object value)
        {
            var invariant = CultureInfo.InvariantCulture;
            return value switch
            {
                string text => SymbolDisplay.FormatLiteral(text, quote: true),
                char character => SymbolDisplay.FormatLiteral(character, quote: true),
                bool flag => flag ? "true" : "false",
                float number => float.IsNaN(number) ? "float.NaN"
                    : float.IsPositiveInfinity(number) ? "float.PositiveInfinity"
                    : float.IsNegativeInfinity(number) ? "float.NegativeInfinity"
                    : number.ToString("R", invariant) + "F",
                double number => double.IsNaN(number) ? "double.NaN"
                    : double.IsPositiveInfinity(number) ? "double.PositiveInfinity"
                    : double.IsNegativeInfinity(number) ? "double.NegativeInfinity"
                    : number.ToString("R", invariant) + "D",
                decimal number => number.ToString(invariant) + "M",
                long number => number.ToString(invariant) + "L",
                ulong number => number.ToString(invariant) + "UL",
                uint number => number.ToString(invariant) + "U",
                int number => number.ToString(invariant),
                short number => $"(short){number.ToString(invariant)}",
                ushort number => $"(ushort){number.ToString(invariant)}",
                byte number => $"(byte){number.ToString(invariant)}",
                sbyte number => $"(sbyte){number.ToString(invariant)}",
                _ => Convert.ToString(value, invariant) ?? "default",
            };
        }

        private static bool IsComparableNumeric(ITypeSymbol type) => type.SpecialType is
            SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;

        /// <summary>The value constraint of a DTO member from its Unity attributes, or null.</summary>
        private static Constraint? ReadConstraint(Context ctx, ISymbol member, ITypeSymbol type, Location? location)
        {
            var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var reject = HasAttribute(member, RejectAttribute);

            var range = FindAttribute(member, RangeAttribute);
            var min = FindAttribute(member, MinAttribute);
            if (range != null || min != null)
            {
                if (!IsComparableNumeric(type))
                {
                    ctx.Report(Diagnostics.UnsupportedConstraint, location, range != null ? "Range" : "Min", member.Name,
                        type.ToDisplayString());
                    return null;
                }

                if (range is { ConstructorArguments.Length: 2 })
                {
                    return new Constraint
                    {
                        Kind = "range",
                        Attribute = "Range",
                        Min = Literal(range.ConstructorArguments[0].Value, typeName),
                        Max = Literal(range.ConstructorArguments[1].Value, typeName),
                        Reject = reject,
                    };
                }

                if (min is { ConstructorArguments.Length: 1 })
                {
                    return new Constraint
                    {
                        Kind = "range",
                        Attribute = "Min",
                        Min = Literal(min.ConstructorArguments[0].Value, typeName),
                        Max = $"{typeName}.MaxValue",
                        Reject = reject,
                    };
                }
            }

            var colorUsage = FindAttribute(member, ColorUsageAttribute);
            if (colorUsage != null)
            {
                if (type.ToDisplayString() != ColorType)
                {
                    ctx.Report(Diagnostics.UnsupportedConstraint, location, "ColorUsage", member.Name, type.ToDisplayString());
                    return null;
                }

                var args = colorUsage.ConstructorArguments;
                return new Constraint
                {
                    Kind = "color",
                    Attribute = "ColorUsage",
                    ShowAlpha = args.Length > 0 && args[0].Value is true,
                    Hdr = args.Length > 1 && args[1].Value is true,
                };
            }

            return null;
        }

        /// <summary>A float attribute argument as a literal of the member type.</summary>
        private static string Literal(object? value, string typeName)
        {
            var number = Convert.ToSingle(value, CultureInfo.InvariantCulture);
            return $"({typeName})({number.ToString("R", CultureInfo.InvariantCulture)}F)";
        }

        #endregion

        #region Rendering

        private static string GetHintName(INamedTypeSymbol type) =>
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty).Replace('<', '[').Replace('>', ']');

        private static string FieldTypeName(Mapping mapping)
        {
            if (mapping.Rules.Factory != null) return mapping.Rules.Factory.FieldTypeName;

            return mapping.Rules.Constraint?.Kind switch
            {
                "range" => $"{Global}RangedPersistentField<{mapping.ValueType}>",
                "color" => $"{Global}ColorPersistentField",
                _ => $"{Global}PersistentField<{mapping.ValueType}>",
            };
        }

        /// <summary>The user normalizers as one lambda, or "null".</summary>
        private static string NormalizerLambda(Mapping mapping)
        {
            if (mapping.Rules.Normalizers.Count == 0) return "null";
            return "value => " + ApplyNormalizers(mapping, "value");
        }

        private static string ApplyNormalizers(Mapping mapping, string expression)
        {
            foreach (var normalizer in mapping.Rules.Normalizers)
            {
                expression = $"{normalizer.Name}.Normalize({expression})";
            }

            return expression;
        }

        private static string NewField(Mapping mapping, string defaultValue)
        {
            var rules = mapping.Rules;
            if (rules.Factory != null) return $"{rules.Factory.Name}.Create(this, {defaultValue})";

            var normalize = NormalizerLambda(mapping);
            switch (rules.Constraint?.Kind)
            {
                case "range":
                    var mode = rules.Constraint.Reject ? "Reject" : "Clamp";
                    return $"new {FieldTypeName(mapping)}(this, {defaultValue}, {rules.Constraint.Min}, {rules.Constraint.Max}, " +
                           $"{Global}RangeMode.{mode}, {normalize})";
                case "color":
                    return $"new {FieldTypeName(mapping)}(this, {defaultValue}, {Bool(rules.Constraint.ShowAlpha)}, " +
                           $"{Bool(rules.Constraint.Hdr)}, {normalize})";
                default:
                    return normalize == "null"
                        ? $"new {FieldTypeName(mapping)}(this, {defaultValue})"
                        : $"new {FieldTypeName(mapping)}(this, {defaultValue}, normalize: {normalize})";
            }
        }

        /// <summary>The loaded value, constrained when the field itself does not do it (declared plain field).</summary>
        private static string LoadedValue(Mapping mapping)
        {
            var value = $"data.{mapping.DtoMember}";
            if (mapping.Generated) return value; // the generated field normalizes itself

            var constraint = mapping.Rules.Constraint;
            if (constraint != null && !mapping.DeclaredConstrained)
            {
                value = constraint.Kind == "range"
                    ? $"{Global}PersistentFieldConstraints.Clamp({value}, {constraint.Min}, {constraint.Max})"
                    : $"{Global}PersistentFieldConstraints.Color({value}, {Bool(constraint.ShowAlpha)}, {Bool(constraint.Hdr)})";
            }

            return ApplyNormalizers(mapping, value);
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private static string Render(Context ctx, ITypeSymbol dto, List<Mapping> mappings, bool generateExtract,
            bool generateInject)
        {
            var package = ctx.Package;
            var dtoName = dto.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated/> by VuTH.Persistent.Generators (PayloadMappingGenerator)");
            builder.AppendLine("#nullable disable");
            builder.AppendLine();

            var ns = package.ContainingNamespace;
            var indent = string.Empty;
            if (ns is { IsGlobalNamespace: false })
            {
                builder.AppendLine($"namespace {ns.ToDisplayString()}");
                builder.AppendLine("{");
                indent = "    ";
            }

            // Containing types, outermost first.
            var containers = new Stack<INamedTypeSymbol>();
            for (var type = package.ContainingType; type != null; type = type.ContainingType) containers.Push(type);
            foreach (var container in containers)
            {
                builder.AppendLine($"{indent}partial {Kind(container)} {container.Name}{TypeParameters(container)}");
                builder.AppendLine($"{indent}{{");
                indent += "    ";
            }

            builder.AppendLine($"{indent}partial class {package.Name}{TypeParameters(package)}");
            builder.AppendLine($"{indent}{{");
            var body = indent + "    ";
            var first = true;

            void Separator()
            {
                if (!first) builder.AppendLine();
                first = false;
            }

            // User attributes re-created once (their arguments are constants), used by the fields and InjectPayload.
            if (ctx.Statics.Count > 0)
            {
                Separator();
                foreach (var extension in ctx.Statics)
                {
                    builder.AppendLine($"{body}private static readonly {extension.TypeName} {extension.Name} = {extension.Creation};");
                }
            }

            var generated = mappings.Where(m => m.Generated).ToList();
            if (generated.Count > 0)
            {
                // Created on first use: no constructor code needed, defaults from a new DTO.
                Separator();
                builder.AppendLine($"{body}private {dtoName} __vuthDefaults;");
                builder.AppendLine($"{body}private {dtoName} VuTHPayloadDefaults => __vuthDefaults ??= new {dtoName}();");
                foreach (var mapping in generated)
                {
                    var backing = $"__vuth{mapping.Field}";
                    builder.AppendLine();
                    builder.AppendLine($"{body}private {FieldTypeName(mapping)} {backing};");
                    builder.AppendLine($"{body}/// <summary>Generated from <see cref=\"{dtoName.Replace("global::", string.Empty)}.{mapping.DtoMember}\"/>.</summary>");
                    builder.AppendLine($"{body}public {FieldTypeName(mapping)} {mapping.Field} =>");
                    builder.AppendLine($"{body}    {backing} ??= {NewField(mapping, $"VuTHPayloadDefaults.{mapping.DtoMember}")};");
                }
            }

            if (generateExtract)
            {
                Separator();
                builder.AppendLine($"{body}public override {dtoName} ExtractPayload()");
                builder.AppendLine($"{body}{{");
                builder.AppendLine($"{body}    return new {dtoName}");
                builder.AppendLine($"{body}    {{");
                foreach (var mapping in mappings.Where(m => m.DtoWritable))
                {
                    builder.AppendLine($"{body}        {mapping.DtoMember} = this.{mapping.Field}.Value,");
                }

                builder.AppendLine($"{body}    }};");
                builder.AppendLine($"{body}}}");
            }

            if (generateInject)
            {
                Separator();
                builder.AppendLine($"{body}public override void InjectPayload({dtoName} data)");
                builder.AppendLine($"{body}{{");
                builder.AppendLine($"{body}    if (data == null) return;");
                builder.AppendLine($"{body}    LoadWithoutNotify(() =>");
                builder.AppendLine($"{body}    {{");
                foreach (var mapping in mappings.Where(m => m.DtoReadable))
                {
                    builder.AppendLine($"{body}        this.{mapping.Field}.SetValueWithoutMarkingDirty({LoadedValue(mapping)});");
                }

                builder.AppendLine($"{body}    }});");
                builder.AppendLine($"{body}}}");
            }

            builder.AppendLine($"{indent}}}");
            while (containers.Count > 0)
            {
                containers.Pop();
                indent = indent.Substring(4);
                builder.AppendLine($"{indent}}}");
            }

            if (ns is { IsGlobalNamespace: false }) builder.AppendLine("}");
            return builder.ToString();
        }

        private static string Kind(INamedTypeSymbol type) => type.TypeKind switch
        {
            TypeKind.Struct => type.IsRecord ? "record struct" : "struct",
            TypeKind.Interface => "interface",
            _ => type.IsRecord ? "record" : "class",
        };

        private static string TypeParameters(INamedTypeSymbol type) =>
            type.TypeParameters.Length == 0 ? string.Empty : "<" + string.Join(", ", type.TypeParameters.Select(t => t.Name)) + ">";

        #endregion
    }
}
