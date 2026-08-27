using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZVec.Extensions.VectorData.SourceGenerator;

public sealed partial class ZVecRecordMetadataGenerator
{
    private static bool IsCandidateClass(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl) return false;
        return classDecl.AttributeLists
                   .SelectMany(al => al.Attributes)
                   .Any(a => a.Name.ToString().Contains(GeneratorMetadataNames.VectorStoreAttributeToken))
               || classDecl.Members.OfType<PropertyDeclarationSyntax>()
                   .Any(p => p.AttributeLists.SelectMany(al => al.Attributes)
                       .Any(a => a.Name.ToString().Contains(GeneratorMetadataNames.VectorStoreAttributeToken)));
    }

    private static RecordModel? GetClassForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        if (symbol == null) return null;
        if (symbol.ContainingType != null) return null;
        if (symbol.ContainingNamespace.IsGlobalNamespace) return null;

        PropertyModel? keyProp = null;
        PropertyModel? vectorProp = null;
        var dataProps = new List<PropertyModel>();

        foreach (var property in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            foreach (var attr in property.GetAttributes())
            {
                string attrName = attr.AttributeClass?.Name ?? string.Empty;
                if (attrName.Contains(GeneratorMetadataNames.VectorStoreKeyToken))
                {
                    keyProp = CreatePropertyModel(property, attr);
                }
                else if (attrName.Contains(GeneratorMetadataNames.VectorStoreVectorToken))
                {
                    vectorProp = CreatePropertyModel(property, attr, isVector: true);
                }
                else if (attrName.Contains(GeneratorMetadataNames.VectorStoreDataToken))
                {
                    dataProps.Add(CreatePropertyModel(property, attr, isData: true));
                }
            }
        }

        if (keyProp == null && vectorProp == null && dataProps.Count == 0)
            return null;

        return new RecordModel(
            symbol.ContainingNamespace.ToDisplayString(),
            symbol.Name,
            keyProp,
            vectorProp,
            dataProps);
    }

    private static PropertyModel CreatePropertyModel(
        IPropertySymbol property,
        AttributeData attribute,
        bool isVector = false,
        bool isData = false)
    {
        string storageName = property.Name;
        int vectorDimensions = 0;
        bool isFullTextIndexed = false;
        bool isIndexed = false;
        string? indexKind = null;
        string? distanceFunctionValue = null;

        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == GeneratorMetadataNames.StorageNameArgument &&
                named.Value.Value is string storageOverride &&
                !string.IsNullOrWhiteSpace(storageOverride))
            {
                storageName = storageOverride;
            }
            else if (named.Key == GeneratorMetadataNames.IsFullTextIndexedArgument && named.Value.Value is bool fts)
            {
                isFullTextIndexed = fts;
            }
            else if (named.Key == GeneratorMetadataNames.IsIndexedArgument && named.Value.Value is bool indexed)
            {
                isIndexed = indexed;
            }
            else if (named.Key == GeneratorMetadataNames.IndexKindArgument && named.Value.Value != null)
            {
                indexKind = named.Value.Value.ToString();
            }
            else if (named.Key == GeneratorMetadataNames.DistanceFunctionArgument &&
                     named.Value.Value is string distanceFunction)
            {
                distanceFunctionValue = distanceFunction;
            }
        }

        if (isVector && attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int dims)
        {
            vectorDimensions = dims;
        }

        if (isData)
        {
            foreach (var ftsAttr in property.GetAttributes())
            {
                if (ftsAttr.AttributeClass?.Name is GeneratorMetadataNames.ZVecFullTextSearchAttributeName
                    or GeneratorMetadataNames.ZVecFullTextSearchName)
                {
                    isFullTextIndexed = true;
                    if (ftsAttr.NamedArguments
                            .FirstOrDefault(n => n.Key == GeneratorMetadataNames.IsFullTextIndexedArgument)
                            .Value.Value is bool ftsEnabled)
                    {
                        isFullTextIndexed = ftsEnabled;
                    }
                }
            }
        }

        return new PropertyModel(
            property.Name,
            storageName,
            property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            property.Type.SpecialType,
            vectorDimensions,
            isFullTextIndexed,
            isIndexed,
            indexKind,
            distanceFunctionValue);
    }

    private readonly struct PropertyModel
    {
        public PropertyModel(
            string name,
            string storageName,
            string fullyQualifiedType,
            SpecialType specialType,
            int vectorDimensions,
            bool isFullTextIndexed,
            bool isIndexed,
            string? indexKind,
            string? distanceFunctionValue)
        {
            Name = name;
            StorageName = storageName;
            FullyQualifiedType = fullyQualifiedType;
            SpecialType = specialType;
            VectorDimensions = vectorDimensions;
            IsFullTextIndexed = isFullTextIndexed;
            IsIndexed = isIndexed;
            IndexKind = indexKind;
            DistanceFunctionValue = distanceFunctionValue;
        }

        public string Name { get; }
        public string StorageName { get; }
        public string FullyQualifiedType { get; }
        public SpecialType SpecialType { get; }
        public int VectorDimensions { get; }
        public bool IsFullTextIndexed { get; }
        public bool IsIndexed { get; }
        public string? IndexKind { get; }
        public string? DistanceFunctionValue { get; }
    }

    private readonly struct RecordModel
    {
        public RecordModel(
            string namespaceName,
            string className,
            PropertyModel? keyProp,
            PropertyModel? vectorProp,
            IReadOnlyList<PropertyModel> dataProps)
        {
            NamespaceName = namespaceName;
            ClassName = className;
            KeyProp = keyProp;
            VectorProp = vectorProp;
            DataProps = dataProps;
        }

        public string NamespaceName { get; }
        public string ClassName { get; }
        public PropertyModel? KeyProp { get; }
        public PropertyModel? VectorProp { get; }
        public IReadOnlyList<PropertyModel> DataProps { get; }
    }
}
