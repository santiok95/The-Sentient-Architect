using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Models;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Postgres.Repositories;

public class PgVectorStore(IApplicationDbContext context) : IVectorStore
{
    public async Task StoreEmbeddingAsync(
        Guid knowledgeItemId,
        int chunkIndex,
        string chunkText,
        float[] embedding,
        CancellationToken ct = default)
    {
        var entity = new KnowledgeEmbedding(knowledgeItemId, chunkIndex, chunkText, embedding);
        await context.KnowledgeEmbeddings.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchSimilarAsync(
        float[] queryEmbedding,
        Guid userId,
        Guid tenantId,
        int topK = 5,
        float minimumScore = 0.7f,
        bool includeShared = true,
        CancellationToken ct = default)
    {
        // The Embedding property is stored as float[] with a ValueConverter → Pgvector.Vector.
        // EF Core's LINQ translator works on the CLR type (float[]), so the Pgvector EF extension
        // instance methods (.CosineDistance(), .L2Distance(), etc.) cannot be used in LINQ —
        // those require the CLR property type to be Pgvector.Vector.
        //
        // Solution: raw parameterised SQL for the ORDER BY so the HNSW index is exercised,
        // then scope-filter and score-threshold in .NET on the bounded result set.

        var queryVector = new Vector(queryEmbedding);
        var fetchCount = topK * 3; // over-fetch so the score filter still yields topK results

        // Scope predicate: personal items OR shared items (if requested).
        // We build the scope filter via LINQ first to get the in-scope embedding IDs,
        // then join with the ordered raw-SQL result.
        //
        // Alternatively, pass the scope as additional SQL predicates — but keeping the
        // scope filter in LINQ makes it easier to evolve without SQL string manipulation.

        // Step 1 — Fetch ordered candidates via raw SQL (cosine distance uses HNSW index).
        //           We pull more than topK to compensate for score filtering afterwards.
        // The <=> operator is pgvector cosine distance.
        // NpgsqlParameter is used to safely bind the vector value.
        var vectorParam = new NpgsqlParameter("queryVector", queryVector);

        // fetchCount is a compile-time-bounded int (topK * 3), never from user input — no injection risk.
#pragma warning disable EF1002
        var rawOrderedIds = await context.Database
            .SqlQueryRaw<Guid>(
                $"""
                SELECT ke."Id"
                FROM   "KnowledgeEmbeddings" ke
                ORDER BY ke."Embedding" <=> @queryVector
                LIMIT  {fetchCount}
                """,
                vectorParam)
            .ToListAsync(ct);
#pragma warning restore EF1002

        if (rawOrderedIds.Count == 0)
            return [];

        // Step 2 — Load the full entities (with navigation) for the ordered candidates,
        //           applying the scope filter in LINQ.
        var candidates = await context.KnowledgeEmbeddings
            .Include(e => e.KnowledgeItem)
            .Where(e =>
                rawOrderedIds.Contains(e.Id) &&
                (e.KnowledgeItem!.UserId == userId ||
                 (includeShared && e.KnowledgeItem.TenantId == tenantId && e.KnowledgeItem.TenantId != userId)))
            .AsNoTracking()
            .ToListAsync(ct);

        // Step 3 — Score in .NET, filter by threshold, re-order by score (raw SQL ordered by distance,
        //           but LINQ lost that order when we applied the scope filter), take topK.
        return candidates
            .Select(e => (Embedding: e, Score: ComputeCosineSimilarity(queryEmbedding, e.Embedding)))
            .Where(x => x.Score >= minimumScore)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => new VectorSearchResult(
                x.Embedding.KnowledgeItemId,
                x.Embedding.ChunkIndex,
                x.Embedding.ChunkText,
                x.Score))
            .ToList();
    }

    public async Task DeleteByKnowledgeItemAsync(Guid knowledgeItemId, CancellationToken ct = default)
    {
        var embeddings = await context.KnowledgeEmbeddings
            .Where(e => e.KnowledgeItemId == knowledgeItemId)
            .ToListAsync(ct);

        if (embeddings.Count > 0)
        {
            context.KnowledgeEmbeddings.RemoveRange(embeddings);
            await context.SaveChangesAsync(ct);
        }
    }

    // -------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------

    /// <summary>
    /// Computes cosine similarity between two float arrays in .NET memory.
    /// Returns a value in [0, 1]: 1 = identical direction, 0 = orthogonal.
    /// </summary>
    private static float ComputeCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0f;

        float dot = 0f, normA = 0f, normB = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denominator == 0f ? 0f : dot / denominator;
    }
}
