using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZVec.Extensions.VectorData.Analyzers;

/// <summary>
/// Emits ZVEC001 when a VectorStore record type lacks a source-generated mapper registration.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ZVecRecordMapperAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic id for missing source-generated mapper.</summary>
    public const string MissingMapperDiagnosticId = "ZVEC001";

    private static readonly DiagnosticDescriptor MissingMapperRule = new(
        id: MissingMapperDiagnosticId,
        title: "Vector store record lacks source-generated mapper",
        messageFormat: "Type '{0}' is decorated with VectorStore mapping attributes but has no generated IZVecRecordMapper registration",
        category: "ZVec.AOT",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Non-source-generated record mappers rely on reflection and may be trimmed under Native AOT.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MissingMapperRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDeclaration)
            return;

        if (!HasVectorStoreRecordAttributes(classDeclaration))
            return;

        var semanticModel = context.SemanticModel;
        var symbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (symbol == null)
            return;

        if (HasGeneratedMapperRegistration(symbol))
            return;

        var diagnostic = Diagnostic.Create(
            MissingMapperRule,
            classDeclaration.Identifier.GetLocation(),
            symbol.ToDisplayString());

        context.ReportDiagnostic(diagnostic);
    }

    private static bool HasVectorStoreRecordAttributes(ClassDeclarationSyntax classDeclaration)
    {
        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = attribute.Name.ToString();
                if (name is AnalyzerMetadataNames.VectorStoreRecord
                    or AnalyzerMetadataNames.VectorStoreRecordAttribute
                    or AnalyzerMetadataNames.ZVecId
                    or AnalyzerMetadataNames.ZVecIdAttribute
                    or AnalyzerMetadataNames.VectorStoreKey
                    or AnalyzerMetadataNames.VectorStoreKeyAttribute)
                    return true;
            }
        }

        return false;
    }

    private static bool HasGeneratedMapperRegistration(INamedTypeSymbol symbol)
    {
        var expectedTypeName = $"{symbol.Name}{AnalyzerMetadataNames.GeneratedMapperSuffix}";

        foreach (var member in symbol.ContainingNamespace.GetMembers())
        {
            if (member is INamedTypeSymbol named && named.Name == expectedTypeName)
                return true;
        }

        foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            var root = syntaxRef.SyntaxTree.GetRoot();
            if (root.ToFullString().Contains(expectedTypeName, StringComparison.Ordinal))
                return true;
        }

        return symbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.Name is AnalyzerMetadataNames.GeneratedCode
                or AnalyzerMetadataNames.GeneratedCodeAttribute ||
            attr.AttributeClass?.ToDisplayString().Contains(AnalyzerMetadataNames.GeneratorTypeName, StringComparison.Ordinal) == true);
    }
}
