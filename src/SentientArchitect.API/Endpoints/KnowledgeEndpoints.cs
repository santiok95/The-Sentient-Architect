using Microsoft.AspNetCore.Mvc;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Knowledge.DeleteKnowledgeItem;
using SentientArchitect.Application.Features.Knowledge.GetKnowledgeItems;
using SentientArchitect.Application.Features.Knowledge.IngestKnowledge;
using SentientArchitect.Application.Features.Knowledge.SearchKnowledge;
using SentientArchitect.Application.Features.Knowledge.RequestPublishKnowledge;
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
            HttpContext httpContext,
            CancellationToken ct,
            [FromQuery] int maxResults = 5,
            [FromQuery] bool includeShared = true,
            [FromQuery] float minimumScore = 0.35f) =>
        {
            var userId   = userAccessor.GetCurrentUserId();
            var tenantId = userAccessor.GetCurrentTenantId();
            var includeAllScopes = httpContext.User.IsInRole("Admin");

            var request = new SearchKnowledgeRequest(
                userId,
                tenantId,
                q,
                maxResults,
                includeShared,
                minimumScore,
                includeAllScopes);

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
        group.MapPost("/{id:guid}/publish", async (
            [FromRoute] Guid id,
            [FromBody] RequestPublishHttpRequest body,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] RequestPublishKnowledgeUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var request = new RequestPublishKnowledgeRequest(userId, id, body.Reason);
            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToHttpResult(); // Returns 200 OK or generic map depending on extensions
        })
        .WithName("RequestPublishKnowledge")
        .WithOpenApi();
    }

    public record RequestPublishHttpRequest(string? Reason);
    
    private record IngestKnowledgeHttpRequest(
        string Title,
        string OriginalContent,
        KnowledgeItemType Type,
        string? SourceUrl = null,
        List<string>? Tags = null);
}
