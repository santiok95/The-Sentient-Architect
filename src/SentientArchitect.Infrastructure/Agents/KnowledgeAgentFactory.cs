using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using SentientArchitect.Infrastructure.Agents.Knowledge;

namespace SentientArchitect.Infrastructure.Agents;

public sealed class KnowledgeAgentFactory
{
    private const string Instructions = """
        You are the Knowledge Agent for The Sentient Architect.

        Your role is to help developers store and retrieve technical knowledge.

        You have access to:
        - SearchByMeaning: Search the knowledge base with natural language queries
        - IngestContent: Store new technical content in the knowledge base

        Guidelines:
        - When the user asks a question, ALWAYS search the knowledge base first
        - When the user shares knowledge to save, use IngestContent
        - Return clear, structured answers citing the knowledge sources
        - If nothing relevant is found, say so honestly
        """;

    public ChatCompletionAgent Create(
        IChatCompletionService chatService,
        SearchPlugin searchPlugin,
        IngestPlugin ingestPlugin)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        builder.Plugins.AddFromObject(searchPlugin, "Search");
        builder.Plugins.AddFromObject(ingestPlugin, "Ingest");

        var kernel = builder.Build();

        return new ChatCompletionAgent
        {
            Name         = "KnowledgeAgent",
            Instructions = Instructions,
            Kernel       = kernel,
            Arguments    = new KernelArguments(
                new PromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                }),
        };
    }
}
