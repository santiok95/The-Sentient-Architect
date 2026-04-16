using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Repositories.DeleteRepository;

public record DeleteRepositoryRequest(Guid RepositoryId, Guid UserId);

public class DeleteRepositoryUseCase(IApplicationDbContext db)
{
    public async Task<Result> ExecuteAsync(DeleteRepositoryRequest request, CancellationToken ct = default)
    {
        var repo = await db.Repositories.FindAsync(new object[] { request.RepositoryId }, ct);

        if (repo is null)
            return Result.Success; // idempotent — already gone

        if (repo.UserId != request.UserId)
            return Result.Failure(["Access denied."], ErrorType.Forbidden);

        db.Repositories.Remove(repo);
        await db.SaveChangesAsync(ct);

        return Result.Success;
    }
}
