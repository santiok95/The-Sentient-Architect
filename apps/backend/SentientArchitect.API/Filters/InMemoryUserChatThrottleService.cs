using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SentientArchitect.API.Options;

namespace SentientArchitect.API.Filters;

/// <summary>
/// Sliding-window per-user chat throttle backed by IMemoryCache.
/// Each user gets at most <see cref="ChatRateLimitOptions.PermitLimit"/> messages
/// within a rolling <see cref="ChatRateLimitOptions.WindowSeconds"/> window.
/// </summary>
internal sealed class InMemoryUserChatThrottleService(
    IMemoryCache cache,
    IOptions<ChatRateLimitOptions> options) : IUserChatThrottleService
{
    private readonly ChatRateLimitOptions _opts = options.Value;

    public bool IsAllowed(Guid userId)
    {
        if (!_opts.Enabled)
            return true;

        var key = CacheKey(userId);
        var window = TimeSpan.FromSeconds(_opts.WindowSeconds);
        var now = DateTimeOffset.UtcNow;

        var state = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new ChatWindowState(0, now.Add(window));
        })!;

        if (state.Count >= _opts.PermitLimit)
            return false;

        // Record this request
        var updated = state with { Count = state.Count + 1 };
        cache.Set(key, updated, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = state.WindowEndsAt,
        });

        return true;
    }

    public TimeSpan GetRetryAfter(Guid userId)
    {
        if (!cache.TryGetValue(CacheKey(userId), out ChatWindowState? state) || state is null)
            return TimeSpan.FromSeconds(_opts.WindowSeconds);

        var remaining = state.WindowEndsAt - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static string CacheKey(Guid userId) =>
        $"chat:throttle:{userId:N}";

    private sealed record ChatWindowState(int Count, DateTimeOffset WindowEndsAt);
}
