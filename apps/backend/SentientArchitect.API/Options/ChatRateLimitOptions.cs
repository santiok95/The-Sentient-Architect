namespace SentientArchitect.API.Options;

public sealed class ChatRateLimitOptions
{
    public const string SectionName = "ChatRateLimit";

    public bool Enabled { get; init; } = true;

    /// <summary>Maximum messages a single user can send per window.</summary>
    public int PermitLimit { get; init; } = 20;

    /// <summary>Sliding window in seconds.</summary>
    public int WindowSeconds { get; init; } = 60;
}
