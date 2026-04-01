using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Knowledge.SearchKnowledge;

public class SearchKnowledgeUseCase
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly IKnowledgeRepository _knowledgeRepository;

    public SearchKnowledgeUseCase(
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        IKnowledgeRepository knowledgeRepository)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _knowledgeRepository = knowledgeRepository;
    }

    public async Task<Result<SearchKnowledgeResponse>> ExecuteAsync(
        SearchKnowledgeRequest request,
        CancellationToken ct = default)
    {
        // 1. Validate
        if (string.IsNullOrWhiteSpace(request.Query))
            return Result<SearchKnowledgeResponse>.Failure(["Query is required."]);

        // 2. Generate embedding for query
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Query, ct);

        // 3. Vector similarity search
        var vectorResults = await _vectorStore.SearchSimilarAsync(
            queryEmbedding,
            request.UserId,
            request.TenantId,
            request.MaxResults,
            request.MinimumScore,
            request.IncludeShared,
            ct);

        // 4 & 5. Hydrate with KnowledgeItem metadata and map to response records
        var results = new List<KnowledgeSearchResult>(vectorResults.Count);

        foreach (var vectorResult in vectorResults)
        {
            var item = await _knowledgeRepository.GetByIdAsync(vectorResult.KnowledgeItemId, ct);

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
