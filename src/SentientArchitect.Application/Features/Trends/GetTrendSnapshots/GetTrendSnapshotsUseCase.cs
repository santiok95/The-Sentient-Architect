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

        static string ToTraction(Domain.Enums.TrendDirection d) => d switch
        {
            Domain.Enums.TrendDirection.Rising    => "Growing",
            Domain.Enums.TrendDirection.Stable    => "Mainstream",
            Domain.Enums.TrendDirection.Declining => "Declining",
            _                                     => "Mainstream",
        };

        var snapshots = trend.Snapshots
            .OrderBy(s => s.Date)
            .Select(s => new SnapshotItem(
                ToTraction(s.Direction),
                s.Score * 100f,
                s.Date.ToString("yyyy-MM-dd"),
                s.Notes))
            .ToList();

        return Result<GetTrendSnapshotsResponse>.SuccessWith(new GetTrendSnapshotsResponse(snapshots));
    }
}
