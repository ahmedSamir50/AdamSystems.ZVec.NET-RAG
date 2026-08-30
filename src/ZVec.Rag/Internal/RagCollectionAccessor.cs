using Microsoft.Extensions.VectorData;
using System.Diagnostics.CodeAnalysis;
using ZVec.Extensions.VectorData.Manifest;
using ZVec.Extensions.VectorData.Store;
using ZVec.Rag.Constants;
using ZVec.Rag.Exceptions;
using ZVec.Rag.Schema;

namespace ZVec.Rag.Internal;

/// <summary>
/// Opens RAG collections and wraps embedder stamp failures.
/// </summary>
internal static class RagCollectionAccessor
{
    /// <summary>
    /// Ensures the RAG chunk collection exists, translating stamp failures to <see cref="ZVecRagInitializationException"/>.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "ZVecRagRecordV1 uses source-generated schema and mapper; GetCollection does not require dynamic code at runtime.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ZVecRagRecordV1 uses source-generated schema and mapper; GetCollection does not require unreferenced code at runtime.")]
    public static async Task<VectorStoreCollection<string, ZVecRagRecordV1>> EnsureCollectionAsync(
        ZVecVectorStore store,
        ZVecVectorStoreOptions storeOptions,
        string collectionName,
        CancellationToken cancellationToken)
        => await EnsureCollectionAsync<ZVecRagRecordV1>(store, storeOptions, collectionName, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Ensures the section-summary collection exists, translating stamp failures to <see cref="ZVecRagInitializationException"/>.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "ZVecRagSectionSummaryV1 uses source-generated schema and mapper; GetCollection does not require dynamic code at runtime.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ZVecRagSectionSummaryV1 uses source-generated schema and mapper; GetCollection does not require unreferenced code at runtime.")]
    public static async Task<VectorStoreCollection<string, ZVecRagSectionSummaryV1>> EnsureSummaryCollectionAsync(
        ZVecVectorStore store,
        ZVecVectorStoreOptions storeOptions,
        string collectionName,
        CancellationToken cancellationToken)
        => await EnsureCollectionAsync<ZVecRagSectionSummaryV1>(store, storeOptions, collectionName, cancellationToken)
            .ConfigureAwait(false);

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "ZVecRagRecordV1 and ZVecRagSectionSummaryV1 use source-generated schema and mapper; GetCollection does not require dynamic code at runtime.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ZVecRagRecordV1 and ZVecRagSectionSummaryV1 use source-generated schema and mapper; GetCollection does not require unreferenced code at runtime.")]
    private static async Task<VectorStoreCollection<string, TRecord>> EnsureCollectionAsync<TRecord>(
        ZVecVectorStore store,
        ZVecVectorStoreOptions storeOptions,
        string collectionName,
        CancellationToken cancellationToken)
        where TRecord : class
    {
        try
        {
            var collection = store.GetCollection<string, TRecord>(collectionName);
            await collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
            return collection;
        }
        catch (ZVecEmbedderMismatchException ex)
        {
            throw new ZVecRagInitializationException(
                ZVecRagErrorMessages.InitializationFailed(storeOptions.StoragePath, ex.Message),
                ex);
        }
        catch (ZVecManifestException ex)
        {
            throw new ZVecRagInitializationException(
                ZVecRagErrorMessages.InitializationFailed(storeOptions.StoragePath, ex.Message),
                ex);
        }
    }
}
