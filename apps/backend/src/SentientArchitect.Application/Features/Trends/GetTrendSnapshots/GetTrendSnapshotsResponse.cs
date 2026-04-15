namespace SentientArchitect.Application.Features.Trends.GetTrendSnapshots;

public record SnapshotItem(
    string TractionLevel,
    float SentimentScore,
    string SnapshotDate,
    string? Notes);

public record GetTrendSnapshotsResponse(List<SnapshotItem> Snapshots);
