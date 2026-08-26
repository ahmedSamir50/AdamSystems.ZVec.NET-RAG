using Microsoft.CodeAnalysis;

namespace ZVec.Extensions.VectorData.SourceGenerator;

/// <summary>
/// Incremental Roslyn Source Generator that produces zero-reflection static metadata mappers
/// and collection schema factories for POCOs annotated with Microsoft.Extensions.VectorData attributes.
/// </summary>
/// <remarks>
/// <code>
/// ┌─────────────────────────────────────────────────────────────┐
/// │               Annotated [VectorStore] POCO                  │
/// ├─────────────────────────────────────────────────────────────┤
/// │            ZVecRecordMetadataGenerator (Roslyn SG)          │
/// ├─────────────────────────────────────────────────────────────┤
/// │   Emits &lt;Class&gt;ZVecMetadataMapper.g.cs (0-Reflection AOT)  │
/// │   • VectorStoreCollectionDefinition (key/vector/data props) │
/// │   • BuildSchema(string) via AddField / AddVector            │
/// │   • IZVecRecordMapper&lt;TRecord&gt; implementation              │
/// │   • [ModuleInitializer] auto-registration                   │
/// └─────────────────────────────────────────────────────────────┘
/// </code>
/// </remarks>
[Generator]
public sealed partial class ZVecRecordMetadataGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateClass(s),
                transform: static (ctx, _) => GetClassForGeneration(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(classDeclarations, static (spc, source) =>
        {
            if (source != null)
            {
                GenerateSource(spc, source.Value);
            }
        });
    }
}
