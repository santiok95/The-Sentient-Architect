using Microsoft.AspNetCore.Identity;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Data;

namespace SentientArchitect.Infrastructure.Identity;

public sealed class IdentityAuthService(UserManager<ApplicationUser> userManager) : IAuthIdentityService
{
    public async Task<Result<AuthIdentityUser>> RegisterAsync(
        RegisterIdentityUserRequest request,
        CancellationToken ct = default)
    {
        _ = ct;

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            TenantId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return MapFailure<AuthIdentityUser>(createResult);

        var roleResult = await userManager.AddToRoleAsync(user, "User");
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return MapFailure<AuthIdentityUser>(roleResult, ErrorType.Failure);
        }

        return Result<AuthIdentityUser>.SuccessWith(await ToAuthIdentityUserAsync(user));
    }

    public async Task<AuthIdentityUser?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        _ = ct;

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return null;

        var isValid = await userManager.CheckPasswordAsync(user, password);
        return !isValid ? null : await ToAuthIdentityUserAsync(user);
    }

    public async Task<AuthIdentityUser?> FindByIdAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        _ = ct;

        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : await ToAuthIdentityUserAsync(user);
    }

    private async Task<AuthIdentityUser> ToAuthIdentityUserAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);

        return new AuthIdentityUser(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.TenantId,
            user.IsActive,
            roles.ToList());
    }

    private static Result<T> MapFailure<T>(IdentityResult result, ErrorType? overrideType = null)
    {
        var errorType = overrideType ?? (result.Errors.Any(error =>
            error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
                ? ErrorType.Conflict
                : ErrorType.Validation);

        return Result<T>.Failure(result.Errors.Select(error => error.Description), errorType);
    }
}