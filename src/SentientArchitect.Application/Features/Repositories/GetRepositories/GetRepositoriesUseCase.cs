using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Enums;

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

        var items = repos.Select(r =>
        {
            var latest = r.Reports.OrderByDescending(rep => rep.CreatedAt).FirstOrDefault();
            var status = latest?.Status switch
            {
                AnalysisStatus.InProgress => "Processing",
                AnalysisStatus.Completed  => "Completed",
                AnalysisStatus.Failed     => "Failed",
                _                         => "Pending",
            };
            return new RepositoryItem(
                Id:              r.Id,
                GitUrl:          r.RepositoryUrl,
                TrustLevel:      r.Trust.ToString(),
                PrimaryLanguage: null,
                Stars:           r.StarCount,
                LastCommitDate:  r.LastCommitAt?.ToString("o"),
                ProcessingStatus: status,
                Scope:           r.UserId == r.TenantId ? "Personal" : "Shared",
                CreatedAt:       r.CreatedAt.ToString("o"));
        }).ToList();

        return Result<GetRepositoriesResponse>.SuccessWith(new GetRepositoriesResponse(items, items.Count));
    }
}
