using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.SourceGenerator;
using Xunit;

namespace ZVec.Extensions.VectorData.SourceGenerator.Tests;

/// <summary>
/// TDD Unit tests for Roslyn Incremental Source Generator ZVecRecordMetadataGenerator.
/// </summary>
public sealed class ZVecRecordMetadataGeneratorTests
{
    [Fact]
    public void Generator_Instantiates_Successfully()
    {
        var generator = new ZVecRecordMetadataGenerator();
        Assert.NotNull(generator);
    }

    [Fact]
    public void Driver_ExecutesGeneratorOnEmptyCompilation_NoErrors()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            Array.Empty<SyntaxTree>(),
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ZVecRecordMetadataGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var result = driver.GetRunResult();

        Assert.NotNull(result);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Driver_GeneratesMetadataMapper_WhenAnnotatedClassExists()
    {
        string userSource = @"
using System;
using Microsoft.Extensions.VectorData;

namespace TestNamespace;

public sealed class SampleDocumentRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }
}
";

        var syntaxTree = CSharpSyntaxTree.ParseText(userSource, cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(VectorStoreKeyAttribute).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ZVecRecordMetadataGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var result = driver.GetRunResult();

        Assert.NotEmpty(result.GeneratedTrees);
        string generatedText = result.GeneratedTrees[0].ToString();
        Assert.Contains("SampleDocumentRecordZVecMetadataMapper", generatedText);
        Assert.Contains("VectorStoreCollectionDefinition", generatedText);
    }

    [Fact]
    public void Driver_IgnoresStructType_NoGeneratedSources()
    {
        string userSource = @"
using System;
using Microsoft.Extensions.VectorData;

namespace TestNamespace;

public struct SampleStructRecord
{
    [VectorStoreKey]
    public string Id { get; set; }

    [VectorStoreData]
    public string Title { get; set; }
}
";

        var syntaxTree = CSharpSyntaxTree.ParseText(userSource, cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(VectorStoreKeyAttribute).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ZVecRecordMetadataGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var result = driver.GetRunResult();

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Driver_GeneratesMappersForMultipleClasses_WhenMultipleAnnotatedClassesExist()
    {
        string userSource = @"
using System;
using Microsoft.Extensions.VectorData;

namespace TestNamespace;

public sealed class DocA
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;
}

public sealed class DocB
{
    [VectorStoreKey]
    public string Key { get; set; } = string.Empty;
}
";

        var syntaxTree = CSharpSyntaxTree.ParseText(userSource, cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(VectorStoreKeyAttribute).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ZVecRecordMetadataGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var result = driver.GetRunResult();

        Assert.Equal(2, result.GeneratedTrees.Length);
    }

    [Fact]
    public void Driver_SkipsGlobalNamespaceClass_NoGeneratedSources()
    {
        // A POCO declared without a namespace declaration would cause the generator
        // to emit 'namespace <global namespace>;' which is invalid C#.
        // The fix skips classes in the global namespace entirely.
        string userSource = @"
using System;
using Microsoft.Extensions.VectorData;

public sealed class GlobalNamespaceRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }
}
";

        var syntaxTree = CSharpSyntaxTree.ParseText(userSource, cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(VectorStoreKeyAttribute).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ZVecRecordMetadataGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var result = driver.GetRunResult();

        Assert.Empty(result.GeneratedTrees);
    }
}
