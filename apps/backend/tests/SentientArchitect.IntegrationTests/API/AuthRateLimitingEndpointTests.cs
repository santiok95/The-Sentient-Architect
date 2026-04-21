using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using SentientArchitect.API.Options;
using SentientArchitect.IntegrationTests.Fixtures;
using SentientArchitect.IntegrationTests.Helpers;

namespace SentientArchitect.IntegrationTests.API;

public class AuthRateLimitingEndpointTests
{
    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenBelowThreshold()
    {
        await using var harness = await CreateHarnessAsync();
        harness.LogSink.Clear();

        using var client = harness.CreateClient("198.51.100.10");
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = UniqueEmail(),
            password = "WrongPassword123",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ShouldReturnTooManyRequestsAndLogWarning_WhenEmailThresholdIsExceeded()
    {
        await using var harness = await CreateHarnessAsync();
        using var client = harness.CreateClient("198.51.100.11");
        var email = UniqueEmail();

        await RegisterAsync(client, email, "Password123");
        harness.LogSink.Clear();

        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 5; attempt++)
            response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "WrongPassword123" });

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();

        var payload = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        payload.Should().NotBeNull();
        payload!.Detail.Should().Contain("failed login attempts");

        AssertWarningLog(harness.LogSink, RateLimitPolicies.LoginByEmail, "/api/v1/auth/login", "email");
    }

    [Fact]
    public async Task Login_ShouldResetEmailTracker_AfterSuccessfulLogin()
    {
        await using var harness = await CreateHarnessAsync();
        using var client = harness.CreateClient("198.51.100.12");
        var email = UniqueEmail();
        const string password = "Password123";

        await RegisterAsync(client, email, password);
        harness.LogSink.Clear();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var failed = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "WrongPassword123" });
            failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using (var success = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
        {
            success.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var retry = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "WrongPassword123" });
        retry.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_ShouldLimitPerIp_AndKeepOtherIpAvailable()
    {
        await using var harness = await CreateHarnessAsync();
        using var primaryClient = harness.CreateClient("198.51.100.13");

        for (var attempt = 0; attempt < 5; attempt++)
            await RegisterAsync(primaryClient, UniqueEmail(), "Password123");

        harness.LogSink.Clear();

        using var blocked = await primaryClient.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = UniqueEmail(),
            password = "Password123",
            displayName = "Blocked User",
        });

        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        blocked.Headers.RetryAfter.Should().NotBeNull();
        AssertWarningLog(harness.LogSink, RateLimitPolicies.RegisterByIp, "/api/v1/auth/register", "ip");

        using var otherClient = harness.CreateClient("198.51.100.14");
        using var allowed = await otherClient.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = UniqueEmail(),
            password = "Password123",
            displayName = "Allowed User",
        });

        allowed.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Refresh_ShouldReturnTooManyRequests_WhenIpThresholdIsExceeded()
    {
        await using var harness = await CreateHarnessAsync();
        using var client = harness.CreateClient("198.51.100.15");
        harness.LogSink.Clear();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "invalid-refresh-token" });
            refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using var blocked = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "invalid-refresh-token" });
        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        blocked.Headers.RetryAfter.Should().NotBeNull();
        AssertWarningLog(harness.LogSink, RateLimitPolicies.RefreshByIp, "/api/v1/auth/refresh", "ip");
    }

    private async Task<TestHarness> CreateHarnessAsync()
    {
        var sink = new TestLogSink();
        var factory = new AuthApiFactory($"auth-rate-limit-tests-{Guid.NewGuid():N}", sink);
        _ = factory.CreateClient();
        await factory.InitializeAsync();
        sink.Clear();

        return new TestHarness(factory, sink);
    }

    private static async Task RegisterAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password,
            displayName = "Test User",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static async Task<LoginPayload> LoginAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<LoginPayload>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private static void AssertWarningLog(TestLogSink sink, string policy, string endpoint, string sourceType)
    {
        var hasMatchingLog = sink.Entries.Any(entry =>
            entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning &&
            HasProperty(entry, "Policy", policy) &&
            HasProperty(entry, "Endpoint", endpoint) &&
            HasProperty(entry, "SourceType", sourceType) &&
            entry.Properties.TryGetValue("RetryAfterSeconds", out var retryAfterSeconds) &&
            retryAfterSeconds is int &&
            entry.Properties.TryGetValue("Identifier", out var identifier) &&
            identifier is string value &&
            !string.IsNullOrWhiteSpace(value));

        hasMatchingLog.Should().BeTrue();
    }

    private static bool HasProperty(TestLogEntry entry, string key, string expected)
    {
        return entry.Properties.TryGetValue(key, out var value) &&
               string.Equals(value?.ToString(), expected, StringComparison.Ordinal);
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@test.local";

    private sealed record LoginPayload(string Token, string RefreshToken, int ExpiresIn);

    private sealed record ProblemPayload(string Title, int Status, string Detail);

    private sealed class TestHarness(AuthApiFactory factory, TestLogSink logSink) : IAsyncDisposable
    {
        public TestLogSink LogSink { get; } = logSink;

        public HttpClient CreateClient(string forwardedFor)
        {
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
            client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
            return client;
        }

        public ValueTask DisposeAsync()
        {
            factory.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}