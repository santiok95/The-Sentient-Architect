using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Knowledge.GetKnowledgeItems;

public record GetKnowledgeItemsRequest(Guid UserId);

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

public class GetKnowledgeItemsUseCase(IApplicationDbContext db)
{
    public async Task<Result<List<KnowledgeItemDto>>> ExecuteAsync(GetKnowledgeItemsRequest request, CancellationToken ct = default)
    {
        var items = await db.KnowledgeItems
            .Where(k => k.UserId == request.UserId)
            .Include(k => k.KnowledgeItemTags)
                .ThenInclude(t => t.Tag)
            .Include(k => k.Embeddings)
            .AsNoTracking()
            .ToListAsync(ct);

        var dtos = items.Select(k => new KnowledgeItemDto(
            Id: k.Id,
            Title: k.Title,
            Type: k.Type.ToString(),
            Scope: k.UserId == k.TenantId ? "Personal" : "Shared",
            ProcessingStatus: k.ProcessingStatus.ToString(),
            HasEmbeddings: k.Embeddings.Any(),
            SourceUrl: k.SourceUrl,
            Tags: k.KnowledgeItemTags.Select(t => t.Tag!.Name).ToList(),
            CreatedAt: k.CreatedAt,
            UpdatedAt: k.UpdatedAt
        )).ToList();

        return Result<List<KnowledgeItemDto>>.SuccessWith(dtos);
    }
}
