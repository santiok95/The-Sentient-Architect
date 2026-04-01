using Microsoft.EntityFrameworkCore;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Repositories.GetAnalysisReport;
using SentientArchitect.Application.Features.Repositories.GetRepositories;
using SentientArchitect.Application.Features.Repositories.SubmitRepository;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.API.Endpoints;

public static class RepositoryEndpoints
{
    public static IEndpointRouteBuilder MapRepositoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/repositories")
            .WithTags("Repositories")
            .RequireAuthorization();

        group.MapPost("/", SubmitAsync)
            .WithName("SubmitRepository")
            .WithOpenApi();

        group.MapGet("/", GetAllAsync)
            .WithName("GetRepositories")
            .WithOpenApi();

        group.MapGet("/{id:guid}/reports", GetReportsAsync)
            .WithName("GetRepositoryReports")
            .WithOpenApi();

        group.MapGet("/reports/{reportId:guid}", GetReportAsync)
            .WithName("GetAnalysisReport")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> SubmitAsync(
        SubmitRepositoryHttpRequest body,
        IUserAccessor userAccessor,
        SubmitRepositoryUseCase useCase,
        CancellationToken ct)
    {
        var userId   = userAccessor.GetCurrentUserId();
        var tenantId = userAccessor.GetCurrentTenantId();

        var request = new SubmitRepositoryRequest(userId, tenantId, body.RepositoryUrl, body.Trust);
        var result  = await useCase.ExecuteAsync(request, ct);

        return result.ToCreatedResult($"/api/v1/repositories/{result.Data?.RepositoryId}");
    }

    private static async Task<IResult> GetAllAsync(
        IUserAccessor userAccessor,
        GetRepositoriesUseCase useCase,
        CancellationToken ct)
    {
        var userId = userAccessor.GetCurrentUserId();
        var result = await useCase.ExecuteAsync(new GetRepositoriesRequest(userId), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetReportsAsync(
        Guid id,
        IApplicationDbContext db,
        CancellationToken ct)
    {
        var reports = await db.AnalysisReports
            .Where(r => r.RepositoryInfoId == id)
            .AsNoTracking()
            .ToListAsync(ct);
        return Results.Ok(reports);
    }

    private static async Task<IResult> GetReportAsync(
        Guid reportId,
        GetAnalysisReportUseCase useCase,
        CancellationToken ct)
    {
        var result = await useCase.ExecuteAsync(new GetAnalysisReportRequest(reportId), ct);
        return result.ToHttpResult();
    }

    private record SubmitRepositoryHttpRequest(string RepositoryUrl, RepositoryTrust Trust);
}
