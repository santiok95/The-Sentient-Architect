using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace SentientArchitect.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var types = typeof(ApplicationServiceExtensions).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("UseCase") && !t.IsAbstract && !t.IsInterface);

        foreach (var type in types)
        {
            services.AddScoped(type);
        }

        return services;
    }
}
