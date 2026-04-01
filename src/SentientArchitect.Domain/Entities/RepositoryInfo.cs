using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class RepositoryInfo : BaseEntity
{
    public RepositoryInfo(Guid userId, Guid tenantId, string repositoryUrl, RepositoryTrust trust)
    {
        UserId = userId;
        TenantId = tenantId;
        RepositoryUrl = repositoryUrl;
        Trust = trust;
        Reports = new HashSet<AnalysisReport>();
    }

    private RepositoryInfo()
    {
        Reports = new HashSet<AnalysisReport>();
    }

    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string RepositoryUrl { get; private set; } = string.Empty;
    public string? LocalPath { get; private set; }
    public RepositoryTrust Trust { get; private set; }
    public string? DefaultBranch { get; private set; }
    public int? StarCount { get; private set; }
    public int? ContributorCount { get; private set; }
    public DateTime? LastCommitAt { get; private set; }
    public DateTime? LastAnalyzedAt { get; private set; }

    public ICollection<AnalysisReport> Reports { get; private set; }

    public void SetLocalPath(string path)
    {
        LocalPath = path;
    }

    public void ClearLocalPath()
    {
        LocalPath = null;
    }

    public void UpdateGitMetadata(string? branch, int? stars, int? contributors, DateTime? lastCommit)
    {
        DefaultBranch = branch;
        StarCount = stars;
        ContributorCount = contributors;
        LastCommitAt = lastCommit;
    }

    public void MarkAnalyzed()
    {
        LastAnalyzedAt = DateTime.UtcNow;
    }
}
