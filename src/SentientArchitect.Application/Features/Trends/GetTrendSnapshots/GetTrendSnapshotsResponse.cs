using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Trends.GetTrendSnapshots;

public record SnapshotItem(
    DateOnly Date,
    float Score,
    TrendDirection Direction,
    string? Notes);

public record GetTrendSnapshotsResponse(List<SnapshotItem> Snapshots);
