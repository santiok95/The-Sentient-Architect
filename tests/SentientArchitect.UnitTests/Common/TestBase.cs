using Microsoft.EntityFrameworkCore;
using SentientArchitect.Data;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.UnitTests.Common;

/// <summary>
/// Base class for use case tests that need a real EF Core context (in-memory).
/// Each test class gets its own isolated database instance.
/// KnowledgeEmbedding is ignored because its Vector type is Npgsql-specific.
/// </summary>
public abstract class TestBase : IDisposable
{
    protected ApplicationContext DbContext { get; }

    protected TestBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        DbContext = new TestApplicationContext(options);
    }

    public void Dispose()
    {
        DbContext.Dispose();
    }

    // Derived context that ignores Npgsql-specific types unsupported by InMemory provider.
    private sealed class TestApplicationContext(DbContextOptions<ApplicationContext> options)
        : ApplicationContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Ignore<KnowledgeEmbedding>();
        }
    }
}
