using System.Security.Cryptography;
using System.Text;
using ZVec.Rag.Constants;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// Generates content-addressable chunk identifiers per D-4.
/// </summary>
public static class ZVecChunkIdGenerator
{
    /// <summary>
    /// Computes <c>SHA256(source_uri | strategy_id | chunk_index)</c> as lowercase hex.
    /// </summary>
    public static string Compute(string sourceUri, string strategyId, int chunkIndex)
    {
        string payload = $"{sourceUri}|{strategyId}|{chunkIndex}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Computes SHA-256 hex digest of arbitrary text (for source hash metadata).</summary>
    public static string ComputeSourceHash(string text)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Returns the Story 2.1 default chunking strategy id.</summary>
    public static string DefaultStrategyId => ZVecRagConstants.WholeTextStrategyId;
}
