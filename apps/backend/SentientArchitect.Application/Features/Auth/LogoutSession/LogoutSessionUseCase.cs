using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Auth.LogoutSession;

public record LogoutSessionRequest(Guid UserId);

public class LogoutSessionUseCase
{
    public Task<Result> ExecuteAsync(
        LogoutSessionRequest request,
        CancellationToken ct = default)
    {
        _ = ct;

        if (request.UserId == Guid.Empty)
            return Task.FromResult(Result.Failure(["User is not authenticated."], ErrorType.Unauthorized));

        return Task.FromResult(Result.Success);
    }
}