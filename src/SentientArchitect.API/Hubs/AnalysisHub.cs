using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.API.Hubs;

[Authorize]
public sealed class AnalysisHub(IApplicationDbContext db, IUserAccessor userAccessor) : Hub
{
    /// <summary>
    /// Joins the SignalR group for a repository analysis stream.
    /// Only the repository owner may join — prevents eavesdropping on another user's analysis.
    /// </summary>
    public async Task JoinRepository(string repositoryId)
    {
        if (!Guid.TryParse(repositoryId, out var repoGuid))
        {
            await Clients.Caller.SendAsync("ReceiveError", "Invalid repository ID.");
            return;
        }

        var userId = userAccessor.GetCurrentUserId();
        var exists = await db.Repositories
            .AsNoTracking()
            .AnyAsync(r => r.Id == repoGuid && r.UserId == userId);

        if (!exists)
        {
            await Clients.Caller.SendAsync("ReceiveError", "Repository not found or access denied.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, repositoryId);
    }

    public async Task LeaveRepository(string repositoryId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, repositoryId);
}
