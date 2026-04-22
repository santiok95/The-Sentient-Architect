using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SentientArchitect.API.Filters;

internal sealed class AuthRateLimitRejectedResult(
    string detail,
    TimeSpan retryAfter,
    string policy,
    string sourceType,
    string? identifier = null) : IResult
{
    private readonly TimeSpan _retryAfter = retryAfter > TimeSpan.Zero
        ? retryAfter
        : TimeSpan.FromSeconds(60);

    public Task ExecuteAsync(HttpContext httpContext)
    {
        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(_retryAfter.TotalSeconds));
        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SentientArchitect.API.AuthRateLimit");

        logger.LogWarning(
            "Auth rate limit rejected. Policy {Policy} Endpoint {Endpoint} SourceType {SourceType} RetryAfterSeconds {RetryAfterSeconds} Identifier {Identifier}",
            policy,
            httpContext.Request.Path.Value ?? string.Empty,
            sourceType,
            retryAfterSeconds,
            AnonymizeIdentifier(identifier));

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.ContentType = "application/problem+json";
        httpContext.Response.Headers.RetryAfter =
            retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        return httpContext.Response.WriteAsJsonAsync(new
        {
            title = "Demasiados intentos.",
            status = StatusCodes.Status429TooManyRequests,
            detail,
        }, httpContext.RequestAborted);
    }

    private static string AnonymizeIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return "unknown";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identifier.Trim().ToLowerInvariant()));
        return Convert.ToHexStringLower(bytes)[..12];
    }
}