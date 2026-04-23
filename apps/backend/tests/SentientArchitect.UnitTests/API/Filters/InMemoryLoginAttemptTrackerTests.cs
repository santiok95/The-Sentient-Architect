using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SentientArchitect.API.Filters;
using SentientArchitect.API.Options;

namespace SentientArchitect.UnitTests.API.Filters;

public class InMemoryLoginAttemptTrackerTests : IDisposable
{
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly InMemoryLoginAttemptTracker _sut;

    public InMemoryLoginAttemptTrackerTests()
    {
        var opts = Options.Create(new AuthRateLimitOptions
        {
            Enabled = true,
            Login = new AuthRateLimitOptions.PolicyOptions
            {
                PermitLimit   = 20,
                WindowSeconds = 300,
            },
        });

        _sut = new InMemoryLoginAttemptTracker(_cache, opts);
    }

    [Fact]
    public void IsBlocked_ShouldReturnFalse_WhenNoFailuresRecorded()
    {
        _sut.IsBlocked("user@example.com").Should().BeFalse();
    }

    [Fact]
    public void IsBlocked_ShouldReturnFalse_WhenBelowThreshold()
    {
        for (var i = 0; i < 4; i++)
            _sut.RecordFailure("user@example.com");

        _sut.IsBlocked("user@example.com").Should().BeFalse();
    }

    [Fact]
    public void IsBlocked_ShouldReturnTrue_WhenThresholdReached()
    {
        for (var i = 0; i < 5; i++)
            _sut.RecordFailure("user@example.com");

        _sut.IsBlocked("user@example.com").Should().BeTrue();
    }

    [Fact]
    public void IsBlocked_ShouldReturnTrue_WhenThresholdExceeded()
    {
        for (var i = 0; i < 10; i++)
            _sut.RecordFailure("user@example.com");

        _sut.IsBlocked("user@example.com").Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_ShouldTreatEmailsCaseInsensitively()
    {
        for (var i = 0; i < 4; i++)
            _sut.RecordFailure("User@Example.COM");

        // Same email, different casing — must count together
        _sut.RecordFailure("user@example.com");

        _sut.IsBlocked("USER@EXAMPLE.COM").Should().BeTrue();
    }

    [Fact]
    public void IsBlocked_ShouldIsolateCountsPerEmail()
    {
        for (var i = 0; i < 5; i++)
            _sut.RecordFailure("attacker@evil.com");

        _sut.IsBlocked("innocent@example.com").Should().BeFalse();
    }

    [Fact]
    public void Reset_ShouldClearRecordedFailures()
    {
        for (var i = 0; i < 5; i++)
            _sut.RecordFailure("user@example.com");

        _sut.Reset("user@example.com");

        _sut.IsBlocked("user@example.com").Should().BeFalse();
    }

    [Fact]
    public void GetRetryAfter_ShouldReturnPositiveValue_WhenEmailIsBlocked()
    {
        for (var i = 0; i < 5; i++)
            _sut.RecordFailure("user@example.com");

        _sut.GetRetryAfter("user@example.com").Should().BeGreaterThan(TimeSpan.Zero);
    }

    public void Dispose() => _cache.Dispose();
}
