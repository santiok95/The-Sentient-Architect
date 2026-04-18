using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Auth.RefreshSession;

public record RefreshSessionRequest(string RefreshToken);

public record RefreshSessionResponse(string Token, string RefreshToken);

public class RefreshSessionUseCase(IAuthIdentityService authIdentityService, ITokenService tokenService)
{
    public async Task<Result<RefreshSessionResponse>> ExecuteAsync(
        RefreshSessionRequest request,
        CancellationToken ct = default)
    {
        var userId = tokenService.GetUserIdFromToken(request.RefreshToken, allowExpired: true);

        if (!userId.HasValue)
            return Result<RefreshSessionResponse>.Failure(["Invalid refresh token."], ErrorType.Unauthorized);

        var user = await authIdentityService.FindByIdAsync(userId.Value, ct);
        if (user is null || !user.IsActive)
            return Result<RefreshSessionResponse>.Failure(["Invalid refresh token."], ErrorType.Unauthorized);

        var newToken = tokenService.CreateToken(
            user.Id,
            user.Email,
            user.DisplayName,
            user.TenantId,
            user.Roles.ToList());

        return Result<RefreshSessionResponse>.SuccessWith(
            new RefreshSessionResponse(newToken, newToken));
    }
}