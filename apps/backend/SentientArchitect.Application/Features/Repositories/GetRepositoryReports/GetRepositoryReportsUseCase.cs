using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Application.Features.Repositories.GetRepositoryReports;

public record GetRepositoryReportsRequest(Guid RepositoryId, Guid UserId);

public class GetRepositoryReportsUseCase(IApplicationDbContext db)
{
    public async Task<Result<List<AnalysisReport>>> ExecuteAsync(GetRepositoryReportsRequest request, CancellationToken ct = default)
    {
        var repositoryExists = await db.Repositories
            .AsNoTracking()
            .AnyAsync(r => r.Id == request.RepositoryId && r.UserId == request.UserId, ct);

        if (!repositoryExists)
            return Result<List<AnalysisReport>>.Failure(
                [$"Repository '{request.RepositoryId}' not found."],
                ErrorType.NotFound);

        var reports = await db.AnalysisReports
            .Where(r => r.RepositoryInfoId == request.RepositoryId)
            .AsNoTracking()
            .ToListAsync(ct);

        return Result<List<AnalysisReport>>.SuccessWith(reports);
    }
}
