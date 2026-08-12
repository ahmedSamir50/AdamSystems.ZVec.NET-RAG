using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ZVec.Extensions.VectorData.SourceGenerator;

/// <summary>
/// Incremental Roslyn Source Generator that produces zero-reflection static metadata mappers
/// for POCOs annotated with Microsoft.Extensions.VectorData attributes.
/// </summary>
/// <remarks>
/// <code>
/// ┌─────────────────────────────────────────────────────────────┐
/// │               Annotated [VectorStore] POCO                  │
/// ├─────────────────────────────────────────────────────────────┤
/// │            ZVecRecordMetadataGenerator (Roslyn SG)          │
/// ├─────────────────────────────────────────────────────────────┤
/// │   Emits &lt;Class&gt;ZVecMetadataMapper.g.cs (0-Reflection AOT)  │
/// └─────────────────────────────────────────────────────────────┘
/// </code>
/// </remarks>
[Generator]
public sealed class ZVecRecordMetadataGenerator : IIncrementalGenerator
{
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

    private static bool IsCandidateClass(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl) return false;
        return classDecl.AttributeLists
                   .SelectMany(al => al.Attributes)
                   .Any(a => a.Name.ToString().Contains("VectorStore"))
               || classDecl.Members.OfType<PropertyDeclarationSyntax>()
                   .Any(p => p.AttributeLists.SelectMany(al => al.Attributes)
                       .Any(a => a.Name.ToString().Contains("VectorStore")));
    }

    private static RecordModel? GetClassForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        if (symbol == null) return null;
        if (symbol.ContainingType != null) return null; // Skip nested classes
        if (symbol.ContainingNamespace.IsGlobalNamespace) return null;

        var properties = symbol.GetMembers().OfType<IPropertySymbol>().ToList();
        string? keyPropName = null;
        string? vectorPropName = null;
        int vectorDimensions = 0;
        var dataPropNames = new List<string>();

        foreach (var p in properties)
        {
            foreach (var attr in p.GetAttributes())
            {
                string attrName = attr.AttributeClass?.Name ?? string.Empty;
                if (attrName.Contains("VectorStoreKey"))
                {
                    keyPropName = p.Name;
                }
                else if (attrName.Contains("VectorStoreVector"))
                {
                    vectorPropName = p.Name;
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int dims)
                    {
                        vectorDimensions = dims;
                    }
                }
                else if (attrName.Contains("VectorStoreData"))
                {
                    dataPropNames.Add(p.Name);
                }
            }
        }

        if (keyPropName == null && vectorPropName == null && dataPropNames.Count == 0)
            return null;

        string namespaceName = symbol.ContainingNamespace.ToDisplayString();
        string className = symbol.Name;

        return new RecordModel(namespaceName, className, keyPropName, vectorPropName, vectorDimensions, dataPropNames);
    }

    private static void GenerateSource(SourceProductionContext context, RecordModel model)
    {
        var propsSb = new StringBuilder();
        propsSb.AppendLine("    public static VectorStoreCollectionDefinition Definition { get; } = new VectorStoreCollectionDefinition");
        propsSb.AppendLine("    {");
        propsSb.AppendLine("        Properties = new VectorStoreRecordProperty[]");
        propsSb.AppendLine("        {");

        if (model.KeyPropName != null)
        {
            propsSb.AppendLine($"            new VectorStoreRecordKeyProperty(\"{model.KeyPropName}\", typeof(string)),");
        }

        if (model.VectorPropName != null)
        {
            propsSb.AppendLine($"            new VectorStoreRecordVectorProperty(\"{model.VectorPropName}\", typeof(ReadOnlyMemory<float>), {model.VectorDimensions}),");
        }

        foreach (var dataProp in model.DataPropNames)
        {
            propsSb.AppendLine($"            new VectorStoreRecordDataProperty(\"{dataProp}\", typeof(object)),");
        }

        propsSb.AppendLine("        }");
        propsSb.AppendLine("    };");

        string hintName = $"{model.NamespaceName.Replace('.', '_')}_{model.ClassName}ZVecMetadataMapper.g.cs";

        string source = $@"// <auto-generated/>
#nullable enable

using System;
using Microsoft.Extensions.VectorData;

namespace {model.NamespaceName};

/// <summary>
/// Generated zero-reflection static metadata mapper for <see cref=""{model.ClassName}""/>.
/// </summary>
public static class {model.ClassName}ZVecMetadataMapper
{{
{propsSb}
}}
";

        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }

    private readonly struct RecordModel
    {
        public RecordModel(
            string namespaceName,
            string className,
            string? keyPropName,
            string? vectorPropName,
            int vectorDimensions,
            IReadOnlyList<string> dataPropNames)
        {
            NamespaceName = namespaceName;
            ClassName = className;
            KeyPropName = keyPropName;
            VectorPropName = vectorPropName;
            VectorDimensions = vectorDimensions;
            DataPropNames = dataPropNames;
        }

        public string NamespaceName { get; }
        public string ClassName { get; }
        public string? KeyPropName { get; }
        public string? VectorPropName { get; }
        public int VectorDimensions { get; }
        public IReadOnlyList<string> DataPropNames { get; }
    }
}
