using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.API.Hubs;

[Authorize]
public sealed class IngestionHub(IUserAccessor userAccessor) : Hub
{
    /// <summary>
    /// Joins the SignalR group for ingestion progress notifications.
    /// Validates that the requested userId matches the authenticated caller —
    /// prevents a user from subscribing to another user's ingestion feed.
    /// </summary>
    public async Task JoinUser(string userId)
    {
        if (!Guid.TryParse(userId, out var requestedUserId))
        {
            await Clients.Caller.SendAsync("ReceiveError", "Invalid user ID.");
            return;
        }

        var callerId = userAccessor.GetCurrentUserId();
        if (callerId != requestedUserId)
        {
            await Clients.Caller.SendAsync("ReceiveError", "Access denied.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }
}
