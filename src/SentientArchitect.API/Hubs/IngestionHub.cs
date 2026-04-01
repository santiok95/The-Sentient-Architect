using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SentientArchitect.API.Hubs;

[Authorize]
public sealed class IngestionHub : Hub
{
    public async Task JoinUser(string userId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, userId);
}
