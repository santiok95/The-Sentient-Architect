namespace SentientArchitect.Domain.ValueObjects;

/// <summary>
/// Represents a single result from a vector similarity search.
/// Immutable by design — this is a pure Value Object.
/// </summary>
public record VectorSearchResult(
    Guid KnowledgeItemId,
    int ChunkIndex,
    string ChunkText,
    float Score);
