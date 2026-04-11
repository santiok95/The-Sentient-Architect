namespace SentientArchitect.Application.Features.Repositories.GetAnalysisReport;

public record FindingItem(
    Guid Id,
    string Severity,
    string Category,
    string Message,
    string? FilePath,
    int? LineNumber);

public record GetAnalysisReportResponse(
    Guid Id,
    string Status,
    string? Summary,
    int TotalFindings,
    int CriticalFindings,
    string? CompletedAt,
    List<FindingItem> Findings);
