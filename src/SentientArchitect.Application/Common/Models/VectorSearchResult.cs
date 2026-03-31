namespace SentientArchitect.Application.Common.Models;

/// <summary>
/// Immutable result of a vector similarity search.
/// </summary>
public record VectorSearchResult(
    Guid KnowledgeItemId,
    int ChunkIndex,
    string ChunkText,
    float Score);
