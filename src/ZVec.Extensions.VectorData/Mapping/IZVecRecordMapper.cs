using ZVec.NET;
using ZVec.NET.Mapping;

namespace ZVec.Extensions.VectorData.Mapping;

/// <summary>
/// AOT-clean zero-reflection mapper between a POCO record type and a <see cref="ZVecDoc"/>.
/// Implementations are emitted by <c>ZVecRecordMetadataGenerator</c> for each
/// <c>[VectorStoreRecord]</c>-annotated class.
/// </summary>
/// <typeparam name="TRecord">The POCO record type.</typeparam>
public interface IZVecRecordMapper<TRecord> where TRecord : class
{
    /// <summary>
    /// Converts a POCO record into a <see cref="ZVecDoc"/> for native upsert.
    /// Zero reflection — direct property access.
    /// </summary>
    ZVecDoc ToDoc(TRecord record, ZVecTypeModel model);

    /// <summary>
    /// Converts a <see cref="ZVecDoc"/> back into a POCO record after native fetch.
    /// Zero reflection — direct property access.
    /// </summary>
    TRecord FromDoc(ZVecDoc doc, ZVecTypeModel model);
}
