using Microsoft.EntityFrameworkCore;
using SentientArchitect.Data;

namespace SentientArchitect.UnitTests.Common;

/// <summary>
/// Base class for use case tests that need a real EF Core context (in-memory).
/// Each test class gets its own isolated database instance.
/// </summary>
public abstract class TestBase : IDisposable
{
    protected ApplicationContext DbContext { get; }

    protected TestBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        DbContext = new ApplicationContext(options);
    }

    public void Dispose()
    {
        DbContext.Dispose();
    }
}
