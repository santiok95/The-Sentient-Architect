using System.Text;
using Anthropic.SDK;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
#pragma warning disable SKEXP0001
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
#pragma warning restore SKEXP0001
#pragma warning disable SKEXP0010
using Microsoft.SemanticKernel.Connectors.OpenAI;
#pragma warning restore SKEXP0010
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Data;
using SentientArchitect.Infrastructure.AI;
using SentientArchitect.Infrastructure.Agents;
using SentientArchitect.Infrastructure.Agents.Consultant;
using SentientArchitect.Infrastructure.Agents.Knowledge;
using SentientArchitect.Infrastructure.Identity;

namespace SentientArchitect.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── ASP.NET Identity ─────────────────────────────────────────────────
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit           = true;
                options.Password.RequireLowercase       = true;
                options.Password.RequireUppercase       = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength         = 8;
                options.User.RequireUniqueEmail         = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationContext>();

        // ── JWT Authentication ───────────────────────────────────────────────
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer           = true,
                    ValidIssuer              = configuration["Jwt:Issuer"] ?? "SentientArchitect",
                    ValidateAudience         = true,
                    ValidAudience            = configuration["Jwt:Audience"] ?? "SentientArchitect",
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero,
                };
            });

        // ── HTTP Context ─────────────────────────────────────────────────────
        services.AddHttpContextAccessor();

        // ── Identity services ────────────────────────────────────────────────
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUserAccessor, UserAccessor>();
        services.AddScoped<IdentitySeeder>();

        // ── AI — Chat (Anthropic) ─────────────────────────────────────────────
        var anthropicKey = configuration["AI:Anthropic:ApiKey"];
        if (!string.IsNullOrWhiteSpace(anthropicKey))
        {
            IChatClient chatClient = new AnthropicClient(anthropicKey).Messages
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();

#pragma warning disable SKEXP0001
            services.AddSingleton<IChatCompletionService>(
                chatClient.AsChatCompletionService());
#pragma warning restore SKEXP0001
        }

        // ── AI — Embeddings (OpenAI) ───────────────────────────────────────────
        var openAiKey = configuration["AI:OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
#pragma warning disable SKEXP0010
            var embeddingGenerator = new OpenAITextEmbeddingGenerationService(
                "text-embedding-3-small", openAiKey);
#pragma warning restore SKEXP0010
#pragma warning disable SKEXP0001
            services.AddSingleton<ITextEmbeddingGenerationService>(embeddingGenerator);
#pragma warning restore SKEXP0001
            services.AddScoped<IEmbeddingService, OpenAIEmbeddingService>();
        }
        else
        {
            services.AddScoped<IEmbeddingService, NullEmbeddingService>();
        }

        // ── SK Plugins (Scoped — they use DbContext) ───────────────────────────
        services.AddScoped<SearchPlugin>();
        services.AddScoped<IngestPlugin>();
        services.AddScoped<ProfilePlugin>();
        services.AddScoped<SummaryPlugin>();

        // ── Agent Factories (Singleton) ────────────────────────────────────────
        services.AddSingleton<KnowledgeAgentFactory>();
        services.AddSingleton<ConsultantAgentFactory>();

        return services;
    }

    /// <summary>
    /// Seeds roles and the initial admin user. Call this from Program.cs after the app is built.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope  = serviceProvider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync();
    }
}
