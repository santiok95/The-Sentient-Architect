using Microsoft.AspNetCore.SignalR;
using SentientArchitect.API.Hubs;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.API.Services;

public sealed class AnalysisProgressReporter(IHubContext<AnalysisHub> hubContext) : IAnalysisProgressReporter
{
    public Task ReportProgressAsync(Guid repositoryId, int percent, string status, CancellationToken ct = default)
        => hubContext.Clients.Group(repositoryId.ToString()).SendAsync("ReceiveProgress", percent, status, ct);

    public Task ReportCompleteAsync(Guid repositoryId, Guid reportId, CancellationToken ct = default)
        => hubContext.Clients.Group(repositoryId.ToString()).SendAsync("ReceiveComplete", reportId, ct);

    public Task ReportErrorAsync(Guid repositoryId, string message, CancellationToken ct = default)
        => hubContext.Clients.Group(repositoryId.ToString()).SendAsync("ReceiveError", message, ct);
}
