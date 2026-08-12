using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.VectorData;
using ZVec.NET.Mapping;

namespace ZVec.AotTestApp;

/// <summary>
/// Sample document model for Native AOT trim verification.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
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

            Console.WriteLine("=== All Native AOT Verification Tests Passed Successfully ===");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ AOT Verification Failure: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
