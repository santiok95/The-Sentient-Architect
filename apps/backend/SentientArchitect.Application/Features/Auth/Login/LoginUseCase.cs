using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

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

public class LoginUseCase(IAuthIdentityService authIdentityService, ITokenService tokenService)
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

        var token = tokenService.CreateToken(
            user.Id,
            user.Email,
            user.DisplayName,
            user.TenantId,
            user.Roles.ToList());

        return Result<LoginResponse>.SuccessWith(new LoginResponse(
            token,
            token,
            tokenService.GetAccessTokenLifetimeSeconds(),
            new AuthSessionUser(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Roles.FirstOrDefault() ?? "User",
                user.TenantId)));
    }
}