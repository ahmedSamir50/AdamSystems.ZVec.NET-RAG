using System;
using System.Collections.Generic;
using System.Linq;
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
                if (name is "VectorStoreRecord" or "VectorStoreRecordAttribute" or "ZVecId" or "ZVecIdAttribute" or "VectorStoreKey" or "VectorStoreKeyAttribute")
                    return true;
            }
        }

        return false;
    }

    private static bool HasGeneratedMapperRegistration(INamedTypeSymbol symbol)
    {
        // The source generator (ZVecRecordMetadataGenerator) emits a class named
        // `{ClassName}ZVecMetadataMapper`. Match that exact name so correctly
        // generated records are not flagged as missing a mapper.
        var expectedTypeName = $"{symbol.Name}ZVecMetadataMapper";

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
            attr.AttributeClass?.Name is "GeneratedCode" or "GeneratedCodeAttribute" ||
            attr.AttributeClass?.ToDisplayString().Contains("ZVecRecordMetadataGenerator", StringComparison.Ordinal) == true);
    }
}

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

    private static readonly HashSet<string> ReflectionMembers = new(StringComparer.Ordinal)
    {
        "GetProperties",
        "GetProperty",
        "GetField",
        "GetFields",
        "GetCustomAttribute",
        "GetCustomAttributes",
        "CreateInstance"
    };

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

        if (method.ContainingType?.Name is "Type" or "Activator" or "Attribute")
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
            attr.AttributeClass?.Name is "RequiresUnreferencedCodeAttribute" or "RequiresUnreferencedCode" or
            "RequiresDynamicCodeAttribute" or "RequiresDynamicCode");
}
