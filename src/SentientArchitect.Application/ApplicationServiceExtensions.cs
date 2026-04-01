using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using System.Reflection;

namespace SentientArchitect.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationServiceExtensions).Assembly;
        
        // Auto-register UseCases
        var types = assembly.GetTypes()
            .Where(t => t.Name.EndsWith("UseCase") && !t.IsAbstract && !t.IsInterface);

        foreach (var type in types)
        {
            services.AddScoped(type);
        }

        // Auto-register FluentValidation Validators
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
