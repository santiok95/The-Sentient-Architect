namespace SentientArchitect.API.Options;

internal static class RateLimitPolicies
{
    internal const string LoginByIp    = "auth:login:ip";
    internal const string LoginByEmail = "auth:login:email";
    internal const string RegisterByIp = "auth:register:ip";
    internal const string RefreshByIp  = "auth:refresh:ip";
}
