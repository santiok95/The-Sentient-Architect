using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Admin.ReviewPublishRequest;

public class ReviewPublishRequestUseCase(IApplicationDbContext db)
{
    public async Task<Result> ExecuteAsync(
        ReviewPublishRequestRequest request,
        CancellationToken ct = default)
    {
        var publishRequest = await db.ContentPublishRequests
            .FirstOrDefaultAsync(r => r.Id == request.RequestId, ct);

        if (publishRequest is null)
            return Result.Failure(["Publish request not found."]);

        if (request.Approved)
        {
            publishRequest.Approve(request.ReviewerUserId);
        }
        else
        {
            var reason = request.RejectionReason ?? "No reason provided.";
            publishRequest.Reject(request.ReviewerUserId, reason);
        }

        await db.SaveChangesAsync(ct);

        return Result.Success;
    }
}
