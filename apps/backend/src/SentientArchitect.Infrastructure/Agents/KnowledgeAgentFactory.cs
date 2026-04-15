using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentientArchitect.Infrastructure.Agents.Knowledge;

namespace SentientArchitect.Infrastructure.Agents;

public sealed class KnowledgeAgentFactory
{
    public Kernel CreateKernel(IServiceProvider services)
    {
        var chatService  = services.GetRequiredService<IChatCompletionService>();
        var searchPlugin = services.GetRequiredService<SearchPlugin>();
        var ingestPlugin = services.GetRequiredService<IngestPlugin>();

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        builder.Plugins.AddFromObject(searchPlugin, "Search");
        builder.Plugins.AddFromObject(ingestPlugin, "Ingest");

        return builder.Build();
    }
}
