using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.API.Hubs;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Conversations.Chat;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.API.Endpoints;

public class ChatEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations")
            .WithTags("Chat")
            .RequireAuthorization();

        group.MapPost("/{conversationId:guid}/chat", async (
            [FromRoute] Guid conversationId,
            [FromBody] ChatRequest body,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] ExecuteChatUseCase executeChatUseCase,
            [FromServices] IHubContext<ConversationHub> hubContext,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var groupName = conversationId.ToString();
            var streamedAnyToken = false;

            var result = await executeChatUseCase.ExecuteAsync(
                new ExecuteChatRequest(
                    conversationId,
                    userId,
                    body.Message,
                    body.ActiveRepositoryId,
                    body.PreferredStack,
                    body.ContextMode),
                async (token, tokenCt) =>
                {
                    streamedAnyToken = true;
                    await hubContext.Clients.Group(groupName)
                        .SendAsync("ReceiveToken", token, cancellationToken: tokenCt);
                },
                ct);

            if (!result.Succeeded)
            {
                await hubContext.Clients.Group(groupName)
                    .SendAsync("ReceiveError", string.Join("; ", result.Errors), ct);

                return result.ToHttpResult();
            }

            if (!streamedAnyToken &&
                result.Data is not null &&
                !string.IsNullOrWhiteSpace(result.Data.AssistantMessage))
            {
                await hubContext.Clients.Group(groupName)
                    .SendAsync("ReceiveToken", result.Data.AssistantMessage, ct);
            }

            await hubContext.Clients.Group(groupName).SendAsync("ReceiveComplete", ct);

            return result.ToHttpResult();
        })
        .WithName("Chat")
        .WithOpenApi();
    }

    private record ChatRequest(
        string Message,
        Guid? ActiveRepositoryId = null,
        string? PreferredStack = null,
        ConsultantContextMode? ContextMode = null);
}
