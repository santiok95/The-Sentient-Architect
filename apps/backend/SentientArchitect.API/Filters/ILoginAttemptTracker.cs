namespace SentientArchitect.API.Filters;

internal interface ILoginAttemptTracker
{
    bool IsBlocked(string email);
    void RecordFailure(string email);
    void Reset(string email);
    TimeSpan GetRetryAfter(string email);
}
