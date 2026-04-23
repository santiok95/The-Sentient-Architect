namespace SentientArchitect.API.Options;

public sealed class AuthRateLimitOptions
{
    public const string SectionName = "AuthRateLimit";

    public bool Enabled { get; init; } = true;

    public PolicyOptions Login { get; init; } = new();
    public PolicyOptions Register { get; init; } = new();
    public PolicyOptions Refresh { get; init; } = new();

    public sealed class PolicyOptions
    {
        public int PermitLimit { get; init; }
        public int WindowSeconds { get; init; }
    }
}
