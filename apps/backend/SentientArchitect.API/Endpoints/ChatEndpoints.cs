using Microsoft.AspNetCore.Mvc;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.API.Filters;
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
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();

            var result = await executeChatUseCase.ExecuteAsync(
                new ExecuteChatRequest(
                    conversationId,
                    userId,
                    body.Message,
                    body.ActiveRepositoryId,
                    body.PreferredStack,
                    body.ContextMode),
                ct);

            return result.ToHttpResult();
        })
        .WithName("Chat")
        .WithOpenApi()
        .AddEndpointFilter<ChatMessageLimitsFilter>()  // 1º: largo del mensaje + presupuesto diario
        .AddEndpointFilter<ChatThrottleFilter>();      // 2º: sliding window per-user
    }

    internal record ChatRequest(
        string Message,
        Guid? ActiveRepositoryId = null,
        string? PreferredStack = null,
        ConsultantContextMode? ContextMode = null);
}
