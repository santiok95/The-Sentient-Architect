using Microsoft.Extensions.Options;
using SentientArchitect.API.Options;
using SentientArchitect.Application.Features.Auth.Login;

namespace SentientArchitect.API.Filters;

internal sealed class LoginFailedByEmailFilter(
    ILoginAttemptTracker tracker,
    IOptions<AuthRateLimitOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        if (!options.Value.Enabled)
            return await next(ctx);

        var request = ctx.Arguments.OfType<LoginRequest>().FirstOrDefault();

        if (request is null)
            return await next(ctx);

        if (tracker.IsBlocked(request.Email))
            return BuildRejection(request.Email, tracker.GetRetryAfter(request.Email));

        var result = await next(ctx);

        if (IsFailedLogin(result))
        {
            tracker.RecordFailure(request.Email);

            if (tracker.IsBlocked(request.Email))
                return BuildRejection(request.Email, tracker.GetRetryAfter(request.Email));

            return result;
        }

        tracker.Reset(request.Email);

        return result;
    }

    private static bool IsFailedLogin(object? result) =>
        result is Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult;

    private static IResult BuildRejection(string email, TimeSpan retryAfter) =>
        new AuthRateLimitRejectedResult(
            "Demasiados intentos fallidos. Esperá un momento antes de volver a intentarlo.",
            retryAfter,
            RateLimitPolicies.LoginByEmail,
            "email",
            email);
}
