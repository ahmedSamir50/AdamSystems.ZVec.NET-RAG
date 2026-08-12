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
/// │   • VectorStoreCollectionDefinition (key/vector/data props) │
/// │   • IZVecRecordMapper&lt;TRecord&gt; implementation              │
/// │   • [ModuleInitializer] auto-registration                   │
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
        PropertyModel? keyProp = null;
        PropertyModel? vectorProp = null;
        int vectorDimensions = 0;
        var dataProps = new List<PropertyModel>();

        foreach (var p in properties)
        {
            foreach (var attr in p.GetAttributes())
            {
                string attrName = attr.AttributeClass?.Name ?? string.Empty;
                if (attrName.Contains("VectorStoreKey"))
                {
                    keyProp = new PropertyModel(p.Name, p.Type.ToDisplayString(), p.Type.SpecialType);
                }
                else if (attrName.Contains("VectorStoreVector"))
                {
                    vectorProp = new PropertyModel(p.Name, p.Type.ToDisplayString(), p.Type.SpecialType);
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int dims)
                    {
                        vectorDimensions = dims;
                    }
                }
                else if (attrName.Contains("VectorStoreData"))
                {
                    dataProps.Add(new PropertyModel(p.Name, p.Type.ToDisplayString(), p.Type.SpecialType));
                }
            }
        }

        if (keyProp == null && vectorProp == null && dataProps.Count == 0)
            return null;

        string namespaceName = symbol.ContainingNamespace.ToDisplayString();
        string className = symbol.Name;

        return new RecordModel(
            namespaceName, className,
            keyProp, vectorProp, vectorDimensions, dataProps);
    }

    private static void GenerateSource(SourceProductionContext context, RecordModel model)
    {
        var propertiesList = new StringBuilder();
        if (model.KeyProp.HasValue)
        {
            propertiesList.AppendLine($"            new VectorStoreRecordKeyProperty(\"{model.KeyProp.Value.Name}\", typeof({model.KeyProp.Value.FullyQualifiedType})),");
        }
        if (model.VectorProp.HasValue)
        {
            propertiesList.AppendLine($"            new VectorStoreRecordVectorProperty(\"{model.VectorProp.Value.Name}\", typeof({model.VectorProp.Value.FullyQualifiedType}), {model.VectorDimensions}),");
        }
        foreach (var dataProp in model.DataProps)
        {
            propertiesList.AppendLine($"            new VectorStoreRecordDataProperty(\"{dataProp.Name}\", typeof({dataProp.FullyQualifiedType})),");
        }

        var toDocKey = model.KeyProp.HasValue
            ? $"doc.Id = record.{model.KeyProp.Value.Name}?.ToString() ?? string.Empty;"
            : string.Empty;

        var toDocData = new StringBuilder();
        foreach (var dataProp in model.DataProps)
        {
            toDocData.AppendLine($"            doc.Fields[model.Fields.Find(f => f.PropertyName == \"{dataProp.Name}\")!.StorageName] = (object?)record.{dataProp.Name};");
        }

        var toDocVector = model.VectorProp.HasValue
            ? $$"""
            var vecStorage = model.Vectors.Find(v => v.PropertyName == "{{model.VectorProp.Value.Name}}")!.StorageName;
            doc.DenseVectors[vecStorage] = record.{{model.VectorProp.Value.Name}};
"""
            : string.Empty;

        var fromDocKey = model.KeyProp.HasValue
            ? $"record.{model.KeyProp.Value.Name} = doc.Id;"
            : string.Empty;

        var fromDocData = new StringBuilder();
        foreach (var dataProp in model.DataProps)
        {
            fromDocData.AppendLine($$"""
            var {{dataProp.Name}}Storage = model.Fields.Find(f => f.PropertyName == "{{dataProp.Name}}")!.StorageName;
            if (doc.Fields.TryGetValue({{dataProp.Name}}Storage, out var {{dataProp.Name}}Val) && {{dataProp.Name}}Val != null)
                record.{{dataProp.Name}} = ({{dataProp.FullyQualifiedType}}){{dataProp.Name}}Val;
""");
        }

        var fromDocVector = model.VectorProp.HasValue
            ? $$"""
            var {{model.VectorProp.Value.Name}}Storage = model.Vectors.Find(v => v.PropertyName == "{{model.VectorProp.Value.Name}}")!.StorageName;
            if (doc.DenseVectors.TryGetValue({{model.VectorProp.Value.Name}}Storage, out var {{model.VectorProp.Value.Name}}Dense))
                record.{{model.VectorProp.Value.Name}} = {{model.VectorProp.Value.Name}}Dense;
"""
            : string.Empty;

        string source = $$"""
// <auto-generated/>
#nullable enable

using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;

namespace {{model.NamespaceName}};

/// <summary>
/// Generated zero-reflection static metadata mapper for <see cref="{{model.ClassName}}"/>.
/// Emits VectorStoreCollectionDefinition, IZVecRecordMapper&lt;T&gt; implementation,
/// and ModuleInitializer registration.
/// </summary>
public static class {{model.ClassName}}ZVecMetadataMapper
{
    /// <summary>Generated collection definition.</summary>
    public static VectorStoreCollectionDefinition Definition { get; } = new VectorStoreCollectionDefinition
    {
        Properties = new VectorStoreRecordProperty[]
        {
{{propertiesList.ToString().TrimEnd()}}
        }
    };

    /// <summary>Zero-reflection mapper for {{model.ClassName}}.</summary>
    public sealed class Mapper : IZVecRecordMapper<{{model.ClassName}}>
    {
        /// <inheritdoc />
        public ZVecDoc ToDoc({{model.ClassName}} record, ZVecTypeModel model)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (model == null) throw new ArgumentNullException(nameof(model));
            var doc = new ZVecDoc();
            {{toDocKey}}
{{toDocData.ToString().TrimEnd()}}
            {{toDocVector}}
            return doc;
        }

        /// <inheritdoc />
        public {{model.ClassName}} FromDoc(ZVecDoc doc, ZVecTypeModel model)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (model == null) throw new ArgumentNullException(nameof(model));
            var record = new {{model.ClassName}}();
            {{fromDocKey}}
{{fromDocData.ToString().TrimEnd()}}
            {{fromDocVector}}
            return record;
        }
    }

    internal static class {{model.ClassName}}MapperRegistration
    {
        [ModuleInitializer]
        internal static void Register()
        {
            ZVecRecordMapperRegistry.Register<{{model.ClassName}}>(new Mapper());
        }
    }
}
""";

        string hintName = $"{model.NamespaceName.Replace('.', '_')}_{model.ClassName}ZVecMetadataMapper.g.cs";
        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }

    private readonly struct PropertyModel
    {
        public PropertyModel(string name, string fullyQualifiedType, SpecialType specialType)
        {
            Name = name;
            FullyQualifiedType = fullyQualifiedType;
            SpecialType = specialType;
        }
        public string Name { get; }
        public string FullyQualifiedType { get; }
        public SpecialType SpecialType { get; }
    }

    private readonly struct RecordModel
    {
        public RecordModel(
            string namespaceName,
            string className,
            PropertyModel? keyPropName,
            PropertyModel? vectorPropName,
            int vectorDimensions,
            IReadOnlyList<PropertyModel> dataPropNames)
        {
            NamespaceName = namespaceName;
            ClassName = className;
            KeyProp = keyPropName;
            VectorProp = vectorPropName;
            VectorDimensions = vectorDimensions;
            DataProps = dataPropNames;
        }

        public string NamespaceName { get; }
        public string ClassName { get; }
        public PropertyModel? KeyProp { get; }
        public PropertyModel? VectorProp { get; }
        public int VectorDimensions { get; }
        public IReadOnlyList<PropertyModel> DataProps { get; }
    }
}
