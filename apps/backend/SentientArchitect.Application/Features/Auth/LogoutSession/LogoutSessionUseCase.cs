using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Auth.LogoutSession;

public record LogoutSessionRequest(Guid UserId);

public class LogoutSessionUseCase(IApplicationDbContext db)
{
    public async Task<Result> ExecuteAsync(
        LogoutSessionRequest request,
        CancellationToken ct = default)
    {
        if (request.UserId == Guid.Empty)
            return Result.Failure(["No hay una sesión activa para cerrar."], ErrorType.Unauthorized);

        var tokens = await db.RefreshTokens
            .Where(r => r.UserId == request.UserId && !r.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.Revoke();

        if (tokens.Count > 0)
            await db.SaveChangesAsync(ct);

        return Result.Success;
    }
}
