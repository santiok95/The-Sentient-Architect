using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Admin.GetPendingPublishRequests;

public class GetPendingPublishRequestsUseCase(IApplicationDbContext db)
{
    public async Task<Result<List<ContentPublishRequest>>> ExecuteAsync(CancellationToken ct = default)
    {
        var requests = await db.ContentPublishRequests
            .Where(r => r.Status == PublishRequestStatus.Pending)
            .AsNoTracking()
            .ToListAsync(ct);

        return Result<List<ContentPublishRequest>>.SuccessWith(requests);
    }
}
