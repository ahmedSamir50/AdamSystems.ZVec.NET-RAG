using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZVec.Extensions.VectorData.Analyzers;

/// <summary>
/// Emits ZVEC002 when reflection APIs are used outside approved fallback paths.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ZVecReflectionHotPathAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic id for reflection hot-path usage.</summary>
    public const string ReflectionHotPathDiagnosticId = "ZVEC002";

    private static readonly DiagnosticDescriptor ReflectionHotPathRule = new(
        id: ReflectionHotPathDiagnosticId,
        title: "Reflection API used in non-fallback path",
        messageFormat: "Avoid '{0}' in hot paths; use source-generated mappers or approved fallback branches",
        category: "ZVec.AOT",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Reflection in VectorData hot paths breaks Native AOT trimming guarantees.");

    private static readonly HashSet<string> ReflectionMembers = new(AnalyzerMetadataNames.ReflectionMemberNames, StringComparer.Ordinal);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(ReflectionHotPathRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return;

        if (IsApprovedFallbackPath(context.SemanticModel, invocation))
            return;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol method)
            return;

        if (!ReflectionMembers.Contains(method.Name))
            return;

        if (method.ContainingType?.Name is AnalyzerMetadataNames.TypeClassName
            or AnalyzerMetadataNames.ActivatorClassName
            or AnalyzerMetadataNames.AttributeClassName)
        {
            var diagnostic = Diagnostic.Create(
                ReflectionHotPathRule,
                invocation.GetLocation(),
                $"{method.ContainingType.Name}.{method.Name}");

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsApprovedFallbackPath(SemanticModel semanticModel, SyntaxNode node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (current is BaseMethodDeclarationSyntax methodDeclaration)
            {
                var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
                if (methodSymbol != null && HasApprovedFallbackAnnotation(methodSymbol))
                    return true;
            }

            if (current is TypeDeclarationSyntax)
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(current);
                if (typeSymbol != null && HasApprovedFallbackAnnotation(typeSymbol))
                    return true;
            }
        }

        return false;
    }

    private static bool HasApprovedFallbackAnnotation(ISymbol symbol) =>
        symbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.Name is AnalyzerMetadataNames.RequiresUnreferencedCodeAttribute
                or AnalyzerMetadataNames.RequiresUnreferencedCode
                or AnalyzerMetadataNames.RequiresDynamicCodeAttribute
                or AnalyzerMetadataNames.RequiresDynamicCode);
}
