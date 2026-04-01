using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Repositories.GetRepositories;

public class GetRepositoriesUseCase(IApplicationDbContext db)
{
    public async Task<Result<GetRepositoriesResponse>> ExecuteAsync(
        GetRepositoriesRequest request,
        CancellationToken ct = default)
    {
        var repos = await db.Repositories
            .Include(r => r.Reports)
            .Where(r => r.UserId == request.UserId)
            .AsNoTracking()
            .ToListAsync(ct);

        var items = repos
            .Select(r => new RepositoryItem(
                r.Id,
                r.RepositoryUrl,
                r.Trust,
                r.LastAnalyzedAt,
                r.Reports.Count))
            .ToList();

        return Result<GetRepositoriesResponse>.SuccessWith(new GetRepositoriesResponse(items));
    }
}
