using Microsoft.AspNetCore.Mvc;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Trends.GetTrendSnapshots;
using SentientArchitect.Application.Features.Trends.GetTrends;

namespace SentientArchitect.API.Endpoints;

public class TrendEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/trends")
            .WithTags("Trends");

        group.MapGet("/", async (
            [FromServices] GetTrendsUseCase useCase,
            CancellationToken ct,
            [FromQuery] string? category = null,
            [FromQuery] string? traction = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20) =>
        {
            var result = await useCase.ExecuteAsync(
                new GetTrendsRequest(category, traction, page, pageSize), ct);
            return result.ToHttpResult();
        })
        .WithName("GetTrends")
        .WithOpenApi();

        group.MapGet("/{id:guid}/snapshots", async (
            [FromRoute] Guid id,
            [FromServices] GetTrendSnapshotsUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new GetTrendSnapshotsRequest(id), ct);
            return result.ToHttpResult();
        })
        .WithName("GetTrendSnapshots")
        .WithOpenApi();
    }
}
