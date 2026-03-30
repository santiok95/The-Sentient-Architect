namespace SentientArchitect.Domain.Entities;

/// <summary>
/// One KnowledgeItem → many embeddings (one per chunk).
/// Uses float[] in Domain; mapped to pgvector Vector type at the Infrastructure boundary.
/// </summary>
public class KnowledgeEmbedding
{
    /// <summary>EF Core requires a parameterless constructor. Never call this directly.</summary>
    private KnowledgeEmbedding() { }

    public Guid Id { get; private set; }
    public Guid KnowledgeItemId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string ChunkText { get; private set; } = default!;

    /// <summary>
    /// The vector embedding as a float array.
    /// Dimensionality depends on the embedding model (e.g., 1536 for text-embedding-3-small).
    /// Mapped to pgvector 'vector' column type in Infrastructure.
    /// </summary>
    public float[] Embedding { get; private set; } = default!;

    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Creates a new embedding chunk for a KnowledgeItem.
    /// </summary>
    public static KnowledgeEmbedding Create(
        Guid knowledgeItemId,
        int chunkIndex,
        string chunkText,
        float[] embedding)
    {
        if (knowledgeItemId == Guid.Empty)
            throw new ArgumentException("KnowledgeItemId cannot be empty.", nameof(knowledgeItemId));

        if (chunkIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(chunkIndex), "ChunkIndex must be zero or positive.");

        if (string.IsNullOrWhiteSpace(chunkText))
            throw new ArgumentException("ChunkText cannot be empty.", nameof(chunkText));

        if (embedding is null || embedding.Length == 0)
            throw new ArgumentException("Embedding cannot be null or empty.", nameof(embedding));

        return new KnowledgeEmbedding
        {
            Id = Guid.NewGuid(),
            KnowledgeItemId = knowledgeItemId,
            ChunkIndex = chunkIndex,
            ChunkText = chunkText,
            Embedding = embedding,
            CreatedAt = DateTime.UtcNow
        };
    }
}
