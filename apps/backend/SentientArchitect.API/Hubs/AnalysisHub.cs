using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.API.Hubs;

[Authorize]
public sealed class AnalysisHub(IApplicationDbContext db) : Hub
{
    public async Task JoinRepository(string repositoryId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        if (!Guid.TryParse(repositoryId, out var repoGuid)) return;

        var owned = await db.Repositories
            .AsNoTracking()
            .AnyAsync(r => r.Id == repoGuid && r.UserId == userId);

        if (!owned) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, repositoryId);
    }

    public async Task LeaveRepository(string repositoryId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, repositoryId);

    private Guid GetUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? Context.User?.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
