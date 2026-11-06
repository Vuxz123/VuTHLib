using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace VuTH.Persistent.Generators
{
    /// <summary>
    /// Incremental pipeline: <c>ForAttributeWithMetadataName</c> finds the marked packages, <see cref="PackageEmitter"/>
    /// turns each into a <see cref="GenerationResult"/> made of plain values (source text, diagnostic data), and only a
    /// changed result reaches the output step. No symbol crosses a pipeline step, so unchanged packages are not regenerated.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class PayloadMappingGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // [GeneratePersistentFields] includes the mapping: a class with both is handled once, by the fields pipeline.
            var mapping = context.SyntaxProvider.ForAttributeWithMetadataName(
                    PackageEmitter.MappingAttribute,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ctx.TargetSymbol is INamedTypeSymbol package &&
                                        !PackageEmitter.HasAttribute(package, PackageEmitter.FieldsAttribute)
                        ? PackageEmitter.Emit(package, ctx.SemanticModel.Compilation, generateFields: false, ct)
                        : null)
                .Where(static result => result != null);

            var fields = context.SyntaxProvider.ForAttributeWithMetadataName(
                    PackageEmitter.FieldsAttribute,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ctx.TargetSymbol is INamedTypeSymbol package
                        ? PackageEmitter.Emit(package, ctx.SemanticModel.Compilation, generateFields: true, ct)
                        : null)
                .Where(static result => result != null);

            context.RegisterSourceOutput(mapping, static (spc, result) => Output(spc, result!));
            context.RegisterSourceOutput(fields, static (spc, result) => Output(spc, result!));
        }

        private static void Output(SourceProductionContext context, GenerationResult result)
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            if (result.Source != null)
            {
                context.AddSource(result.HintName, SourceText.From(result.Source, Encoding.UTF8));
            }
        }
    }
}
