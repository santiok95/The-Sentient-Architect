using SentientArchitect.Domain.Abstractions;

namespace SentientArchitect.Domain.Entities;

public class KnowledgeEmbedding : BaseEntity
{
    public KnowledgeEmbedding(Guid knowledgeItemId, int chunkIndex,
        string chunkText, float[] embedding)
    {
        KnowledgeItemId = knowledgeItemId;
        ChunkIndex = chunkIndex;
        ChunkText = chunkText;
        Embedding = embedding;
    }

    private KnowledgeEmbedding() { }

    public Guid KnowledgeItemId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string ChunkText { get; private set; } = string.Empty;
    public float[] Embedding { get; private set; } = [];

    public KnowledgeItem? KnowledgeItem { get; private set; }
}
