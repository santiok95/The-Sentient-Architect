using System.Reflection;

namespace SentientArchitect.API.Common.Endpoints;

public static class EndpointModuleExtensions
{
    public static IEndpointRouteBuilder MapEndpointModules(this IEndpointRouteBuilder root)
    {
        var types = typeof(EndpointModuleExtensions).Assembly.GetTypes()
            .Where(t => typeof(IEndpointModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        foreach (var t in types)
        {
            if (Activator.CreateInstance(t) is IEndpointModule module)
                module.Map(root);
        }

        return root;
    }
}
