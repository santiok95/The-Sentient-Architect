namespace SentientArchitect.Application.Common.Interfaces;

public interface ITrendScanner
{
    /// <summary>Runs a full scan cycle and upserts trends into the database.</summary>
    Task ScanAsync(CancellationToken ct = default);
}
