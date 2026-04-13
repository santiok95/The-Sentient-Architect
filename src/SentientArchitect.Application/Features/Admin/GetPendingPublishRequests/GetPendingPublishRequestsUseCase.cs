using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Admin.GetPendingPublishRequests;

public record GetPublishRequestsQuery(
    string? Status = null,
    int Page = 1,
    int PageSize = 50);

public record PublishRequestKnowledgeItem(
    string Id,
    string Title,
    string Type,
    string? Summary);

public record PublishRequestUser(
    string Id,
    string DisplayName,
    string Role);

public record PublishRequestItem(
    string Id,
    PublishRequestKnowledgeItem KnowledgeItem,
    PublishRequestUser RequestedBy,
    string? RequestReason,
    string Status,
    string CreatedAt,
    string? ReviewedAt);

public record GetPublishRequestsResponse(
    List<PublishRequestItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetPendingPublishRequestsUseCase(IApplicationDbContext db, IUserService userService)
{
    public async Task<Result<GetPublishRequestsResponse>> ExecuteAsync(
        GetPublishRequestsQuery query,
        CancellationToken ct = default)
    {
        var baseQuery = db.ContentPublishRequests
            .Include(r => r.KnowledgeItem)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<PublishRequestStatus>(query.Status, out var statusEnum))
        {
            baseQuery = baseQuery.Where(r => r.Status == statusEnum);
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var requests = await baseQuery
            .OrderByDescending(r => r.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var userIds = requests.Select(r => r.RequestedByUserId).Distinct();
        var users = await userService.GetUserSummariesAsync(userIds, ct);

        var items = requests.Select(r =>
        {
            var ki = r.KnowledgeItem!;
            users.TryGetValue(r.RequestedByUserId, out var user);

            return new PublishRequestItem(
                Id: r.Id.ToString(),
                KnowledgeItem: new PublishRequestKnowledgeItem(
                    Id: ki.Id.ToString(),
                    Title: ki.Title,
                    Type: ki.Type.ToString(),
                    Summary: ki.Summary),
                RequestedBy: new PublishRequestUser(
                    Id: r.RequestedByUserId.ToString(),
                    DisplayName: user?.DisplayName ?? "Unknown",
                    Role: user?.Role ?? "User"),
                RequestReason: r.RequestReason,
                Status: r.Status.ToString(),
                CreatedAt: r.CreatedAt.ToString("O"),
                ReviewedAt: r.ReviewedAt?.ToString("O"));
        }).ToList();

        return Result<GetPublishRequestsResponse>.SuccessWith(
            new GetPublishRequestsResponse(items, totalCount, query.Page, query.PageSize));
    }
}
