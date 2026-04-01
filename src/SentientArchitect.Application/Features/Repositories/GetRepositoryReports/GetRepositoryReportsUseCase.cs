using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Application.Features.Repositories.GetRepositoryReports;

public record GetRepositoryReportsRequest(Guid RepositoryId);

public class GetRepositoryReportsUseCase(IApplicationDbContext db)
{
    public async Task<Result<List<AnalysisReport>>> ExecuteAsync(GetRepositoryReportsRequest request, CancellationToken ct = default)
    {
        var reports = await db.AnalysisReports
            .Where(r => r.RepositoryInfoId == request.RepositoryId)
            .AsNoTracking()
            .ToListAsync(ct);

        return Result<List<AnalysisReport>>.SuccessWith(reports);
    }
}
