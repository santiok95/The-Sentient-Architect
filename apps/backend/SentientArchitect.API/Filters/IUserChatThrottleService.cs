namespace SentientArchitect.API.Filters;

internal interface IUserChatThrottleService
{
    bool IsAllowed(Guid userId);
    TimeSpan GetRetryAfter(Guid userId);
}
