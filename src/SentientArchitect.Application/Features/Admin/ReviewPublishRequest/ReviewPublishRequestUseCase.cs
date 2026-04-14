using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Constants;

namespace SentientArchitect.Application.Features.Admin.ReviewPublishRequest;

public class ReviewPublishRequestUseCase(IApplicationDbContext db)
{
    public async Task<Result> ExecuteAsync(
        ReviewPublishRequestRequest request,
        CancellationToken ct = default)
    {
        var publishRequest = await db.ContentPublishRequests
            .Include(r => r.KnowledgeItem)
            .FirstOrDefaultAsync(r => r.Id == request.RequestId, ct);

        if (publishRequest is null)
            return Result.Failure(["Publish request not found."]);

        if (request.Action.Equals("Approve", StringComparison.OrdinalIgnoreCase))
        {
            publishRequest.Approve(request.ReviewerUserId);
            publishRequest.KnowledgeItem!.PublishToShared(TenantIds.Shared);
        }
        else if (request.Action.Equals("Reject", StringComparison.OrdinalIgnoreCase))
        {
            var reason = request.RejectionReason ?? "No reason provided.";
            publishRequest.Reject(request.ReviewerUserId, reason);
        }
        else
        {
            return Result.Failure([$"Unknown action '{request.Action}'. Use 'Approve' or 'Reject'."]);
        }

        await db.SaveChangesAsync(ct);

        return Result.Success;
    }
}
