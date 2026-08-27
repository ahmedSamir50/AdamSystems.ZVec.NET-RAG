using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Exceptions;

namespace ZVec.Extensions.VectorData.Manifest;

/// <summary>
/// Thrown when an existing collection's embedder stamp does not match the configured model or schema.
/// </summary>
public sealed class ZVecEmbedderMismatchException : ZVecVectorDataException
{
    /// <summary>
    /// Initializes a new instance with expected vs actual stamp fields.
    /// </summary>
    public ZVecEmbedderMismatchException(
        string collectionPath,
        string expectedModelId,
        string actualModelId,
        int expectedDimensions,
        int actualDimensions,
        string expectedQuantizeType,
        string actualQuantizeType,
        string expectedStorageDataType,
        string actualStorageDataType)
        : base(ZVecErrorMessages.EmbedderStampMismatch(
            collectionPath,
            expectedModelId,
            actualModelId,
            expectedDimensions,
            actualDimensions,
            expectedQuantizeType,
            actualQuantizeType,
            expectedStorageDataType,
            actualStorageDataType))
    {
        CollectionPath = collectionPath;
        ExpectedModelId = expectedModelId;
        ActualModelId = actualModelId;
        ExpectedDimensions = expectedDimensions;
        ActualDimensions = actualDimensions;
        ExpectedQuantizeType = expectedQuantizeType;
        ActualQuantizeType = actualQuantizeType;
        ExpectedStorageDataType = expectedStorageDataType;
        ActualStorageDataType = actualStorageDataType;
    }

    /// <summary>Path to the native collection directory.</summary>
    public string CollectionPath { get; }

    /// <summary>Expected embedder model id from configuration.</summary>
    public string ExpectedModelId { get; }

    /// <summary>Model id recorded in the manifest.</summary>
    public string ActualModelId { get; }

    /// <summary>Expected vector dimensions from schema.</summary>
    public int ExpectedDimensions { get; }

    /// <summary>Dimensions recorded in the manifest.</summary>
    public int ActualDimensions { get; }

    /// <summary>Expected quantization type name.</summary>
    public string ExpectedQuantizeType { get; }

    /// <summary>Quantization type recorded in the manifest.</summary>
    public string ActualQuantizeType { get; }

    /// <summary>Expected native storage data type name.</summary>
    public string ExpectedStorageDataType { get; }

    /// <summary>Storage data type recorded in the manifest.</summary>
    public string ActualStorageDataType { get; }
}
