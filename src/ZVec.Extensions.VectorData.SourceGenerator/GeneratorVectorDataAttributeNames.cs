namespace ZVec.Extensions.VectorData.SourceGenerator;

/// <summary>
/// Well-known index-kind tokens for quantize overrides and legacy metric names.
/// </summary>
internal static class GeneratorVectorDataAttributeNames
{
    internal const string QuantizeTypeInt8 = "Int8";
    internal const string QuantizeTypeFp16 = "Fp16";
    internal const string QuantizeTypeUndefined = "Undefined";

    internal const string MetricTypeCosine = "Cosine";
    internal const string MetricTypeL2 = "L2";
    internal const string MetricTypeIp = "Ip";
}
