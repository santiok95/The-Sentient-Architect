using Microsoft.AspNetCore.Mvc;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Conversations.ArchiveConversation;
using SentientArchitect.Application.Features.Conversations.CreateConversation;
using SentientArchitect.Application.Features.Conversations.DeleteConversation;
using SentientArchitect.Application.Features.Conversations.GetConversationDetail;
using SentientArchitect.Application.Features.Conversations.GetConversations;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.API.Endpoints;

public class ConversationEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations")
            .WithTags("Conversations")
            .RequireAuthorization();

        group.MapPost("/", async (
            [FromBody] CreateConversationHttpRequest body,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] CreateConversationUseCase useCase,
            CancellationToken ct) =>
        {
            var userId   = userAccessor.GetCurrentUserId();
            var tenantId = userAccessor.GetCurrentTenantId();

            var request = new CreateConversationRequest(
                userId,
                tenantId,
                body.Title ?? "Nueva conversación",
                body.AgentType,
                body.ActiveRepositoryId);
            var result  = await useCase.ExecuteAsync(request, ct);

            return result.ToCreatedResult($"/api/v1/conversations/{result.Data?.ConversationId}");
        })
        .WithName("CreateConversation")
        .WithOpenApi();

        group.MapGet("/", async (
            [FromServices] IUserAccessor userAccessor,
            [FromServices] GetConversationsUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(new GetConversationsRequest(userId), ct);
            return result.ToHttpResult();
        })
        .WithName("GetConversations")
        .WithOpenApi();

        group.MapGet("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] GetConversationDetailUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(new GetConversationDetailRequest(id, userId), ct);
            return result.ToHttpResult();
        })
        .WithName("GetConversationDetail")
        .WithOpenApi();

        group.MapPatch("/{id:guid}/archive", async (
            [FromRoute] Guid id,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] ArchiveConversationUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(new ArchiveConversationRequest(id, userId), ct);
            return result.ToHttpResult();
        })
        .WithName("ArchiveConversation")
        .WithOpenApi();

        group.MapDelete("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] DeleteConversationUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(new DeleteConversationRequest(id, userId), ct);
            return result.ToHttpResult();
        })
        .WithName("DeleteConversation")
        .WithOpenApi();
    }

    private record CreateConversationHttpRequest(
        string? Title = null,
        AgentType AgentType = AgentType.Knowledge,
        Guid? ActiveRepositoryId = null);
}
