using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class TrendSnapshot : BaseEntity
{
    public TrendSnapshot(Guid technologyTrendId, float score, TrendDirection direction, DateOnly date, string? notes = null)
    {
        TechnologyTrendId = technologyTrendId;
        Score = score;
        Direction = direction;
        Date = date;
        Notes = notes;
    }

    private TrendSnapshot() { }

    public Guid TechnologyTrendId { get; private set; }
    public float Score { get; private set; }
    public TrendDirection Direction { get; private set; }
    public DateOnly Date { get; private set; }
    public string? Notes { get; private set; }

    public TechnologyTrend? TechnologyTrend { get; private set; }
}
