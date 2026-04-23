using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Repositories.EnqueueRepositoryAnalysis;

public record EnqueueRepositoryAnalysisRequest(Guid RepositoryId, Guid UserId);

public class EnqueueRepositoryAnalysisUseCase(
    IApplicationDbContext db,
    IAnalysisProgressReporter progressReporter)
{
    public async Task<Result<Guid>> ExecuteAsync(
        EnqueueRepositoryAnalysisRequest request,
        CancellationToken ct = default)
    {
        var repositoryExists = await db.Repositories
            .AsNoTracking()
            .AnyAsync(r => r.Id == request.RepositoryId && r.UserId == request.UserId, ct);

        if (!repositoryExists)
        {
            return Result<Guid>.Failure(
                [$"Repository '{request.RepositoryId}' not found."],
                ErrorType.NotFound);
        }

        var activeReport = await db.AnalysisReports
            .AsNoTracking()
            .Where(r => r.RepositoryInfoId == request.RepositoryId)
            .Where(r => r.Status == AnalysisStatus.Pending || r.Status == AnalysisStatus.InProgress)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.Status })
            .FirstOrDefaultAsync(ct);

        if (activeReport is not null)
            return Result<Guid>.SuccessWith(activeReport.Id);

        var report = new AnalysisReport(request.RepositoryId);
        db.AnalysisReports.Add(report);
        await db.SaveChangesAsync(ct);

        await progressReporter.ReportProgressAsync(request.RepositoryId, 0, "Analisis en cola...", ct);

        return Result<Guid>.SuccessWith(report.Id);
    }
}