namespace SentientArchitect.Application.Common.Interfaces;

public interface IAnalysisProgressReporter
{
    Task ReportProgressAsync(Guid repositoryId, int percent, string status, CancellationToken ct = default);
    Task ReportCompleteAsync(Guid repositoryId, Guid reportId, CancellationToken ct = default);
    Task ReportErrorAsync(Guid repositoryId, string message, CancellationToken ct = default);
}
