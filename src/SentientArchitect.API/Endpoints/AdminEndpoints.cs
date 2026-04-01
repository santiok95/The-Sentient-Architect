using Microsoft.EntityFrameworkCore;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Admin.ReviewPublishRequest;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.API.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin")
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        group.MapGet("/publish-requests", GetPendingAsync)
            .WithName("GetPendingPublishRequests")
            .WithOpenApi();

        group.MapPost("/publish-requests/{id:guid}/review", ReviewAsync)
            .WithName("ReviewPublishRequest")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetPendingAsync(
        IApplicationDbContext db,
        CancellationToken ct)
    {
        var requests = await db.ContentPublishRequests
            .Where(r => r.Status == PublishRequestStatus.Pending)
            .AsNoTracking()
            .ToListAsync(ct);
        return Results.Ok(requests);
    }

    private static async Task<IResult> ReviewAsync(
        Guid id,
        ReviewPublishRequestHttpRequest body,
        IUserAccessor userAccessor,
        ReviewPublishRequestUseCase useCase,
        CancellationToken ct)
    {
        var reviewerUserId = userAccessor.GetCurrentUserId();

        var request = new ReviewPublishRequestRequest(id, reviewerUserId, body.Approved, body.RejectionReason);
        var result  = await useCase.ExecuteAsync(request, ct);
        return result.ToHttpResult();
    }

    private record ReviewPublishRequestHttpRequest(bool Approved, string? RejectionReason = null);
}
