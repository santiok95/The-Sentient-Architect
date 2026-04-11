using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Repositories.GetAnalysisReport;

public class GetAnalysisReportUseCase(IApplicationDbContext db)
{
    public async Task<Result<GetAnalysisReportResponse>> ExecuteAsync(
        GetAnalysisReportRequest request,
        CancellationToken ct = default)
    {
        var report = await db.AnalysisReports
            .Include(r => r.Findings)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.ReportId, ct);

        if (report is null)
            return Result<GetAnalysisReportResponse>.Failure([$"Report '{request.ReportId}' not found."], ErrorType.NotFound);

        var findings = report.Findings
            .Select(f => new FindingItem(
                f.Id,
                MapSeverity(f.Severity),
                f.Category,
                f.Message,
                f.FilePath,
                f.LineNumber))
            .ToList();

        var response = new GetAnalysisReportResponse(
            report.Id,
            report.Status.ToString(),
            report.Summary,
            report.TotalFindings,
            report.CriticalFindings,
            report.CompletedAt?.ToString("o"),
            findings);

        return Result<GetAnalysisReportResponse>.SuccessWith(response);
    }

    private static string MapSeverity(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Critical => "Critical",
        FindingSeverity.Error    => "High",
        FindingSeverity.Warning  => "Medium",
        FindingSeverity.Info     => "Low",
        _                        => severity.ToString(),
    };
}
