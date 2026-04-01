using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Features.Trends.GetTrendSnapshots;
using SentientArchitect.Application.Features.Trends.GetTrends;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.API.Endpoints;

public static class TrendEndpoints
{
    public static IEndpointRouteBuilder MapTrendEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/trends")
            .WithTags("Trends");

        group.MapGet("/", GetAllAsync)
            .WithName("GetTrends")
            .WithOpenApi();

        group.MapGet("/{id:guid}/snapshots", GetSnapshotsAsync)
            .WithName("GetTrendSnapshots")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        GetTrendsUseCase useCase,
        CancellationToken ct,
        TrendCategory? category = null)
    {
        var result = await useCase.ExecuteAsync(new GetTrendsRequest(category), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetSnapshotsAsync(
        Guid id,
        GetTrendSnapshotsUseCase useCase,
        CancellationToken ct)
    {
        var result = await useCase.ExecuteAsync(new GetTrendSnapshotsRequest(id), ct);
        return result.ToHttpResult();
    }
}
