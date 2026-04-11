using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Repositories.GetRepositoryAnalysis;

public record GetRepositoryAnalysisRequest(Guid RepositoryId);

public record AnalysisReportSummary(
    Guid Id,
    string Status,
    string? Summary,
    int TotalFindings,
    int CriticalFindings,
    FindingsBreakdown FindingsCount,
    float OverallHealthScore,
    float SecurityScore,
    float QualityScore,
    float MaintainabilityScore,
    string? ExecutedAt,
    double AnalysisDurationSeconds);

public record FindingsBreakdown(int Critical, int High, int Medium, int Low);

public record RepositoryInfoSummary(
    string GitUrl,
    string? PrimaryLanguage,
    string TrustLevel,
    int? Stars,
    string? LastCommitDate);

public record GetRepositoryAnalysisResponse(
    RepositoryInfoSummary RepositoryInfo,
    List<AnalysisReportSummary> Reports);

public class GetRepositoryAnalysisUseCase(IApplicationDbContext db)
{
    public async Task<Result<GetRepositoryAnalysisResponse>> ExecuteAsync(
        GetRepositoryAnalysisRequest request,
        CancellationToken ct = default)
    {
        var repo = await db.Repositories
            .Include(r => r.Reports)
                .ThenInclude(rep => rep.Findings)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RepositoryId, ct);

        if (repo is null)
            return Result<GetRepositoryAnalysisResponse>.Failure(
                [$"Repository '{request.RepositoryId}' not found."],
                ErrorType.NotFound);

        var repoInfo = new RepositoryInfoSummary(
            GitUrl:          repo.RepositoryUrl,
            PrimaryLanguage: repo.PrimaryLanguage,
            TrustLevel:      repo.Trust.ToString(),
            Stars:           repo.StarCount,
            LastCommitDate:  repo.LastCommitAt?.ToString("o"));

        var reports = repo.Reports
            .OrderByDescending(r => r.CreatedAt)
            .Select(r =>
            {
                var findings = r.Findings.ToList();
                var critical = findings.Count(f => f.Severity == FindingSeverity.Critical);
                var high     = findings.Count(f => f.Severity == FindingSeverity.High);
                var medium   = findings.Count(f => f.Severity == FindingSeverity.Medium);
                var low      = findings.Count(f => f.Severity == FindingSeverity.Low);

                // Derive scores from findings when dedicated score fields are not available
                var penalty = (critical * 25f) + (high * 10f) + (medium * 3f) + (low * 0.5f);
                var health   = Math.Max(0f, 100f - penalty);

                var duration = r.CompletedAt.HasValue
                    ? (r.CompletedAt.Value - r.CreatedAt).TotalSeconds
                    : 0;

                return new AnalysisReportSummary(
                    Id:                      r.Id,
                    Status:                  r.Status.ToString(),
                    Summary:                 r.Summary,
                    TotalFindings:           r.TotalFindings,
                    CriticalFindings:        r.CriticalFindings,
                    FindingsCount:           new FindingsBreakdown(critical, high, medium, low),
                    OverallHealthScore:      MathF.Round(health, 1),
                    SecurityScore:           MathF.Round(Math.Max(0f, 100f - (critical * 30f) - (high * 15f)), 1),
                    QualityScore:            MathF.Round(Math.Max(0f, 100f - (medium * 5f) - (low * 1f)), 1),
                    MaintainabilityScore:    MathF.Round(Math.Max(0f, 100f - (high * 8f) - (medium * 4f)), 1),
                    ExecutedAt:              r.CompletedAt?.ToString("o"),
                    AnalysisDurationSeconds: Math.Round(duration, 1));
            })
            .ToList();

        return Result<GetRepositoryAnalysisResponse>.SuccessWith(
            new GetRepositoryAnalysisResponse(repoInfo, reports));
    }
}
