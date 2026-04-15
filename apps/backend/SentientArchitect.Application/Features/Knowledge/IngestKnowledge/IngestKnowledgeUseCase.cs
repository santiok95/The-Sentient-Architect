using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Knowledge.IngestKnowledge;

public class IngestKnowledgeUseCase(
    IApplicationDbContext db,
    IVectorStore vectorStore,
    IEmbeddingService embeddingService)
{
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

        // 2. Sanitize content before persisting or embedding
        var sanitizedTitle   = PromptInjectionSanitizer.Sanitize(request.Title);
        var sanitizedContent = PromptInjectionSanitizer.Sanitize(request.OriginalContent);

        // 4. Create entity and mark as processing
        var item = new KnowledgeItem(
            request.UserId,
            request.TenantId,
            sanitizedTitle,
            sanitizedContent,
            request.Type,
            request.SourceUrl);

        item.MarkAsProcessing();

        // 3. Persist initial state
        db.KnowledgeItems.Add(item);
        await db.SaveChangesAsync(ct);

        try
        {
            // 4. Chunk the content
            var chunks = ChunkText(request.OriginalContent);

            // 5 & 6. Generate embeddings and store each chunk
            for (var i = 0; i < chunks.Count; i++)
            {
                var embedding = await embeddingService.GenerateEmbeddingAsync(chunks[i], ct);
                await vectorStore.StoreEmbeddingAsync(item.Id, i, chunks[i], embedding, ct);
            }

            // 7. Handle tags
            if (request.Tags is { Count: > 0 })
            {
                foreach (var tagName in request.Tags)
                {
                    if (string.IsNullOrWhiteSpace(tagName))
                        continue;

                    var normalizedName = tagName.Trim();

                    var tag = await db.Tags
                        .FirstOrDefaultAsync(t => t.Name == normalizedName && t.Category == TagCategory.Custom, ct);

                    if (tag is null)
                    {
                        tag = new Tag(normalizedName, TagCategory.Custom);
                        db.Tags.Add(tag);
                        await db.SaveChangesAsync(ct);
                    }

                    var kitag = new KnowledgeItemTag { KnowledgeItemId = item.Id, TagId = tag.Id };
                    db.KnowledgeItemTags.Add(kitag);
                    await db.SaveChangesAsync(ct);
                }
            }

            // 8. Mark completed with summary
            var summary = request.OriginalContent.Length <= 200
                ? request.OriginalContent
                : request.OriginalContent[..200];

            item.MarkAsCompleted(summary);

            // 9. Persist final state
            await db.SaveChangesAsync(ct);

            // 10. Auto-create publish request for non-admin users
            if (!request.IsUserAdmin)
            {
                var publishRequest = new ContentPublishRequest(
                    item.Id,
                    request.UserId,
                    requestReason: null);
                db.ContentPublishRequests.Add(publishRequest);
                await db.SaveChangesAsync(ct);
            }

            // 11. Return success
            return Result<IngestKnowledgeResponse>.SuccessWith(
                new IngestKnowledgeResponse(item.Id, item.ProcessingStatus, chunks.Count));
        }
        catch (Exception ex)
        {
            item.MarkAsFailed();
            await db.SaveChangesAsync(ct);

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
