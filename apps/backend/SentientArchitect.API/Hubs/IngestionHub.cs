using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SentientArchitect.API.Hubs;

[Authorize]
public sealed class IngestionHub : Hub
{
    public async Task JoinUser(string userId)
    {
        var currentUserId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? Context.User?.FindFirstValue("sub");

        if (currentUserId is null || currentUserId != userId) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }
}
