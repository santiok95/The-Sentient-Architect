using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Data;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Infrastructure.Identity;
using SentientArchitect.IntegrationTests.Helpers;

namespace SentientArchitect.IntegrationTests.Fixtures;

internal sealed class AuthApiFactory(string databaseName, TestLogSink logSink) : WebApplicationFactory<Program>
{
    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        await db.Database.EnsureCreatedAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Startup:SkipDatabaseInitialization"] = "true",
                ["Jwt:Key"] = "test-secret-key-minimum-32-chars!!",
                ["Jwt:Issuer"] = "SentientArchitect",
                ["Jwt:Audience"] = "SentientArchitect",
                ["Jwt:ExpiresInHours"] = "1",
                ["Seeder:AdminEmail"] = "admin@test.local",
                ["Seeder:AdminPassword"] = "Admin123!",
                ["Conversation:CompactionThreshold"] = "20",
                ["AuthRateLimit:Enabled"] = "true",
                ["AuthRateLimit:Login:PermitLimit"] = "20",
                ["AuthRateLimit:Login:WindowSeconds"] = "300",
                ["AuthRateLimit:Register:PermitLimit"] = "5",
                ["AuthRateLimit:Register:WindowSeconds"] = "900",
                ["AuthRateLimit:Refresh:PermitLimit"] = "10",
                ["AuthRateLimit:Refresh:WindowSeconds"] = "300",
                ["AI:Anthropic:ApiKey"] = string.Empty,
                ["AI:OpenAI:ApiKey"] = string.Empty,
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new TestLoggerProvider(logSink));
        });

        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                services.Remove(descriptor);

            RemoveDbRegistrations(services);

            services.AddScoped(_ => new DbContextOptionsBuilder<ApplicationContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
            services.AddScoped<ApplicationContext>(sp =>
                new AuthTestApplicationContext(sp.GetRequiredService<DbContextOptions<ApplicationContext>>()));
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationContext>());

            services.PostConfigure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
            {
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });
        });
    }

    private static void RemoveDbRegistrations(IServiceCollection services)
    {
        var descriptors = services.Where(descriptor =>
                descriptor.ServiceType == typeof(ApplicationContext) ||
                descriptor.ServiceType == typeof(DbContextOptions<ApplicationContext>) ||
                descriptor.ServiceType == typeof(IApplicationDbContext))
            .ToList();

        foreach (var descriptor in descriptors)
            services.Remove(descriptor);
    }

    private sealed class AuthTestApplicationContext(DbContextOptions<ApplicationContext> options)
        : ApplicationContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Ignore<KnowledgeEmbedding>();
        }
    }
}