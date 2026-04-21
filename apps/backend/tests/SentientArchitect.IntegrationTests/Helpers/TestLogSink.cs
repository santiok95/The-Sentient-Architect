using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SentientArchitect.IntegrationTests.Helpers;

public sealed class TestLogSink
{
    private readonly ConcurrentQueue<TestLogEntry> _entries = new();

    public IReadOnlyCollection<TestLogEntry> Entries => _entries.ToArray();

    public void Add(TestLogEntry entry) => _entries.Enqueue(entry);

    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
        }
    }
}

public sealed record TestLogEntry(
    LogLevel Level,
    string Category,
    string Message,
    IReadOnlyDictionary<string, object?> Properties);

public sealed class TestLoggerProvider(TestLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, sink);

    public void Dispose()
    {
    }

    private sealed class TestLogger(string categoryName, TestLogSink sink) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> stateProperties
                ? stateProperties.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();

            sink.Add(new TestLogEntry(
                logLevel,
                categoryName,
                formatter(state, exception),
                properties));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}