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

        // 4 & 5. Hydrate with KnowledgeItem metadata and map to response records
        var results = new List<KnowledgeSearchResult>(vectorResults.Count);

        foreach (var vectorResult in vectorResults)
        {
            var item = await db.KnowledgeItems
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.Id == vectorResult.KnowledgeItemId, ct);

            if (item is null)
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
