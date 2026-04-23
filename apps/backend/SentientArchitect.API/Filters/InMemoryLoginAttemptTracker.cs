using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SentientArchitect.API.Options;

namespace SentientArchitect.API.Filters;

internal sealed class InMemoryLoginAttemptTracker(
    IMemoryCache cache,
    IOptions<AuthRateLimitOptions> options) : ILoginAttemptTracker
{
    private const int FailedAttemptLimit = 5;

    public bool IsBlocked(string email)
    {
        var state = GetState(email);
        return state is not null &&
               state.FailureCount >= FailedAttemptLimit &&
               state.ExpiresAt > DateTimeOffset.UtcNow;
    }

    public void RecordFailure(string email)
    {
        var key = CacheKey(email);
        var window = TimeSpan.FromSeconds(options.Value.Login.WindowSeconds);
        var expiresAt = DateTimeOffset.UtcNow.Add(window);
        var currentCount = GetState(email)?.FailureCount ?? 0;

        cache.Set(key, new LoginAttemptState(currentCount + 1, expiresAt), new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = expiresAt,
        });
    }

    public void Reset(string email)
    {
        cache.Remove(CacheKey(email));
    }

    public TimeSpan GetRetryAfter(string email)
    {
        var state = GetState(email);
        if (state is null || state.FailureCount < FailedAttemptLimit)
            return TimeSpan.Zero;

        var retryAfter = state.ExpiresAt - DateTimeOffset.UtcNow;
        return retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero;
    }

    private static string CacheKey(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return $"auth:login:email:{Convert.ToHexStringLower(bytes)}";
    }

    private LoginAttemptState? GetState(string email)
    {
        return cache.TryGetValue(CacheKey(email), out LoginAttemptState? state)
            ? state
            : null;
    }

    private sealed record LoginAttemptState(int FailureCount, DateTimeOffset ExpiresAt);
}
