using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Knowledge.IngestKnowledge;

public class IngestKnowledgeUseCase
{
    private readonly IKnowledgeRepository _knowledgeRepository;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITagRepository _tagRepository;

    public IngestKnowledgeUseCase(
        IKnowledgeRepository knowledgeRepository,
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        ITagRepository tagRepository)
    {
        _knowledgeRepository = knowledgeRepository;
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _tagRepository = tagRepository;
    }

    public async Task<Result<IngestKnowledgeResponse>> ExecuteAsync(
        IngestKnowledgeRequest request,
        CancellationToken ct = default)
    {
        // 1. Validate
        List<string> validationErrors = [];

        if (string.IsNullOrWhiteSpace(request.Title))
            validationErrors.Add("Title is required.");

        if (string.IsNullOrWhiteSpace(request.OriginalContent))
            validationErrors.Add("Content is required.");

        if (validationErrors.Count > 0)
            return Result<IngestKnowledgeResponse>.Failure(validationErrors);

        // 2. Create entity and mark as processing
        var item = new KnowledgeItem(
            request.UserId,
            request.TenantId,
            request.Title,
            request.OriginalContent,
            request.Type,
            request.SourceUrl);

        item.MarkAsProcessing();

        // 3. Persist initial state
        await _knowledgeRepository.AddAsync(item, ct);

        try
        {
            // 4. Chunk the content
            var chunks = ChunkText(request.OriginalContent);

            // 5 & 6. Generate embeddings and store each chunk
            for (var i = 0; i < chunks.Count; i++)
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(chunks[i], ct);
                await _vectorStore.StoreEmbeddingAsync(item.Id, i, chunks[i], embedding, ct);
            }

            // 7. Handle tags
            if (request.Tags is { Count: > 0 })
            {
                foreach (var tagName in request.Tags)
                {
                    if (string.IsNullOrWhiteSpace(tagName))
                        continue;

                    var tag = await _tagRepository.GetByNameAndCategoryAsync(
                        tagName.Trim(), TagCategory.Custom, ct);

                    if (tag is null)
                    {
                        tag = new Tag(tagName.Trim(), TagCategory.Custom);
                        await _tagRepository.AddAsync(tag, ct);
                    }

                    await _tagRepository.AddTagToKnowledgeItemAsync(item.Id, tag.Id, ct);
                }
            }

            // 8. Mark completed with summary
            var summary = request.OriginalContent.Length <= 200
                ? request.OriginalContent
                : request.OriginalContent[..200];

            item.MarkAsCompleted(summary);

            // 9. Persist final state
            await _knowledgeRepository.UpdateAsync(item, ct);

            // 10. Return success
            return Result<IngestKnowledgeResponse>.SuccessWith(
                new IngestKnowledgeResponse(item.Id, item.ProcessingStatus, chunks.Count));
        }
        catch (Exception ex)
        {
            item.MarkAsFailed();
            await _knowledgeRepository.UpdateAsync(item, ct);

            return Result<IngestKnowledgeResponse>.Failure(
                [$"Ingestion failed: {ex.Message}"]);
        }
    }

    private static List<string> ChunkText(string text, int chunkSize = 800, int overlap = 50)
    {
        var chunks = new List<string>();

        if (string.IsNullOrEmpty(text))
            return chunks;

        var step = chunkSize - overlap;
        var position = 0;

        while (position < text.Length)
        {
            var length = Math.Min(chunkSize, text.Length - position);
            chunks.Add(text.Substring(position, length));
            position += step;
        }

        return chunks;
    }
}
