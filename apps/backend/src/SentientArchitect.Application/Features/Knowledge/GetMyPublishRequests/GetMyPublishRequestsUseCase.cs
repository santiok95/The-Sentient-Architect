using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Knowledge.GetMyPublishRequests;

public record GetMyPublishRequestsRequest(Guid UserId, int Page = 1, int PageSize = 20);

public record MyPublishRequestItem(
    string Id,
    string KnowledgeItemId,
    string KnowledgeItemTitle,
    string KnowledgeItemType,
    string? RequestReason,
    string Status,
    string CreatedAt,
    string? ReviewedAt,
    string? RejectionReason);

public record GetMyPublishRequestsResponse(
    List<MyPublishRequestItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetMyPublishRequestsUseCase(IApplicationDbContext db)
{
    public async Task<Result<GetMyPublishRequestsResponse>> ExecuteAsync(
        GetMyPublishRequestsRequest request,
        CancellationToken ct = default)
    {
        var query = db.ContentPublishRequests
            .Where(r => r.RequestedByUserId == request.UserId)
            .Include(r => r.KnowledgeItem)
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(r => new MyPublishRequestItem(
            Id:                  r.Id.ToString(),
            KnowledgeItemId:     r.KnowledgeItemId.ToString(),
            KnowledgeItemTitle:  r.KnowledgeItem?.Title ?? "(eliminado)",
            KnowledgeItemType:   r.KnowledgeItem?.Type.ToString() ?? "",
            RequestReason:       r.RequestReason,
            Status:              r.Status.ToString(),
            CreatedAt:           r.CreatedAt.ToString("O"),
            ReviewedAt:          r.ReviewedAt?.ToString("O"),
            RejectionReason:     r.RejectionReason
        )).ToList();

        return Result<GetMyPublishRequestsResponse>.SuccessWith(
            new GetMyPublishRequestsResponse(dtos, totalCount, page, pageSize));
    }
}
