using SentientArchitect.Domain.ValueObjects;

namespace SentientArchitect.Domain.Interfaces;

/// <summary>
/// Abstraction over the vector database (pgvector initially, swappable to Qdrant/Milvus later).
/// All vector operations go through this interface — never direct pgvector calls.
/// </summary>
public interface IVectorStore
{
    Task StoreEmbeddingAsync(
        Guid knowledgeItemId,
        int chunkIndex,
        string chunkText,
        float[] embedding,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int maxResults,
        Guid userId,
        Guid tenantId,
        bool includeShared = true,
        float minScore = 0.7f,
        CancellationToken cancellationToken = default);

    Task DeleteEmbeddingsAsync(Guid knowledgeItemId, CancellationToken cancellationToken = default);
    Task<bool> HasEmbeddingsAsync(Guid knowledgeItemId, CancellationToken cancellationToken = default);
}
