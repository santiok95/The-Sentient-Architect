using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Common.Interfaces;

public sealed record AuthIdentityUser(
    Guid Id,
    string Email,
    string DisplayName,
    Guid TenantId,
    bool IsActive,
    IReadOnlyList<string> Roles);

public sealed record RegisterIdentityUserRequest(
    string Email,
    string Password,
    string DisplayName);

public interface IAuthIdentityService
{
    Task<Result<AuthIdentityUser>> RegisterAsync(
        RegisterIdentityUserRequest request,
        CancellationToken ct = default);

    Task<AuthIdentityUser?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct = default);

    Task<AuthIdentityUser?> FindByIdAsync(
        Guid userId,
        CancellationToken ct = default);
}