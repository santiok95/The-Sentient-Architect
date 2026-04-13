using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class TechnologyTrend : BaseEntity
{
    public TechnologyTrend(string name, TrendCategory category)
    {
        Name = name;
        Category = category;
        Direction = TrendDirection.Stable;
        RelevanceScore = 0f;
        Sources = [];
        LastScannedAt = DateTime.UtcNow;
        Snapshots = new HashSet<TrendSnapshot>();
    }

    private TechnologyTrend()
    {
        Sources = [];
        Snapshots = new HashSet<TrendSnapshot>();
    }

    public string Name { get; private set; } = string.Empty;
    public TrendCategory Category { get; private set; }
    public TrendDirection Direction { get; private set; }
    public float RelevanceScore { get; private set; }
    public string? Description { get; private set; }
    public List<string> Sources { get; private set; }
    public DateTime LastScannedAt { get; private set; }
    public int? StarCount { get; private set; }
    public string? GitHubUrl { get; private set; }

    public ICollection<TrendSnapshot> Snapshots { get; private set; }

    public void UpdateRelevance(float score, TrendDirection direction, string? description)
    {
        RelevanceScore = score;
        Direction = direction;
        Description = description;
        LastScannedAt = DateTime.UtcNow;
    }

    public void UpdateGitHubStats(int starCount, string? githubUrl)
    {
        StarCount = starCount;
        if (!string.IsNullOrWhiteSpace(githubUrl))
            GitHubUrl = githubUrl;
    }

    public void AddSource(string url)
    {
        if (!Sources.Contains(url))
            Sources.Add(url);
    }

    public void RecordSnapshot(TrendSnapshot snapshot)
    {
        Snapshots.Add(snapshot);
    }
}
