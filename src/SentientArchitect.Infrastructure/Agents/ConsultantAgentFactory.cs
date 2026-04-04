using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentientArchitect.Infrastructure.Agents.Consultant;
using SentientArchitect.Infrastructure.Agents.Knowledge;

namespace SentientArchitect.Infrastructure.Agents;

public sealed class ConsultantAgentFactory
{
    public Kernel CreateKernel(IServiceProvider services)
    {
        var chatService             = services.GetRequiredService<IChatCompletionService>();
        var profilePlugin           = services.GetRequiredService<ProfilePlugin>();
        var summaryPlugin           = services.GetRequiredService<SummaryPlugin>();
        var searchPlugin            = services.GetRequiredService<SearchPlugin>();
        var repositoryContextPlugin = services.GetRequiredService<RepositoryContextPlugin>();

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        builder.Plugins.AddFromObject(profilePlugin,           "Profile");
        builder.Plugins.AddFromObject(summaryPlugin,           "Summary");
        builder.Plugins.AddFromObject(searchPlugin,            "Search");
        builder.Plugins.AddFromObject(repositoryContextPlugin, "RepositoryContext");

        return builder.Build();
    }
}
