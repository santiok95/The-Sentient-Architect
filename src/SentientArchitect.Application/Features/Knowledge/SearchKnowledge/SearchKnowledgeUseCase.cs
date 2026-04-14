using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Knowledge.SearchKnowledge;

public class SearchKnowledgeUseCase(
    IApplicationDbContext db,
    IVectorStore vectorStore,
    IEmbeddingService embeddingService)
{
    public async Task<Result<SearchKnowledgeResponse>> ExecuteAsync(
        SearchKnowledgeRequest request,
        CancellationToken ct = default)
    {
        // 1. Validate
        if (string.IsNullOrWhiteSpace(request.Query))
            return Result<SearchKnowledgeResponse>.Failure(["Query is required."]);

        // 2. Generate embedding for query
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(request.Query, ct);

        // 3. Vector similarity search
        var vectorResults = await vectorStore.SearchSimilarAsync(
            queryEmbedding,
            request.UserId,
            request.TenantId,
            request.MaxResults,
            request.MinimumScore,
            request.IncludeShared,
            request.IncludeAllScopes,
            ct);

        // 4. Batch-fetch all KnowledgeItems in a single query (avoid N+1)
        var ids = vectorResults.Select(v => v.KnowledgeItemId).ToList();
        var itemsById = await db.KnowledgeItems
            .AsNoTracking()
            .Where(k => ids.Contains(k.Id))
            .ToDictionaryAsync(k => k.Id, ct);

        // 5. Map to response records preserving the vector similarity ranking
        var results = new List<KnowledgeSearchResult>(vectorResults.Count);
        foreach (var vectorResult in vectorResults)
        {
            if (!itemsById.TryGetValue(vectorResult.KnowledgeItemId, out var item))
                continue;

            results.Add(new KnowledgeSearchResult(
                item.Id,
                item.Title,
                vectorResult.ChunkText,
                vectorResult.Score,
                item.Type,
                item.SourceUrl));
        }

        // 6. Return success
        return Result<SearchKnowledgeResponse>.SuccessWith(
            new SearchKnowledgeResponse(results, results.Count));
    }
}
