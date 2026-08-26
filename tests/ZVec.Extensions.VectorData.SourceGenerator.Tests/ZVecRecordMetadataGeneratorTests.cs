using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.SourceGenerator;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.SourceGenerator.Tests;

/// <summary>
/// TDD unit tests for Roslyn incremental source generator <see cref="ZVecRecordMetadataGenerator"/>.
/// </summary>
public sealed class ZVecRecordMetadataGeneratorTests
{
    private static (GeneratorDriver Driver, Compilation Compilation) RunGeneratorOnSource(string userSource)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(userSource);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            ReferenceAssemblies.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ZVecRecordMetadataGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        return (driver, compilation);
    }

    private static string GetGeneratedText(GeneratorDriver driver)
    {
        var result = driver.GetRunResult();
        Assert.NotEmpty(result.GeneratedTrees);
        return string.Join(Environment.NewLine, result.GeneratedTrees.Select(t => t.ToString()));
    }

    [Fact]
    public void Driver_ExecutesGeneratorOnEmptyCompilation_NoErrors()
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            Array.Empty<SyntaxTree>(),
            ReferenceAssemblies.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ZVecRecordMetadataGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var result = driver.GetRunResult();

        Assert.NotNull(result);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Driver_GeneratesAddFieldAndAddVector_WhenAnnotatedClassExists()
    {
        const string userSource = """
using System;
using Microsoft.Extensions.VectorData;

namespace TestNamespace;

public sealed class SampleDocumentRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true, IsFullTextIndexed = true)]
    public string Title { get; set; } = string.Empty;

    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }
}
""";

        var (driver, _) = RunGeneratorOnSource(userSource);
        string generatedText = GetGeneratedText(driver);

        Assert.Contains("SampleDocumentRecordZVecMetadataMapper", generatedText);
        Assert.Contains("builder.AddVector(", generatedText);
        Assert.Contains("ZVecFtsIndexParam", generatedText);
        Assert.Contains("using ZVec.Extensions.VectorData.Mapping;", generatedText);
        Assert.Contains("ZVecCollectionSchemaRegistry.Register", generatedText);
        Assert.Contains("ModuleInitializer", generatedText);
        Assert.DoesNotContain("model.Fields.Find", generatedText);
    }

    [Fact]
    public void Driver_GeneratedMapper_RoundTripsRecordWithoutReflectionLookups()
    {
        const string userSource = """
using System;
using Microsoft.Extensions.VectorData;

namespace TestNamespace;

public sealed class RoundTripRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreVector(4)]
    public ReadOnlyMemory<float> Vector { get; set; }
}
""";

        var (driver, compilation) = RunGeneratorOnSource(userSource);
        string generatedText = GetGeneratedText(driver);

        var generatedTree = CSharpSyntaxTree.ParseText(generatedText, cancellationToken: TestContext.Current.CancellationToken);
        var updatedCompilation = CSharpCompilation.Create(
            compilation.AssemblyName,
            compilation.SyntaxTrees.Append(generatedTree),
            ReferenceAssemblies.All,
            (CSharpCompilationOptions)compilation.Options);

        using var peStream = new MemoryStream();
        var emitResult = updatedCompilation.Emit(peStream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics.Select(d => d.ToString())));

        var assembly = System.Reflection.Assembly.Load(peStream.ToArray());
        var recordType = assembly.GetType("TestNamespace.RoundTripRecord", throwOnError: true)!;
        var mapperType = assembly.GetType("TestNamespace.RoundTripRecordZVecMetadataMapper+Mapper", throwOnError: true)!;
        var mapper = Activator.CreateInstance(mapperType)!;

        var record = Activator.CreateInstance(recordType)!;
        recordType.GetProperty("Id")!.SetValue(record, "doc-1");
        recordType.GetProperty("Title")!.SetValue(record, "Hello");
        recordType.GetProperty("Vector")!.SetValue(record, new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f, 0.4f }));

        var toDoc = mapperType.GetMethod("ToDoc")!;
        var doc = (ZVecDoc)toDoc.Invoke(mapper, new[] { record, null! })!;
        Assert.Equal("doc-1", doc.Id);
        Assert.Equal("Hello", doc.Fields["Title"]);
        Assert.Equal(new float[] { 0.1f, 0.2f, 0.3f, 0.4f }, doc.DenseVectors["Vector"].ToArray());

        var fromDoc = mapperType.GetMethod("FromDoc")!;
        var restored = fromDoc.Invoke(mapper, new object[] { doc, null! })!;
        Assert.Equal("doc-1", recordType.GetProperty("Id")!.GetValue(restored));
        Assert.Equal("Hello", recordType.GetProperty("Title")!.GetValue(restored));
        Assert.Equal(new float[] { 0.1f, 0.2f, 0.3f, 0.4f }, ((ReadOnlyMemory<float>)recordType.GetProperty("Vector")!.GetValue(restored)!).ToArray());
    }

    [Fact]
    public void Driver_IgnoresStructType_NoGeneratedSources()
    {
        const string userSource = """
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
""";

        var (driver, _) = RunGeneratorOnSource(userSource);
        var result = driver.GetRunResult();
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Driver_GeneratesMappersForMultipleClasses_WhenMultipleAnnotatedClassesExist()
    {
        const string userSource = """
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
""";

        var (driver, _) = RunGeneratorOnSource(userSource);
        var result = driver.GetRunResult();
        Assert.Equal(2, result.GeneratedTrees.Length);
    }

    [Fact]
    public void Driver_SkipsGlobalNamespaceClass_NoGeneratedSources()
    {
        const string userSource = """
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
""";

        var (driver, _) = RunGeneratorOnSource(userSource);
        var result = driver.GetRunResult();
        Assert.Empty(result.GeneratedTrees);
    }

    private sealed class RoundTripRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public ReadOnlyMemory<float> Vector { get; set; }
    }

    private static class ReferenceAssemblies
    {
        public static MetadataReference[] All => CreateTrustedPlatformReferences()
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(VectorStoreKeyAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ZVecDoc).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IZVecRecordMapper<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ZVecCollectionSchemaRegistry).Assembly.Location),
            })
            .Distinct()
            .ToArray();

        private static IEnumerable<MetadataReference> CreateTrustedPlatformReferences()
        {
            if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string paths)
            {
                foreach (string path in paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    yield return MetadataReference.CreateFromFile(path);
                }
            }
            else
            {
                yield return MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            }
        }
    }
}
