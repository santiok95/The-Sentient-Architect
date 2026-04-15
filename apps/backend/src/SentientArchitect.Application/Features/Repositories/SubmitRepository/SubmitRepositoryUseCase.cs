using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Application.Features.Repositories.SubmitRepository;

public class SubmitRepositoryUseCase(IApplicationDbContext db)
{
    public async Task<Result<SubmitRepositoryResponse>> ExecuteAsync(
        SubmitRepositoryRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryUrl))
            return Result<SubmitRepositoryResponse>.Failure(["Repository URL is required."]);

        var repo = new RepositoryInfo(
            request.UserId,
            request.TenantId,
            request.RepositoryUrl,
            request.Trust);

        db.Repositories.Add(repo);
        await db.SaveChangesAsync(ct);

        return Result<SubmitRepositoryResponse>.SuccessWith(
            new SubmitRepositoryResponse(repo.Id, repo.RepositoryUrl));
    }
}
