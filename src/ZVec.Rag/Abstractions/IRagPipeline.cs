namespace ZVec.Rag.Abstractions;

/// <summary>
/// Composite facade implementing ingest, retrieve, and generate without decorator middleware.
/// </summary>
public interface IRagPipeline : IRagIngestor, IRagRetriever, IRagGenerator
{
}
