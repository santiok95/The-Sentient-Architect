using Microsoft.AspNetCore.Mvc;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Admin.GetPendingPublishRequests;
using SentientArchitect.Application.Features.Admin.ReviewPublishRequest;

namespace SentientArchitect.API.Endpoints;

public class AdminEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin")
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        group.MapGet("/publish-requests", async (
            [FromServices] GetPendingPublishRequestsUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("GetPendingPublishRequests")
        .WithOpenApi();

        group.MapPost("/publish-requests/{id:guid}/review", async (
            [FromRoute] Guid id,
            [FromBody] ReviewPublishRequestHttpRequest body,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] ReviewPublishRequestUseCase useCase,
            CancellationToken ct) =>
        {
            var reviewerUserId = userAccessor.GetCurrentUserId();

            var request = new ReviewPublishRequestRequest(id, reviewerUserId, body.Approved, body.RejectionReason);
            var result  = await useCase.ExecuteAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("ReviewPublishRequest")
        .WithOpenApi();

        group.MapPost("/trends/sync", async (
            [FromServices] ITrendScanner scanner,
            CancellationToken ct) =>
        {
            // Fire-and-forget — don't block the HTTP response
            _ = Task.Run(() => scanner.ScanAsync(CancellationToken.None));
            return Results.Accepted("/api/v1/admin/trends/sync", new
            {
                message = "Trend scan initiated in background.",
                estimatedDurationMinutes = 5
            });
        })
        .WithName("TriggerTrendScan")
        .WithOpenApi();
    }

    private record ReviewPublishRequestHttpRequest(bool Approved, string? RejectionReason = null);
}
