namespace ZVec.Extensions.VectorData.SourceGenerator;

/// <summary>
/// Resolved dense-vector metric for source-generated HNSW index emission.
/// </summary>
internal enum GeneratorVectorMetricKind
{
    DefaultCosine,
    L2,
    InnerProduct
}
