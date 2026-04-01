using Microsoft.EntityFrameworkCore;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Knowledge.IngestKnowledge;
using SentientArchitect.Application.Features.Knowledge.SearchKnowledge;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.API.Endpoints;

public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/knowledge")
            .WithTags("Knowledge")
            .RequireAuthorization();

        group.MapPost("/", IngestAsync)
            .WithName("IngestKnowledge")
            .WithOpenApi();

        group.MapGet("/search", SearchAsync)
            .WithName("SearchKnowledge")
            .WithOpenApi();

        group.MapGet("/", ListAsync)
            .WithName("ListKnowledge")
            .WithOpenApi();

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteKnowledge")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> IngestAsync(
        IngestKnowledgeHttpRequest body,
        IUserAccessor userAccessor,
        IngestKnowledgeUseCase useCase,
        CancellationToken ct)
    {
        var userId   = userAccessor.GetCurrentUserId();
        var tenantId = userAccessor.GetCurrentTenantId();

        var request = new IngestKnowledgeRequest(
            userId,
            tenantId,
            body.Title,
            body.OriginalContent,
            body.Type,
            body.SourceUrl,
            body.Tags);

        var result = await useCase.ExecuteAsync(request, ct);

        return result.ToCreatedResult($"/api/v1/knowledge/{result.Data?.KnowledgeItemId}");
    }

    private static async Task<IResult> SearchAsync(
        string q,
        IUserAccessor userAccessor,
        SearchKnowledgeUseCase useCase,
        CancellationToken ct,
        int maxResults = 5,
        bool includeShared = true)
    {
        var userId   = userAccessor.GetCurrentUserId();
        var tenantId = userAccessor.GetCurrentTenantId();

        var request = new SearchKnowledgeRequest(
            userId,
            tenantId,
            q,
            maxResults,
            includeShared);

        var result = await useCase.ExecuteAsync(request, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> ListAsync(
        IUserAccessor userAccessor,
        IApplicationDbContext db,
        CancellationToken ct)
    {
        var userId = userAccessor.GetCurrentUserId();
        var items  = await db.KnowledgeItems
            .Where(k => k.UserId == userId)
            .AsNoTracking()
            .ToListAsync(ct);
        return Results.Ok(items);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        IApplicationDbContext db,
        IVectorStore vectorStore,
        CancellationToken ct)
    {
        await vectorStore.DeleteByKnowledgeItemAsync(id, ct);

        var item = await db.KnowledgeItems.FindAsync([id], ct);
        if (item is not null)
        {
            db.KnowledgeItems.Remove(item);
            await db.SaveChangesAsync(ct);
        }

        return Results.NoContent();
    }

    private record IngestKnowledgeHttpRequest(
        string Title,
        string OriginalContent,
        KnowledgeItemType Type,
        string? SourceUrl = null,
        List<string>? Tags = null);
}
