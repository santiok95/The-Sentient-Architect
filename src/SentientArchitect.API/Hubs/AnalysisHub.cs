using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SentientArchitect.API.Hubs;

[Authorize]
public sealed class AnalysisHub : Hub
{
    public async Task JoinRepository(string repositoryId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, repositoryId);
}
