using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.API.Filters;

/// <summary>
/// Endpoint filter applied to POST /conversations/{id}/chat.
/// Rejects requests from users who exceed the configured sliding-window limit.
/// </summary>
internal sealed class ChatThrottleFilter(
    IUserChatThrottleService throttle,
    IUserAccessor userAccessor) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var userId = userAccessor.GetCurrentUserId();

        if (userId == Guid.Empty)
            return await next(ctx);

        if (!throttle.IsAllowed(userId))
        {
            var retryAfter = throttle.GetRetryAfter(userId);
            return new AuthRateLimitRejectedResult(
                "Demasiadas consultas en poco tiempo. Esperá un momento antes de continuar.",
                retryAfter,
                "chat:per-user",
                "user",
                userId.ToString());
        }

        return await next(ctx);
    }
}
