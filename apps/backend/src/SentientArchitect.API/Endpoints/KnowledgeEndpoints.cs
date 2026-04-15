using Microsoft.AspNetCore.Mvc;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Knowledge.DeleteKnowledgeItem;
using SentientArchitect.Application.Features.Knowledge.GetKnowledgeItems;
using SentientArchitect.Application.Features.Knowledge.IngestKnowledge;
using SentientArchitect.Application.Features.Knowledge.SearchKnowledge;
using SentientArchitect.Application.Features.Knowledge.RequestPublishKnowledge;
using SentientArchitect.Application.Features.Knowledge.GetMyPublishRequests;
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
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var userId   = userAccessor.GetCurrentUserId();
            var tenantId = userAccessor.GetCurrentTenantId();
            var isAdmin  = httpContext.User.IsInRole("Admin");

            if (!TryParseKnowledgeType(body.Type, out var knowledgeType))
            {
                return Results.ValidationProblem(
                    errors: new Dictionary<string, string[]>
                    {
                        ["type"] = ["Tipo inválido. Valores permitidos: Article, Note, Documentation, Repository."]
                    },
                    title: "Ha ocurrido uno o más errores de validación/negocio.");
            }

            var request = new IngestKnowledgeRequest(
                userId,
                tenantId,
                body.Title,
                body.Content ?? string.Empty,
                knowledgeType,
                body.SourceUrl,
                body.Tags,
                IsUserAdmin: isAdmin);

            var result = await useCase.ExecuteAsync(request, ct);

            if (!result.Succeeded)
                return result.ToHttpResult();

            var response = new IngestKnowledgeHttpResponse(
                result.Data!.KnowledgeItemId,
                body.Title,
                ToApiKnowledgeType(knowledgeType),
                result.Data.Status.ToString(),
                result.Data.ChunksCreated);

            return Results.Created($"/api/v1/knowledge/{result.Data.KnowledgeItemId}", response);
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

            if (!result.Succeeded)
                return result.ToHttpResult();

            var response = new SearchKnowledgeHttpResponse(
                result.Data!.Results.Select(r => new KnowledgeSearchResultHttpResponse(
                    r.KnowledgeItemId,
                    r.Title,
                    r.ChunkText,
                    r.Score,
                    ToApiKnowledgeType(r.Type),
                    r.SourceUrl)).ToList(),
                result.Data.TotalFound);

            return Results.Ok(response);
        })
        .WithName("SearchKnowledge")
        .WithOpenApi();

        group.MapGet("/", async (
            [FromServices] IUserAccessor userAccessor,
            [FromServices] GetKnowledgeItemsUseCase useCase,
            CancellationToken ct,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? type = null) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(
                new GetKnowledgeItemsRequest(userId, page, pageSize, search, type), ct);

            if (!result.Succeeded)
                return result.ToHttpResult();

            var data = result.Data!;
            var response = new KnowledgeListHttpResponse(
                data.Items.Select(item => item with { Type = ToApiKnowledgeType(item.Type) }).ToList(),
                data.TotalCount,
                data.Page,
                data.PageSize);

            return Results.Ok(response);
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
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var isAdmin = httpContext.User.IsInRole("Admin");
            var request = new RequestPublishKnowledgeRequest(userId, id, isAdmin, body.Reason);
            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("RequestPublishKnowledge")
        .WithOpenApi();

        group.MapGet("/my-publish-requests", async (
            [FromServices] IUserAccessor userAccessor,
            [FromServices] GetMyPublishRequestsUseCase useCase,
            CancellationToken ct,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(
                new GetMyPublishRequestsRequest(userId, page, pageSize), ct);
            return result.ToHttpResult();
        })
        .WithName("GetMyPublishRequests")
        .WithOpenApi();
    }

    private static bool TryParseKnowledgeType(string rawType, out KnowledgeItemType type)
    {
        if (string.Equals(rawType, "Repository", StringComparison.OrdinalIgnoreCase))
        {
            type = KnowledgeItemType.RepositoryReference;
            return true;
        }

        return Enum.TryParse(rawType, ignoreCase: true, out type);
    }

    private static string ToApiKnowledgeType(KnowledgeItemType type)
        => type == KnowledgeItemType.RepositoryReference ? "Repository" : type.ToString();

    private static string ToApiKnowledgeType(string type)
        => string.Equals(type, nameof(KnowledgeItemType.RepositoryReference), StringComparison.OrdinalIgnoreCase)
            ? "Repository"
            : type;

    public record KnowledgeListHttpResponse(
        List<KnowledgeItemDto> Items,
        int TotalCount,
        int Page,
        int PageSize);

    public record RequestPublishHttpRequest(string? Reason);

    public record IngestKnowledgeHttpRequest(
        string Title,
        string? Content,
        string Type,
        string? SourceUrl = null,
        List<string>? Tags = null);

    public record IngestKnowledgeHttpResponse(
        Guid Id,
        string Title,
        string Type,
        string Status,
        int ChunksCreated);

    public record SearchKnowledgeHttpResponse(
        List<KnowledgeSearchResultHttpResponse> Results,
        int TotalFound);

    public record KnowledgeSearchResultHttpResponse(
        Guid KnowledgeItemId,
        string Title,
        string ChunkText,
        float Score,
        string Type,
        string? SourceUrl);
}
