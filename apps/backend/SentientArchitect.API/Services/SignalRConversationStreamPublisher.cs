using Microsoft.AspNetCore.SignalR;
using SentientArchitect.API.Hubs;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.API.Services;

public sealed class SignalRConversationStreamPublisher(
    IHubContext<ConversationHub> hubContext,
    ILogger<SignalRConversationStreamPublisher> logger) : IConversationStreamPublisher
{
    public async Task PublishTokenAsync(Guid conversationId, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            await hubContext.Clients.Group(conversationId.ToString())
                .SendAsync("ReceiveToken", token, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish chat token for conversation {ConversationId}.", conversationId);
        }
    }

    public async Task PublishCompleteAsync(Guid conversationId, CancellationToken ct = default)
    {
        try
        {
            await hubContext.Clients.Group(conversationId.ToString())
                .SendAsync("ReceiveComplete", cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish chat completion for conversation {ConversationId}.", conversationId);
        }
    }

    public async Task PublishErrorAsync(Guid conversationId, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            await hubContext.Clients.Group(conversationId.ToString())
                .SendAsync("ReceiveError", message, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish chat error for conversation {ConversationId}.", conversationId);
        }
    }
}