using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Application.Features.Conversations.Chat;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.Infrastructure.Agents;
using SentientArchitect.Infrastructure.Agents.Consultant;
using SentientArchitect.Infrastructure.Agents.Knowledge;

namespace SentientArchitect.Infrastructure.Chat;

public sealed class ChatExecutionService(
    IServiceProvider services,
    KnowledgeAgentFactory knowledgeFactory,
    ConsultantAgentFactory consultantFactory,
    SearchPlugin searchPlugin,
    ProfilePlugin profilePlugin,
    SummaryPlugin summaryPlugin,
    IUserAccessor userAccessor) : IChatExecutionService
{
    private const string KnowledgeSystemPrompt = """
        You are the Knowledge Agent for The Sentient Architect.
        Your role is to help developers store and retrieve technical knowledge.
        You have access to:
        - Search-SearchByMeaning: Search the knowledge base with natural language queries
        - Ingest-IngestContent: Store new technical content in the knowledge base

        STRICT GUIDELINES:
        1. When a user asks a question, ALWAYS use Search-SearchByMeaning first.
        2. If you find relevant information, PRIORITIZE IT OVER YOUR GENERAL KNOWLEDGE.
        3. Cite the document titles you found.
        4. If nothing is found, say so clearly before offering general advice.
        """;

    private const string ConsultantSystemPrompt = """
        You are the Architecture Consultant for The Sentient Architect.
        Your role is to provide expert software architecture advice tailored to the developer's context.
        You have access to:
        - Profile-GetUserProfile: Get the developer's tech stack and preferences
        - Summary-GetConversationSummary: Get context from previous conversation
        - Search-SearchByMeaning: Search the knowledge base for relevant patterns

        ALWAYS start by calling Profile-GetUserProfile to personalize your advice.
        """;

    public async Task<Result<ChatExecutionResponse>> ExecuteAsync(
        ChatExecutionRequest request,
        IReadOnlyList<ConversationMessage> history,
        Func<string, CancellationToken, Task>? onToken = null,
        CancellationToken ct = default)
    {
        try
        {
            var isConsultant = string.Equals(request.AgentType, "Consultant", StringComparison.OrdinalIgnoreCase);

            var kernel = isConsultant
                ? consultantFactory.CreateKernel(services)
                : knowledgeFactory.CreateKernel(services);

            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = BuildChatHistory(history, isConsultant ? ConsultantSystemPrompt : KnowledgeSystemPrompt);

            if (!isConsultant)
            {
                var knowledgeResponse = await RunDeterministicKnowledgeFlowAsync(
                    request,
                    chatService,
                    chatHistory,
                    onToken,
                    ct);

                return knowledgeResponse;
            }

            // Keep consultant stable while Anthropic tool-call protocol through the bridge
            // is still intermittently failing with tool_use/tool_result mismatches.
            return await RunDeterministicConsultantFlowAsync(
                request,
                chatService,
                chatHistory,
                onToken,
                ct);
        }
        catch (Exception ex)
        {
            return Result<ChatExecutionResponse>.Failure([ex.Message], ErrorType.Failure);
        }
    }

    private async Task<Result<ChatExecutionResponse>> RunDeterministicKnowledgeFlowAsync(
        ChatExecutionRequest request,
        IChatCompletionService chatService,
        ChatHistory history,
        Func<string, CancellationToken, Task>? onToken,
        CancellationToken ct)
    {
        var retrievedContext = await searchPlugin.SearchByMeaningAsync(
            request.Message,
            maxResults: 8,
            cancellationToken: ct);

        var responseKernelBuilder = Kernel.CreateBuilder();
        responseKernelBuilder.Services.AddSingleton(chatService);
        var responseKernel = responseKernelBuilder.Build();

        var responseHistory = new ChatHistory();
        foreach (var item in history)
            responseHistory.Add(item);

        var contextMessage = new ChatMessageContent(
            AuthorRole.System,
            $"Retrieved project knowledge (source of truth):\n{retrievedContext}\n\n" +
            "Output policy (high fidelity + clear teaching):\n" +
            "1. Use this structure: 'Regla actual del proyecto' -> 'Por que importa' -> 'Ejemplo aplicado al proyecto' -> optional 'Alternativa generica (no normativa del proyecto)'.\n" +
            "2. Quote at least one exact fragment and include the source title.\n" +
            "3. If a quoted fragment is in English, immediately add a short Spanish explanation below it. Keep technical identifiers unchanged (Result, Success, Failure, DELETE, HTTP 204, ToHttpResult).\n" +
            "4. Never mix normative project rule and generic advice in the same sentence. Label generic advice explicitly.\n" +
            "5. If retrieved rule states static property style, write it exactly as property access (Result.Success), not method style (Result.Success()).\n" +
            "6. Keep examples aligned with project conventions (e.g., avoid repository pattern if project rule says no repository pattern).\n" +
            "7. Keep response concise, explicit, and auditable.");

        if (responseHistory.Count > 0 && responseHistory[0].Role == AuthorRole.System)
            responseHistory.Insert(1, contextMessage);
        else
            responseHistory.Insert(0, contextMessage);

        var responseBuilder = new System.Text.StringBuilder();
        var noToolSettings = new PromptExecutionSettings();

        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(responseHistory, noToolSettings, responseKernel, ct))
        {
            if (string.IsNullOrEmpty(chunk.Content))
                continue;

            responseBuilder.Append(chunk.Content);

            if (onToken is not null)
                await onToken(chunk.Content, ct);
        }

        return Result<ChatExecutionResponse>.SuccessWith(
            new ChatExecutionResponse(responseBuilder.ToString(), "Knowledge"));
    }

    private async Task<Result<ChatExecutionResponse>> RunDeterministicConsultantFlowAsync(
        ChatExecutionRequest request,
        IChatCompletionService chatService,
        ChatHistory history,
        Func<string, CancellationToken, Task>? onToken,
        CancellationToken ct)
    {
        var userId = userAccessor.GetCurrentUserId();
        var userProfile = await profilePlugin.GetUserProfileAsync(userId.ToString(), ct);
        var conversationSummary = await summaryPlugin.GetConversationSummaryAsync(
            request.ConversationId.ToString(),
            ct);
        var retrievedContext = await searchPlugin.SearchByMeaningAsync(
            request.Message,
            maxResults: 8,
            cancellationToken: ct);

        var responseKernelBuilder = Kernel.CreateBuilder();
        responseKernelBuilder.Services.AddSingleton(chatService);
        var responseKernel = responseKernelBuilder.Build();

        var responseHistory = new ChatHistory();
        foreach (var item in history)
            responseHistory.Add(item);

        var contextMessage = new ChatMessageContent(
            AuthorRole.System,
            "Consultant context (source of truth):\n" +
            $"- User profile:\n{userProfile}\n\n" +
            $"- Conversation summary:\n{conversationSummary}\n\n" +
            $"- Retrieved project knowledge:\n{retrievedContext}\n\n" +
            "Response policy:\n" +
            "1. Start with a tailored architecture recommendation based on user profile and team context.\n" +
            "2. Prioritize retrieved project rules over generic advice.\n" +
            "3. If a recommendation is generic and not a project rule, label it clearly as 'Alternativa generica'.\n" +
            "4. Keep practical next steps explicit and concise.");

        if (responseHistory.Count > 0 && responseHistory[0].Role == AuthorRole.System)
            responseHistory.Insert(1, contextMessage);
        else
            responseHistory.Insert(0, contextMessage);

        var responseBuilder = new System.Text.StringBuilder();
        var noToolSettings = new PromptExecutionSettings();

        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(responseHistory, noToolSettings, responseKernel, ct))
        {
            if (string.IsNullOrEmpty(chunk.Content))
                continue;

            responseBuilder.Append(chunk.Content);

            if (onToken is not null)
                await onToken(chunk.Content, ct);
        }

        return Result<ChatExecutionResponse>.SuccessWith(
            new ChatExecutionResponse(responseBuilder.ToString(), "Consultant"));
    }

    private static ChatHistory BuildChatHistory(IReadOnlyList<ConversationMessage> messages, string systemPrompt)
    {
        var history = new ChatHistory();
        foreach (var msg in messages)
        {
            var role = msg.Role switch
            {
                MessageRole.User => AuthorRole.User,
                MessageRole.Assistant => AuthorRole.Assistant,
                MessageRole.System => AuthorRole.System,
                _ => AuthorRole.User,
            };

            history.Add(new ChatMessageContent(role, msg.Content));
        }

        if (history.Count == 0 || history[0].Role != AuthorRole.System)
            history.Insert(0, new ChatMessageContent(AuthorRole.System, systemPrompt));

        return history;
    }
}
