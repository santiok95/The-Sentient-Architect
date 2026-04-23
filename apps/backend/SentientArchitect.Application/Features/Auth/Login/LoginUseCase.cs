using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Application.Features.Auth.Login;

public record LoginRequest(string Email, string Password);

public record AuthSessionUser(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    Guid TenantId);

public record LoginResponse(
    string Token,
    string RefreshToken,
    int ExpiresIn,
    AuthSessionUser User);

public class LoginUseCase(IAuthIdentityService authIdentityService, ITokenService tokenService, IApplicationDbContext db)
{
    public async Task<Result<LoginResponse>> ExecuteAsync(
        LoginRequest request,
        CancellationToken ct = default)
    {
        var user = await authIdentityService.ValidateCredentialsAsync(request.Email, request.Password, ct);

        if (user is null)
            return Result<LoginResponse>.Failure(["El correo electrónico o la contraseña son incorrectos."], ErrorType.Unauthorized);

        if (!user.IsActive)
            return Result<LoginResponse>.Failure(["Tu cuenta está inactiva. Contactá al soporte para más información."], ErrorType.Unauthorized);

        var accessToken = tokenService.CreateToken(
            user.Id,
            user.Email,
            user.DisplayName,
            user.TenantId,
            user.Roles.ToList());

        var rawRefreshToken = tokenService.GenerateRefreshToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(tokenService.GetRefreshTokenLifetimeDays());
        db.RefreshTokens.Add(new RefreshToken(user.Id, rawRefreshToken, expiresAt));
        await db.SaveChangesAsync(ct);

        return Result<LoginResponse>.SuccessWith(new LoginResponse(
            accessToken,
            rawRefreshToken,
            tokenService.GetAccessTokenLifetimeSeconds(),
            new AuthSessionUser(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Roles.FirstOrDefault() ?? "User",
                user.TenantId)));
    }
}