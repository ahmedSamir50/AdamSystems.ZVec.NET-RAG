using Microsoft.Extensions.VectorData;
using System.Diagnostics.CodeAnalysis;
using ZVec.Extensions.VectorData.Manifest;
using ZVec.Extensions.VectorData.Store;
using ZVec.Rag.Constants;
using ZVec.Rag.Exceptions;
using ZVec.Rag.Schema;

namespace ZVec.Rag.Internal;

/// <summary>
/// Opens the RAG chunk collection and wraps embedder stamp failures.
/// </summary>
internal static class RagCollectionAccessor
{
    /// <summary>
    /// Ensures the RAG collection exists, translating stamp failures to <see cref="ZVecRagInitializationException"/>.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "ZVecRagRecordV1 uses source-generated schema and mapper; GetCollection does not require dynamic code at runtime.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ZVecRagRecordV1 uses source-generated schema and mapper; GetCollection does not require unreferenced code at runtime.")]
    public static async Task<VectorStoreCollection<string, ZVecRagRecordV1>> EnsureCollectionAsync(
        ZVecVectorStore store,
        ZVecVectorStoreOptions storeOptions,
        string collectionName,
        CancellationToken cancellationToken)
    {
        try
        {
            var collection = store.GetCollection<string, ZVecRagRecordV1>(collectionName);
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
