using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Constants;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Knowledge.GetKnowledgeItems;

public record GetKnowledgeItemsRequest(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Type = null);

public record KnowledgeItemDto(
    Guid Id,
    string Title,
    string Type,
    string Scope,
    string ProcessingStatus,
    bool HasEmbeddings,
    string? SourceUrl,
    List<string> Tags,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record PagedKnowledgeItemsDto(
    List<KnowledgeItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public class GetKnowledgeItemsUseCase(IApplicationDbContext db)
{
    public async Task<Result<PagedKnowledgeItemsDto>> ExecuteAsync(GetKnowledgeItemsRequest request, CancellationToken ct = default)
    {
        // Note: Embeddings are NOT included here — we use a subquery for HasEmbeddings to avoid
        // loading 1536-float vectors per chunk (~6 KB each) just to check existence.
        var query = db.KnowledgeItems
            .Where(k => k.UserId == request.UserId || k.TenantId == TenantIds.Shared)
            .Include(k => k.KnowledgeItemTags)
                .ThenInclude(t => t.Tag)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(k => k.Title.ToLower().Contains(request.Search.ToLower()));

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var rawType = request.Type.Equals("Repository", StringComparison.OrdinalIgnoreCase)
                ? nameof(KnowledgeItemType.RepositoryReference)
                : request.Type;

            if (Enum.TryParse<KnowledgeItemType>(rawType, ignoreCase: true, out var enumType))
                query = query.Where(k => k.Type == enumType);
        }

        var totalCount = await query.CountAsync(ct);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(k => k.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Batch-check which items have embeddings in a single query (avoids N+1 with heavy vector data)
        var itemIds = items.Select(k => k.Id).ToList();
        var itemsWithEmbeddings = await db.KnowledgeEmbeddings
            .AsNoTracking()
            .Where(e => itemIds.Contains(e.KnowledgeItemId))
            .Select(e => e.KnowledgeItemId)
            .Distinct()
            .ToHashSetAsync(ct);

        var dtos = items.Select(k => new KnowledgeItemDto(
            Id: k.Id,
            Title: k.Title,
            Type: k.Type.ToString(),
            Scope: k.TenantId == TenantIds.Shared ? "Shared" : "Personal",
            ProcessingStatus: k.ProcessingStatus.ToString(),
            HasEmbeddings: itemsWithEmbeddings.Contains(k.Id),
            SourceUrl: k.SourceUrl,
            Tags: k.KnowledgeItemTags.Select(t => t.Tag!.Name).ToList(),
            CreatedAt: k.CreatedAt,
            UpdatedAt: k.UpdatedAt
        )).ToList();

        return Result<PagedKnowledgeItemsDto>.SuccessWith(
            new PagedKnowledgeItemsDto(dtos, totalCount, page, pageSize));
    }
}
