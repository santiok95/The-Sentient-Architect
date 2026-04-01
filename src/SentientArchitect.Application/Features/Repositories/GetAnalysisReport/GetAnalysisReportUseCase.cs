using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

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
            return Result<GetAnalysisReportResponse>.Failure([$"Report '{request.ReportId}' not found."]);

        var findings = report.Findings
            .Select(f => new FindingItem(f.Severity, f.Category, f.Message, f.FilePath, f.LineNumber))
            .ToList();

        var response = new GetAnalysisReportResponse(
            report.Id,
            report.Status,
            report.Summary,
            report.TotalFindings,
            report.CriticalFindings,
            report.CompletedAt,
            findings);

        return Result<GetAnalysisReportResponse>.SuccessWith(response);
    }
}
