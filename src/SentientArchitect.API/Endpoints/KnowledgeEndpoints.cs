using Microsoft.AspNetCore.Mvc;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Knowledge.DeleteKnowledgeItem;
using SentientArchitect.Application.Features.Knowledge.GetKnowledgeItems;
using SentientArchitect.Application.Features.Knowledge.IngestKnowledge;
using SentientArchitect.Application.Features.Knowledge.SearchKnowledge;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.API.Endpoints;

public class KnowledgeEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/knowledge")
            .WithTags("Knowledge")
            .RequireAuthorization();

        group.MapPost("/", async (
            [FromBody] IngestKnowledgeHttpRequest body,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] IngestKnowledgeUseCase useCase,
            CancellationToken ct) =>
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
        })
        .WithName("IngestKnowledge")
        .WithOpenApi();

        group.MapGet("/search", async (
            [FromQuery] string q,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] SearchKnowledgeUseCase useCase,
            CancellationToken ct,
            [FromQuery] int maxResults = 5,
            [FromQuery] bool includeShared = true) =>
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
        })
        .WithName("SearchKnowledge")
        .WithOpenApi();

        group.MapGet("/", async (
            [FromServices] IUserAccessor userAccessor,
            [FromServices] GetKnowledgeItemsUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(new GetKnowledgeItemsRequest(userId), ct);
            return result.ToHttpResult();
        })
        .WithName("ListKnowledge")
        .WithOpenApi();

        group.MapDelete("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] DeleteKnowledgeItemUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new DeleteKnowledgeItemRequest(id), ct);
            return result.ToHttpResult();
        })
        .WithName("DeleteKnowledge")
        .WithOpenApi();
    }

    private record IngestKnowledgeHttpRequest(
        string Title,
        string OriginalContent,
        KnowledgeItemType Type,
        string? SourceUrl = null,
        List<string>? Tags = null);
}
