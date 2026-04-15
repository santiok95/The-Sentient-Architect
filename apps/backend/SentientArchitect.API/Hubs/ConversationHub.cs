using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.API.Hubs;

[Authorize]
public sealed class ConversationHub(IApplicationDbContext db) : Hub
{
    public async Task JoinConversation(string conversationId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        if (!Guid.TryParse(conversationId, out var convGuid)) return;

        var owned = await db.Conversations
            .AsNoTracking()
            .AnyAsync(c => c.Id == convGuid && c.UserId == userId);

        if (!owned) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
    }

    public async Task LeaveConversation(string conversationId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);

    private Guid GetUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? Context.User?.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
