using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Store;
using ZVec.Rag.Options;
using ZVec.Rag.Schema;

namespace ZVec.Rag.Internal;

/// <summary>
/// Scoped holder ensuring a single native collection handle per DI scope (avoids LOCK conflicts).
/// </summary>
public sealed class RagCollectionProvider
{
    private readonly ZVecVectorStore _store;
    private readonly ZVecVectorStoreOptions _storeOptions;
    private readonly ZVecRagOptions _ragOptions;
    private VectorStoreCollection<string, ZVecRagRecordV1>? _collection;

    /// <summary>Initializes a new instance.</summary>
    public RagCollectionProvider(
        ZVecVectorStore store,
        ZVecVectorStoreOptions storeOptions,
        ZVecRagOptions ragOptions)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _storeOptions = storeOptions ?? throw new ArgumentNullException(nameof(storeOptions));
        _ragOptions = ragOptions ?? throw new ArgumentNullException(nameof(ragOptions));
    }

    /// <summary>Gets or opens the RAG chunk collection for this scope.</summary>
    public async Task<VectorStoreCollection<string, ZVecRagRecordV1>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (_collection != null)
        {
            return _collection;
        }

        _collection = await RagCollectionAccessor.EnsureCollectionAsync(
            _store,
            _storeOptions,
            _ragOptions.CollectionName,
            cancellationToken).ConfigureAwait(false);

        return _collection;
    }
}
