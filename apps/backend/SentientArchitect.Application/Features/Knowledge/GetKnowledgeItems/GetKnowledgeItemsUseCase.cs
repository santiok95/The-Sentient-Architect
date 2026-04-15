using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
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
        var query = db.KnowledgeItems
            .Where(k => k.UserId == request.UserId || k.TenantId == Guid.Empty)
            .Include(k => k.KnowledgeItemTags)
                .ThenInclude(t => t.Tag)
            .Include(k => k.Embeddings)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.ToLower();
            query = query.Where(k =>
                k.Title.ToLower().Contains(term) ||
                k.KnowledgeItemTags.Any(t => t.Tag!.Name.ToLower().Contains(term)));
        }

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

        var dtos = items.Select(k => new KnowledgeItemDto(
            Id: k.Id,
            Title: k.Title,
            Type: k.Type.ToString(),
            Scope: k.TenantId == Guid.Empty ? "Shared" : "Personal",
            ProcessingStatus: k.ProcessingStatus.ToString(),
            HasEmbeddings: k.Embeddings.Any(),
            SourceUrl: k.SourceUrl,
            Tags: k.KnowledgeItemTags.Select(t => t.Tag!.Name).ToList(),
            CreatedAt: k.CreatedAt,
            UpdatedAt: k.UpdatedAt
        )).ToList();

        return Result<PagedKnowledgeItemsDto>.SuccessWith(
            new PagedKnowledgeItemsDto(dtos, totalCount, page, pageSize));
    }
}
