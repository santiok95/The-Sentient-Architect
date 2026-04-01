using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Trends.GetTrendSnapshots;

public class GetTrendSnapshotsUseCase(IApplicationDbContext db)
{
    public async Task<Result<GetTrendSnapshotsResponse>> ExecuteAsync(
        GetTrendSnapshotsRequest request,
        CancellationToken ct = default)
    {
        var trend = await db.TechnologyTrends
            .Include(t => t.Snapshots)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TrendId, ct);

        if (trend is null)
            return Result<GetTrendSnapshotsResponse>.Failure([$"Trend '{request.TrendId}' not found."]);

        var snapshots = trend.Snapshots
            .Select(s => new SnapshotItem(s.Date, s.Score, s.Direction, s.Notes))
            .ToList();

        return Result<GetTrendSnapshotsResponse>.SuccessWith(new GetTrendSnapshotsResponse(snapshots));
    }
}
