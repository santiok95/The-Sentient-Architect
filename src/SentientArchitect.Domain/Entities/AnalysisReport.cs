using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class AnalysisReport : BaseEntity
{
    public AnalysisReport(Guid repositoryInfoId)
    {
        RepositoryInfoId = repositoryInfoId;
        Status = AnalysisStatus.Pending;
        Findings = new HashSet<AnalysisFinding>();
    }

    private AnalysisReport()
    {
        Findings = new HashSet<AnalysisFinding>();
    }

    public Guid RepositoryInfoId { get; private set; }
    public AnalysisStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int TotalFindings { get; private set; }
    public int CriticalFindings { get; private set; }
    public string? Summary { get; private set; }

    public RepositoryInfo? RepositoryInfo { get; private set; }
    public ICollection<AnalysisFinding> Findings { get; private set; }

    public void MarkInProgress()
    {
        Status = AnalysisStatus.InProgress;
    }

    public void Complete(string summary, int total, int critical)
    {
        Status = AnalysisStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Summary = summary;
        TotalFindings = total;
        CriticalFindings = critical;
    }

    public void Fail(string error)
    {
        Status = AnalysisStatus.Failed;
        ErrorMessage = error;
        CompletedAt = DateTime.UtcNow;
    }
}
