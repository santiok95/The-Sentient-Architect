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

        // GET /api/v1/admin/publish-requests?status=Pending&page=1&pageSize=50
        group.MapGet("/publish-requests", async (
            [FromQuery] string? status,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromServices] GetPendingPublishRequestsUseCase useCase,
            CancellationToken ct) =>
        {
            var query = new GetPublishRequestsQuery(status, page < 1 ? 1 : page, pageSize < 1 ? 50 : pageSize);
            var result = await useCase.ExecuteAsync(query, ct);
            return result.ToHttpResult();
        })
        .WithName("GetPublishRequests")
        .WithOpenApi();

        // PATCH /api/v1/admin/publish-requests/{id}
        group.MapPatch("/publish-requests/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] ReviewPublishRequestHttpRequest body,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] ReviewPublishRequestUseCase useCase,
            CancellationToken ct) =>
        {
            var reviewerUserId = userAccessor.GetCurrentUserId();
            var request = new ReviewPublishRequestRequest(id, reviewerUserId, body.Action, body.RejectionReason);
            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("ReviewPublishRequest")
        .WithOpenApi();

        // POST /api/v1/admin/trends/sync
        group.MapPost("/trends/sync", async (
            [FromServices] ITrendScanner scanner,
            CancellationToken ct) =>
        {
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

    private record ReviewPublishRequestHttpRequest(string Action, string? RejectionReason = null);
}
