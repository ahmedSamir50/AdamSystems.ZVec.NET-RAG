using ZVec.Rag.Models;

namespace ZVec.Rag.Abstractions;

/// <summary>
/// Splits document text into bounded chunks (chunking ACL; separate from readers).
/// </summary>
public interface IZVecTextChunker
{
    /// <summary>Gets the strategy identifier used in chunk id hashing.</summary>
    string StrategyId { get; }

    /// <summary>Splits <paramref name="text"/> into ordered chunks with character offsets.</summary>
    IEnumerable<TextChunk> Chunk(string text);
}
