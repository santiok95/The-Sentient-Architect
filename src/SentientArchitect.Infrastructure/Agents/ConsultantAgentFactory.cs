using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using SentientArchitect.Infrastructure.Agents.Consultant;
using SentientArchitect.Infrastructure.Agents.Knowledge;

namespace SentientArchitect.Infrastructure.Agents;

public sealed class ConsultantAgentFactory
{
    private const string Instructions = """
        You are the Architecture Consultant for The Sentient Architect.

        Your role is to provide expert software architecture advice tailored to the developer's context.

        You have access to:
        - GetUserProfile: Get the developer's tech stack, experience level, and preferences
        - GetConversationSummary: Get context from previous parts of this conversation
        - SaveConversationSummary: Compact the conversation when it gets long
        - SearchByMeaning: Search the knowledge base for relevant architectural patterns

        Guidelines:
        - ALWAYS start by calling GetUserProfile to personalize your advice
        - Search the knowledge base for relevant patterns before giving generic advice
        - Tailor recommendations to the user's experience level and tech stack
        - When conversation is getting long (>3000 tokens), call SaveConversationSummary
        - Be direct and actionable — give specific recommendations, not vague suggestions
        """;

    public ChatCompletionAgent Create(
        IChatCompletionService chatService,
        ProfilePlugin profilePlugin,
        SummaryPlugin summaryPlugin,
        SearchPlugin searchPlugin)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        builder.Plugins.AddFromObject(profilePlugin, "Profile");
        builder.Plugins.AddFromObject(summaryPlugin, "Summary");
        builder.Plugins.AddFromObject(searchPlugin,  "Search");

        var kernel = builder.Build();

        return new ChatCompletionAgent
        {
            Name         = "ConsultantAgent",
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
