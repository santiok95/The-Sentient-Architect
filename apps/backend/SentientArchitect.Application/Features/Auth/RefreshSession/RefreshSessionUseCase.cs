using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Application.Features.Auth.RefreshSession;

public record RefreshSessionRequest(string RefreshToken);

public record RefreshSessionResponse(string Token, string RefreshToken);

public class RefreshSessionUseCase(IAuthIdentityService authIdentityService, ITokenService tokenService, IApplicationDbContext db)
{
    public async Task<Result<RefreshSessionResponse>> ExecuteAsync(
        RefreshSessionRequest request,
        CancellationToken ct = default)
    {
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken, ct);

        if (stored is null || !stored.IsValid)
            return Result<RefreshSessionResponse>.Failure(["La sesión expiró o no es válida. Iniciá sesión nuevamente."], ErrorType.Unauthorized);

        var user = await authIdentityService.FindByIdAsync(stored.UserId, ct);
        if (user is null || !user.IsActive)
            return Result<RefreshSessionResponse>.Failure(["La sesión expiró o no es válida. Iniciá sesión nuevamente."], ErrorType.Unauthorized);

        // Rotate: revoke old token, issue new pair
        stored.Revoke();

        var newAccessToken = tokenService.CreateToken(
            user.Id,
            user.Email,
            user.DisplayName,
            user.TenantId,
            user.Roles.ToList());

        var newRefreshToken = tokenService.GenerateRefreshToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(tokenService.GetRefreshTokenLifetimeDays());
        db.RefreshTokens.Add(new RefreshToken(user.Id, newRefreshToken, expiresAt));

        await db.SaveChangesAsync(ct);

        return Result<RefreshSessionResponse>.SuccessWith(
            new RefreshSessionResponse(newAccessToken, newRefreshToken));
    }
}
