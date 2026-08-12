using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;

namespace ZVec.AotTestApp;

/// <summary>
/// Sample document model for Native AOT trim verification.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class SampleAotDoc
{
    /// <summary>Unique Identifier.</summary>
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>Dense embedding vector.</summary>
    [ZVecVector(768)]
    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }

    /// <summary>Sample title field.</summary>
    [ZVecField]
    [VectorStoreData(IsIndexed = true, IsFullTextIndexed = true)]
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// Non-source-generated record used to surface trim warnings for reflection fallback paths.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed class ReflectionFallbackRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreVector(4)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

public static class Program
{
    public static int Main()
    {
        Console.WriteLine("=== ZVec.NET Native AOT Audit Harness Starting ===");

        try
        {
            // Test 1: TypeModel Resolution under AOT
            var model = ZVecTypeModel.Get<SampleAotDoc>();
            Console.WriteLine($"[AOT Test 1] Model resolved: {model.ClrType.Name} (Id: {model.Id.Property.Name}, Fields: {model.Fields.Count}, Vectors: {model.Vectors.Count})");

            // Test 2: POCO to ZVecDoc Conversion & Vector Pinning under AOT
            float[] sampleVector = new float[768];
            sampleVector[0] = 0.42f;

            var record = new SampleAotDoc
            {
                Id = "doc_aot_001",
                Title = "AOT Document Test",
                Vector = sampleVector
            };

            var doc = ZVecMapper.ToDoc(record, model);
            Console.WriteLine($"[AOT Test 2] ZVecDoc created successfully. Id: {doc.Id}, Fields Count: {doc.Fields.Count}");

            // Test 3: Reverse ZVecDoc to POCO Mapping under AOT
            var restored = ZVecMapper.FromDoc<SampleAotDoc>(doc, model);
            Console.WriteLine($"[AOT Test 3] Document restored: Id={restored.Id}, Title={restored.Title}, VectorDim={restored.Vector.Length}");

            // Test 4: ZVecVectorStore instantiation + collection retrieval under AOT
            var options = new ZVecVectorStoreOptions
            {
                StoragePath = Path.Combine(Path.GetTempPath(), "ZVecAotTests", Guid.NewGuid().ToString("N"))
            };
            Directory.CreateDirectory(options.StoragePath);

            var store = new ZVecVectorStore(new ZVecFactory(), options);
            var collection = store.GetCollection<string, SampleAotDoc>("aot_test_collection");
            Console.WriteLine($"[AOT Test 4] ZVecVectorStore + collection resolved: {collection.Name}");

            // Test 5: Filter Expression Translation under AOT (no Expression.Compile)
            System.Linq.Expressions.Expression<Func<SampleAotDoc, bool>> filter = x => x.Title == "AOT Document Test";
            string filterStr = ZVecFilterExpressionVisitor.Translate(filter);
            Console.WriteLine($"[AOT Test 5] Filter translated: {filterStr}");

            // Test 6: Upsert + Search round-trip under AOT (verifies zero-reflection mapper)
            collection.EnsureCollectionExistsAsync(CancellationToken.None).GetAwaiter().GetResult();
            collection.UpsertAsync(record, CancellationToken.None).GetAwaiter().GetResult();

            var fetched = collection.GetAsync("doc_aot_001", cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
            if (fetched == null) throw new InvalidOperationException("Fetched document was null after upsert.");
            Console.WriteLine($"[AOT Test 6] Upsert + Get round-trip OK. Fetched Title={fetched.Title}");

            // Test 7: Vectorized Search under AOT
            var searchResults = new List<VectorSearchResult<SampleAotDoc>>();
            var searchAsync = collection.SearchAsync(record.Vector, 5, cancellationToken: CancellationToken.None);
            var enumerator = searchAsync.GetAsyncEnumerator(CancellationToken.None);
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                searchResults.Add(enumerator.Current);
            }
            if (searchResults.Count == 0) throw new InvalidOperationException("Search returned no results under AOT.");
            Console.WriteLine($"[AOT Test 7] Vectorized search returned {searchResults.Count} result(s). Top score: {searchResults[0].Score}");

            // Test 8: Reference non-SG record type to surface trim warnings during publish
            var fallbackRecord = new ReflectionFallbackRecord
            {
                Id = "fallback",
                Title = "Reflection Fallback",
                Vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
            };
            _ = fallbackRecord.Title;
            Console.WriteLine($"[AOT Test 8] ReflectionFallbackRecord referenced: {fallbackRecord.Id}");

            // Cleanup
            collection.EnsureCollectionDeletedAsync(CancellationToken.None).GetAwaiter().GetResult();
            try { Directory.Delete(options.StoragePath, recursive: true); } catch { }

            Console.WriteLine("=== All Native AOT Verification Tests Passed Successfully ===");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] AOT Verification Failure: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
