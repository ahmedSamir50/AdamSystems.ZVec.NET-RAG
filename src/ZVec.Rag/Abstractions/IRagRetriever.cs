using ZVec.Rag.Models;

namespace ZVec.Rag.Abstractions;

/// <summary>
/// Hybrid dense + FTS retrieval contract.
/// </summary>
public interface IRagRetriever
{
    /// <summary>Retrieves ranked citations for a natural-language query.</summary>
    Task<IReadOnlyList<Citation>> RetrieveAsync(
        string query,
        int? topK = null,
        CancellationToken cancellationToken = default);
}
