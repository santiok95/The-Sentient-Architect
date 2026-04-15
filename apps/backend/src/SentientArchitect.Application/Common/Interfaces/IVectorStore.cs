using SentientArchitect.Application.Common.Models;

namespace SentientArchitect.Application.Common.Interfaces;

public interface IVectorStore
{
    Task StoreEmbeddingAsync(Guid knowledgeItemId, int chunkIndex,
        string chunkText, float[] embedding, CancellationToken ct = default);

    Task<IReadOnlyList<VectorSearchResult>> SearchSimilarAsync(
        float[] queryEmbedding, Guid userId, Guid tenantId,
        int topK = 5, float minimumScore = 0.7f,
        bool includeShared = true, bool includeAllScopes = false,
        CancellationToken ct = default);

    Task DeleteByKnowledgeItemAsync(Guid knowledgeItemId, CancellationToken ct = default);
}
