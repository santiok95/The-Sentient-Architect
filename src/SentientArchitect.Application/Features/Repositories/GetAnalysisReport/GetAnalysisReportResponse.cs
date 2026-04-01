using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Repositories.GetAnalysisReport;

public record FindingItem(
    FindingSeverity Severity,
    string Category,
    string Message,
    string? FilePath,
    int? LineNumber);

public record GetAnalysisReportResponse(
    Guid Id,
    AnalysisStatus Status,
    string? Summary,
    int TotalFindings,
    int CriticalFindings,
    DateTime? CompletedAt,
    List<FindingItem> Findings);
