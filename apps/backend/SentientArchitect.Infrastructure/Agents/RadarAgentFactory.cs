using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentientArchitect.Infrastructure.Agents.Consultant;
using SentientArchitect.Infrastructure.Agents.Knowledge;

namespace SentientArchitect.Infrastructure.Agents;

public sealed class RadarAgentFactory
{
    public Kernel CreateKernel(IServiceProvider services)
    {
        var chatService  = services.GetRequiredService<IChatCompletionService>();
        var trendsPlugin = services.GetRequiredService<TrendsPlugin>();
        var searchPlugin = services.GetRequiredService<SearchPlugin>();

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        builder.Plugins.AddFromObject(trendsPlugin, "Trends");
        builder.Plugins.AddFromObject(searchPlugin, "Search");

        return builder.Build();
    }
}
