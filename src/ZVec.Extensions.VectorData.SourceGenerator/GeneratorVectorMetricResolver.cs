namespace ZVec.Extensions.VectorData.SourceGenerator;

internal static class GeneratorVectorMetricResolver
{
    internal static GeneratorVectorMetricKind ResolveMetricKind(string? distanceFunctionValue, string? indexKind)
    {
        GeneratorVectorMetricKind? fromDistance = ResolveFromDistanceFunctionValue(distanceFunctionValue);
        if (fromDistance.HasValue)
        {
            return fromDistance.Value;
        }

        if (!string.IsNullOrWhiteSpace(indexKind))
        {
            if (string.Equals(indexKind, GeneratorVectorDataAttributeNames.MetricTypeL2, StringComparison.OrdinalIgnoreCase))
            {
                return GeneratorVectorMetricKind.L2;
            }

            if (string.Equals(indexKind, GeneratorVectorDataAttributeNames.MetricTypeIp, StringComparison.OrdinalIgnoreCase))
            {
                return GeneratorVectorMetricKind.InnerProduct;
            }
        }

        return GeneratorVectorMetricKind.DefaultCosine;
    }

    internal static bool TryResolveQuantizeIndexKind(string? indexKind, out string emissionExpression)
    {
        emissionExpression = GeneratorMetadataNames.HnswIndexParamExpression;
        if (string.IsNullOrWhiteSpace(indexKind))
        {
            return false;
        }

        if (string.Equals(indexKind, GeneratorVectorDataAttributeNames.QuantizeTypeInt8, StringComparison.OrdinalIgnoreCase))
        {
            emissionExpression = GeneratorMetadataNames.HnswIndexParamInt8Expression;
            return true;
        }

        if (string.Equals(indexKind, GeneratorVectorDataAttributeNames.QuantizeTypeFp16, StringComparison.OrdinalIgnoreCase))
        {
            emissionExpression = GeneratorMetadataNames.HnswIndexParamFp16Expression;
            return true;
        }

        if (string.Equals(indexKind, GeneratorVectorDataAttributeNames.QuantizeTypeUndefined, StringComparison.OrdinalIgnoreCase))
        {
            emissionExpression = GeneratorMetadataNames.HnswIndexParamUndefinedQuantizeExpression;
            return true;
        }

        return false;
    }

    private static GeneratorVectorMetricKind? ResolveFromDistanceFunctionValue(string? distanceFunctionValue)
    {
        if (string.IsNullOrWhiteSpace(distanceFunctionValue))
        {
            return null;
        }

        if (string.Equals(distanceFunctionValue, GeneratorVectorDataDistanceFunctions.EuclideanDistance, StringComparison.Ordinal) ||
            string.Equals(distanceFunctionValue, GeneratorVectorDataDistanceFunctions.EuclideanSquaredDistance, StringComparison.Ordinal))
        {
            return GeneratorVectorMetricKind.L2;
        }

        if (string.Equals(distanceFunctionValue, GeneratorVectorDataDistanceFunctions.DotProductSimilarity, StringComparison.Ordinal) ||
            string.Equals(distanceFunctionValue, GeneratorVectorDataDistanceFunctions.NegativeDotProductSimilarity, StringComparison.Ordinal))
        {
            return GeneratorVectorMetricKind.InnerProduct;
        }

        if (string.Equals(distanceFunctionValue, GeneratorVectorDataDistanceFunctions.CosineDistance, StringComparison.Ordinal) ||
            string.Equals(distanceFunctionValue, GeneratorVectorDataDistanceFunctions.CosineSimilarity, StringComparison.Ordinal))
        {
            return GeneratorVectorMetricKind.DefaultCosine;
        }

        return null;
    }
}
