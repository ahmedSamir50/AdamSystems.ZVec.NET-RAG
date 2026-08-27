namespace ZVec.Rag.Models;

/// <summary>
/// A text segment produced by <see cref="Abstractions.IZVecTextChunker"/> with source offset metadata.
/// </summary>
/// <param name="Text">Chunk text payload.</param>
/// <param name="Offset">0-based character offset in the extracted document text.</param>
public sealed record TextChunk(string Text, long Offset);
