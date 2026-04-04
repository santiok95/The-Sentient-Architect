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
    RepositoryContextPlugin repositoryContextPlugin,
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
        Your role is to provide expert software architecture advice tailored to the developer's
        existing codebase and professional context.
        You have access to:
        - Profile-GetUserProfile: Get the developer's tech stack and preferences
        - Summary-GetConversationSummary: Get context from previous conversations
        - Search-SearchByMeaning: Search the knowledge base for relevant rules
        - RepositoryContext-GetUserRepositoriesContext: Get the architectural patterns detected
          in the user's actual analyzed repositories

        MANDATORY RULES — never violate these:
        1. ALWAYS call Profile-GetUserProfile before giving any recommendation.
        2. ALWAYS check the injected codebase context for established patterns before advising.
        3. Recommendations MUST be consistent with the patterns already in use in the user's
           codebase (e.g. if the codebase injects DbContext directly, do NOT recommend adding
           a repository abstraction layer).
        4. NEVER silently recommend a pattern that contradicts an established convention.
           If you mention a conflicting generic alternative, label it explicitly as
           'Alternativa generica (no aplica a este proyecto)' and explain why it does not apply.
        5. Prioritize project-specific knowledge base rules over your general training knowledge.
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
                return await RunDeterministicKnowledgeFlowAsync(
                    request, chatService, chatHistory, onToken, ct);
            }

            // Keep consultant stable while Anthropic tool-call protocol through the bridge
            // is still intermittently failing with tool_use/tool_result mismatches.
            return await RunDeterministicConsultantFlowAsync(
                request, chatService, chatHistory, onToken, ct);
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
            request.Message, maxResults: 8, cancellationToken: ct);

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
        var noToolSettings  = new PromptExecutionSettings();

        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(responseHistory, noToolSettings, responseKernel, ct))
        {
            if (string.IsNullOrEmpty(chunk.Content)) continue;
            responseBuilder.Append(chunk.Content);
            if (onToken is not null) await onToken(chunk.Content, ct);
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

        // Gather all context sources in parallel for lower latency
        var profileTask    = profilePlugin.GetUserProfileAsync(userId.ToString(), ct);
        var summaryTask    = summaryPlugin.GetConversationSummaryAsync(request.ConversationId.ToString(), ct);
        var searchTask     = searchPlugin.SearchByMeaningAsync(request.Message, maxResults: 8, cancellationToken: ct);
        var repoCtxTask    = repositoryContextPlugin.GetUserRepositoriesContextAsync(userId.ToString(), ct);

        await Task.WhenAll(profileTask, summaryTask, searchTask, repoCtxTask);

        var userProfile         = await profileTask;
        var conversationSummary = await summaryTask;
        var retrievedContext    = await searchTask;
        var repositoryContext   = await repoCtxTask;

        var responseKernelBuilder = Kernel.CreateBuilder();
        responseKernelBuilder.Services.AddSingleton(chatService);
        var responseKernel = responseKernelBuilder.Build();

        var responseHistory = new ChatHistory();
        foreach (var item in history)
            responseHistory.Add(item);

        var contextMessage = new ChatMessageContent(
            AuthorRole.System,
            "Consultant context (source of truth — you MUST follow every constraint below):\n\n" +
            $"## User profile\n{userProfile}\n\n" +
            $"## Conversation summary\n{conversationSummary}\n\n" +
            $"## Knowledge base rules\n{retrievedContext}\n\n" +
            $"## Existing codebase patterns (HIGHEST priority — never contradict these)\n{repositoryContext}\n\n" +
            "## Response policy\n" +
            "1. Tailor every recommendation to the user profile and team context.\n" +
            "2. CRITICAL: The 'Existing codebase patterns' section above is ground truth. " +
            "Any recommendation you make MUST be consistent with those patterns.\n" +
            "3. If the codebase already uses a specific pattern " +
            "(e.g. direct DbContext injection, no repository layer, Minimal API, Clean Architecture), " +
            "REINFORCE that pattern — do not contradict it without explicit justification.\n" +
            "4. If you mention a generic alternative that conflicts with a detected pattern, " +
            "label it 'Alternativa generica (no aplica a este proyecto)' and explain the trade-off.\n" +
            "5. Prioritize knowledge base rules over your general training knowledge.\n" +
            "6. Keep practical next steps explicit and actionable.");

        if (responseHistory.Count > 0 && responseHistory[0].Role == AuthorRole.System)
            responseHistory.Insert(1, contextMessage);
        else
            responseHistory.Insert(0, contextMessage);

        var responseBuilder = new System.Text.StringBuilder();
        var noToolSettings  = new PromptExecutionSettings();

        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(responseHistory, noToolSettings, responseKernel, ct))
        {
            if (string.IsNullOrEmpty(chunk.Content)) continue;
            responseBuilder.Append(chunk.Content);
            if (onToken is not null) await onToken(chunk.Content, ct);
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
                MessageRole.User      => AuthorRole.User,
                MessageRole.Assistant => AuthorRole.Assistant,
                MessageRole.System    => AuthorRole.System,
                _                     => AuthorRole.User,
            };

            history.Add(new ChatMessageContent(role, msg.Content));
        }

        if (history.Count == 0 || history[0].Role != AuthorRole.System)
            history.Insert(0, new ChatMessageContent(AuthorRole.System, systemPrompt));

        return history;
    }
}
