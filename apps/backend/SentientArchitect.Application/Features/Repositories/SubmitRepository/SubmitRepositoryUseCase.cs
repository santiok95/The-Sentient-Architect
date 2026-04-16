using Microsoft.EntityFrameworkCore;
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

        // Same URL + same trust level = duplicate. Same URL + different trust = allowed (different analysis context).
        var alreadyExists = await db.Repositories.AnyAsync(
            r => r.UserId == request.UserId &&
                 r.RepositoryUrl == request.RepositoryUrl &&
                 r.Trust == request.Trust,
            ct);

        if (alreadyExists)
            return Result<SubmitRepositoryResponse>.Failure(
                ["Este repositorio ya fue enviado con el mismo nivel de confianza. Podés enviarlo con un nivel distinto (Internal/External) para obtener un análisis desde otro contexto."],
                ErrorType.Conflict);

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
