using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SentientArchitect.Data.Postgres;

/// <summary>
/// Design-time factory for EF Core CLI migrations.
/// Usage: dotnet ef migrations add Initial -p SentientArchitect.Data.Postgres -s SentientArchitect.API
/// 
/// To set environment: $Env:ASPNETCORE_ENVIRONMENT = "Development"
/// </summary>
public class PostgresApplicationContextFactory
    : IDbContextFactory<ApplicationContext>, IDesignTimeDbContextFactory<ApplicationContext>
{
    private const string ConnectionName = "Postgres";

    public ApplicationContext CreateDbContext() => CreateDbContext(Array.Empty<string>());

    public ApplicationContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        var connectionString = configuration.GetConnectionString(ConnectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionName}' was not found in API appsettings.");
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationContext>();

        optionsBuilder.UseNpgsql(dataSource, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(PostgresApplicationContextFactory).Assembly.FullName);
            npgsql.UseVector();
            npgsql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        });

        return new ApplicationContext(optionsBuilder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var apiBasePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SentientArchitect.API"));

        return new ConfigurationBuilder()
            .SetBasePath(apiBasePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }
}
