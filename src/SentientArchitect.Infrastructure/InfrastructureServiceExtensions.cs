using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Data;
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
