using Microsoft.AspNetCore.Mvc;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Repositories.GetAnalysisReport;
using SentientArchitect.Application.Features.Repositories.GetRepositories;
using SentientArchitect.Application.Features.Repositories.GetRepositoryReports;
using SentientArchitect.Application.Features.Repositories.SubmitRepository;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.API.Endpoints;

public class RepositoryEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/repositories")
            .WithTags("Repositories")
            .RequireAuthorization();

        group.MapPost("/", async (
            [FromBody] SubmitRepositoryHttpRequest body,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] SubmitRepositoryUseCase useCase,
            CancellationToken ct) =>
        {
            var userId   = userAccessor.GetCurrentUserId();
            var tenantId = userAccessor.GetCurrentTenantId();

            var request = new SubmitRepositoryRequest(userId, tenantId, body.RepositoryUrl, body.Trust);
            var result  = await useCase.ExecuteAsync(request, ct);

            return result.ToCreatedResult($"/api/v1/repositories/{result.Data?.RepositoryId}");
        })
        .WithName("SubmitRepository")
        .WithOpenApi();

        group.MapGet("/", async (
            [FromServices] IUserAccessor userAccessor,
            [FromServices] GetRepositoriesUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(new GetRepositoriesRequest(userId), ct);
            return result.ToHttpResult();
        })
        .WithName("GetRepositories")
        .WithOpenApi();

        group.MapGet("/{id:guid}/reports", async (
            [FromRoute] Guid id,
            [FromServices] GetRepositoryReportsUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new GetRepositoryReportsRequest(id), ct);
            return result.ToHttpResult();
        })
        .WithName("GetRepositoryReports")
        .WithOpenApi();

        group.MapGet("/reports/{reportId:guid}", async (
            [FromRoute] Guid reportId,
            [FromServices] GetAnalysisReportUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new GetAnalysisReportRequest(reportId), ct);
            return result.ToHttpResult();
        })
        .WithName("GetAnalysisReport")
        .WithOpenApi();
    }

    private record SubmitRepositoryHttpRequest(string RepositoryUrl, RepositoryTrust Trust);
}
