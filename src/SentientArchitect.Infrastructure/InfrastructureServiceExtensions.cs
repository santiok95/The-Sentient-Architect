using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SentientArchitect.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Future: TokenService, UserAccessor, IdentitySeeder, JWT options
        // These will be registered here as they are implemented

        return services;
    }
}
