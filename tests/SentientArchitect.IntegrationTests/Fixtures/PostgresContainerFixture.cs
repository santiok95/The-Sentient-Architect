using Microsoft.EntityFrameworkCore;
using Npgsql;
using SentientArchitect.Data;
using SentientArchitect.Data.Postgres;
using Testcontainers.PostgreSql;

namespace SentientArchitect.IntegrationTests.Fixtures;

/// <summary>
/// Shared test fixture: starts one PostgreSQL container per test class collection,
/// applies EF Core migrations, and disposes after all tests finish.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("sentient_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public ApplicationContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_container.GetConnectionString());
        dataSourceBuilder.UseVector();
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseNpgsql(dataSource, npgsql =>
            {
                npgsql.UseVector();
                npgsql.MigrationsAssembly(typeof(DataPostgresServiceExtensions).Assembly.FullName);
            })
            .Options;

        Context = new ApplicationContext(options);
        await Context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture> { }
