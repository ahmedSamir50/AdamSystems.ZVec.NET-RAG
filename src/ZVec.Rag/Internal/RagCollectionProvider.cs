using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Collection;
using ZVec.Extensions.VectorData.Store;
using ZVec.Rag.Constants;
using ZVec.Rag.Options;
using ZVec.Rag.Schema;

namespace ZVec.Rag.Internal;

/// <summary>
/// Scoped holder ensuring a single native collection handle per DI scope (avoids LOCK conflicts).
/// </summary>
public sealed class RagCollectionProvider : IDisposable, IAsyncDisposable
{
    private readonly ZVecVectorStore _store;
    private readonly ZVecVectorStoreOptions _storeOptions;
    private readonly ZVecRagOptions _ragOptions;
    private VectorStoreCollection<string, ZVecRagRecordV1>? _collection;
    private VectorStoreCollection<string, ZVecRagSectionSummaryV1>? _summaryCollection;

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

    /// <summary>Gets or opens the section-summary collection for this scope.</summary>
    public async Task<VectorStoreCollection<string, ZVecRagSectionSummaryV1>> GetSummaryCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (_summaryCollection != null)
        {
            return _summaryCollection;
        }

        _summaryCollection = await RagCollectionAccessor.EnsureSummaryCollectionAsync(
            _store,
            _storeOptions,
            ZVecRagConstants.SectionSummaryCollectionName,
            cancellationToken).ConfigureAwait(false);

        return _summaryCollection;
    }

    /// <summary>Whether the summary collection handle has been opened in this scope.</summary>
    public bool SummaryCollectionOpened => _summaryCollection != null;

    /// <inheritdoc />
    public void Dispose()
    {
        ReleaseCollectionHandles();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        ReleaseCollectionHandles();
        return ValueTask.CompletedTask;
    }

    private void ReleaseCollectionHandles()
    {
        if (_collection is ZVecVectorizableRecordCollection<ZVecRagRecordV1, string> nativeCollection)
        {
            nativeCollection.ReleaseNativeHandle();
        }

        if (_summaryCollection is ZVecVectorizableRecordCollection<ZVecRagSectionSummaryV1, string> summaryNative)
        {
            summaryNative.ReleaseNativeHandle();
        }

        _collection = null;
        _summaryCollection = null;
    }
}
