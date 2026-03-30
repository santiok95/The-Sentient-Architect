# pgvector / Vector DB Patterns

## Setup
- PostgreSQL 16+ with `CREATE EXTENSION IF NOT EXISTS vector;`
- EF Core package: `Pgvector.EntityFrameworkCore`
- All vector operations go through `IVectorStore` interface (Domain layer)
- Implementation `PgVectorStore` lives in Infrastructure layer

## Embedding Storage
- KnowledgeEmbedding entity holds the vector alongside the chunk text
- Vector dimensionality depends on the embedding model (e.g., 1536 for text-embedding-3-small)
- IMPORTANT: all embeddings MUST use the same model — mixing models makes similarity search meaningless
- Store the model name in configuration so it's explicit

## Chunking Strategy
- Split long content into chunks of 500-800 tokens with ~50 token overlap
- Each chunk becomes a separate KnowledgeEmbedding record
- Preserve the ChunkIndex to reconstruct document order if needed
- Include the parent KnowledgeItem title in each chunk for context

## Similarity Search
- Use cosine distance for similarity: `ORDER BY embedding <=> query_embedding LIMIT N`
- Create HNSW index for fast approximate nearest neighbor search:
  `CREATE INDEX ON knowledge_embeddings USING hnsw (embedding vector_cosine_ops)`
- HNSW parameters: `m=16, ef_construction=64` are good defaults
- Query-time: set `ef_search=40` for balance of speed vs accuracy

## IVectorStore Interface
```csharp
public interface IVectorStore
{
    Task StoreEmbeddingAsync(Guid knowledgeItemId, int chunkIndex, string chunkText, float[] embedding);
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding, 
        int maxResults, 
        Guid userId,                // filter personal scope
        Guid tenantId,              // filter shared scope  
        bool includeShared = true,  // include shared results alongside personal
        float minScore = 0.7f);
    Task DeleteEmbeddingsAsync(Guid knowledgeItemId);
    Task<bool> HasEmbeddingsAsync(Guid knowledgeItemId);
}
```

**Scope logic**: User searches pass `userId` + `tenantId` + `includeShared=true` → returns personal items + shared items. Admin can broaden scope as needed.

## RAG Pipeline
1. User question → generate embedding via IEmbeddingService
2. Search pgvector via IVectorStore.SearchAsync() → returns chunk IDs + scores
3. Fetch parent KnowledgeItems from PostgreSQL via IKnowledgeRepository
4. Assemble context: chunk texts + item metadata + user profile
5. Send assembled prompt to LLM via Semantic Kernel agent
6. Store retrieved item IDs in ConversationMessage.RetrievedContextIds

## Re-embedding
- If changing embedding models, ALL existing embeddings must be regenerated
- Implement as a background job that processes KnowledgeItems in batches
- Delete old embeddings → generate new ones → update in transaction
- The IVectorStore abstraction makes this a contained operation