using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.API.Hubs;

[Authorize]
public sealed class ConversationHub(IApplicationDbContext db, IUserAccessor userAccessor) : Hub
{
    /// <summary>
    /// Joins the SignalR group for a conversation. Only the conversation owner may join.
    /// Prevents other authenticated users from eavesdropping on another user's token stream.
    /// </summary>
    public async Task JoinConversation(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var convGuid))
        {
            await Clients.Caller.SendAsync("ReceiveError", "Invalid conversation ID.");
            return;
        }

        var userId = userAccessor.GetCurrentUserId();
        var exists = await db.Conversations
            .AsNoTracking()
            .AnyAsync(c => c.Id == convGuid && c.UserId == userId);

        if (!exists)
        {
            await Clients.Caller.SendAsync("ReceiveError", "Conversation not found or access denied.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
    }

    public async Task LeaveConversation(string conversationId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);
}
