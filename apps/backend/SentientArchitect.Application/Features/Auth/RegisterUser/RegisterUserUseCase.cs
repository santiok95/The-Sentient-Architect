using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Auth.RegisterUser;

public record RegisterUserRequest(string Email, string Password, string DisplayName);

public record RegisterUserResponse(Guid UserId, string Email, string DisplayName);

public class RegisterUserUseCase(IAuthIdentityService authIdentityService)
{
    public async Task<Result<RegisterUserResponse>> ExecuteAsync(
        RegisterUserRequest request,
        CancellationToken ct = default)
    {
        var result = await authIdentityService.RegisterAsync(
            new RegisterIdentityUserRequest(request.Email, request.Password, request.DisplayName),
            ct);

        if (!result.Succeeded || result.Data is null)
            return Result<RegisterUserResponse>.Failure(result.Errors, result.ErrorType);

        return Result<RegisterUserResponse>.SuccessWith(
            new RegisterUserResponse(result.Data.Id, result.Data.Email, result.Data.DisplayName));
    }
}