using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
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
    IApplicationDbContext db,
    IUserAccessor userAccessor,
    IIntentExtractor intentExtractor) : IChatExecutionService
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
        - Trends-GetRelevantTrends: Get technology trends relevant to a given stack or keywords.
          Use this when the user asks about modernization, trends, or when you want to suggest
          concrete improvement opportunities backed by real ecosystem data.

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
          6. In RepoBound mode, do NOT recommend migration by default.
              Only suggest migration when there is a clear non-functional mismatch
              (e.g. extreme concurrency, latency/SLO constraints, throughput limits,
              operational limits, or ecosystem blockers) and provide explicit evidence.
          7. If stack and repository context conflict, ask the user to choose intent first:
              optimize current repo, hybrid coexistence, or full migration.
          8. Use a two-layer response: (a) short executive recommendation,
              (b) optional detailed technical plan.
          9. If the request provides an explicit preferred stack, that stack is binding.
              Keep the main recommendation and code examples in that stack unless the user
              explicitly asks for alternatives.
          10. When the user asks about modernization opportunities, or when you are analyzing
              an internal/trusted repository and want to suggest improvements, call
              Trends-GetRelevantTrends with stack keywords extracted from the profile and
              repository context. Use the results to produce a concrete
              'Oportunidades de modernización' section with bullets in this format:
              [Trend: {name} {direction}] — {why it applies to THIS project} → {first concrete step}.
              In RepoBound mode, only suggest trends that are COMPATIBLE with the detected patterns.
              NEVER suggest a trend that contradicts an established architecture convention.
        """;

    public async Task<Result<ChatExecutionResponse>> ExecuteAsync(
        ChatExecutionRequest request,
        IReadOnlyList<ConversationMessage> history,
        Func<string, CancellationToken, Task>? onToken = null,
        CancellationToken ct = default)
    {
        try
        {
            var isConsultant = request.AgentType == Domain.Enums.AgentType.Consultant;

            var kernel = isConsultant
                ? consultantFactory.CreateKernel(services)
                : knowledgeFactory.CreateKernel(services);

            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = BuildChatHistory(history, isConsultant ? ConsultantSystemPrompt : KnowledgeSystemPrompt);

            if (request.ShouldCompact)
                await CompactConversationAsync(request.ConversationId, chatService, chatHistory, ct);

            var result = !isConsultant
                ? await RunDeterministicKnowledgeFlowAsync(request, chatService, chatHistory, onToken, ct)
                : await RunDeterministicConsultantFlowAsync(request, chatService, chatHistory, onToken, ct);

            if (result.Succeeded && result.Data is not null)
                await TrackTokenUsageAsync(request.Message, result.Data.AssistantMessage, ct);

            return result;
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
            "2. Quote at least one exact fragment from the retrieved knowledge. Quote it in Spanish — translate it if the original is in English, keeping technical identifiers unchanged (Result, Success, Failure, DELETE, HTTP 204, ToHttpResult). ALWAYS end the quote block with a line that reads exactly: '> Fuente: <document title>'.\n" +
            "3. Never mix normative project rule and generic advice in the same sentence. Label generic advice explicitly.\n" +
            "4. If retrieved rule states static property style, write it exactly as property access (Result.Success), not method style (Result.Success()).\n" +
            "5. Keep examples aligned with project conventions (e.g., avoid repository pattern if project rule says no repository pattern).\n" +
            "6. In folder structure examples, do NOT use 'SentientArchitect' as the root folder name. Infer a short descriptive name from the user's request (e.g. 'PdfGenerator', 'AuthService', 'NotificationWorker'). If unsure, use 'MyApp'.\n" +
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
            new ChatExecutionResponse(FinalizeAssistantMessage(responseBuilder.ToString()), Domain.Enums.AgentType.Knowledge));
    }

    private async Task<Result<ChatExecutionResponse>> RunDeterministicConsultantFlowAsync(
        ChatExecutionRequest request,
        IChatCompletionService chatService,
        ChatHistory history,
        Func<string, CancellationToken, Task>? onToken,
        CancellationToken ct)
    {
        var userId = userAccessor.GetCurrentUserId();

        // Load with tracking so we can persist detected intent
        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.UserId == userId, ct);

        var resolvedMode = request.ContextMode ?? conversation?.ContextMode ?? ConsultantContextMode.Auto;
        var resolvedStack = FirstNonEmpty(request.PreferredStack, conversation?.PreferredStack);
        var resolvedRepositoryId = request.ActiveRepositoryId ?? conversation?.ActiveRepositoryId;
        var explicitStackInRequest = !string.IsNullOrWhiteSpace(request.PreferredStack);

        // In Auto mode, use the LLM to extract intent from the message — no hardcoded keywords.
        // Persist detected stack/scope so follow-up messages don't need to ask again.
        if (resolvedMode == ConsultantContextMode.Auto && conversation is not null)
        {
            var hasEnoughContext = !string.IsNullOrWhiteSpace(resolvedStack) ||
                                   resolvedRepositoryId.HasValue ||
                                   !string.IsNullOrWhiteSpace(conversation.DetectedStack) ||
                                   !string.IsNullOrWhiteSpace(conversation.DetectedScope);

            // Only extract intent if we don't already have it persisted from a previous message
            if (!hasEnoughContext || string.IsNullOrWhiteSpace(conversation.DetectedScope))
            {
                var intent = await intentExtractor.ExtractAsync(request.Message, ct);

                if (!string.IsNullOrWhiteSpace(intent.Stack) || !string.IsNullOrWhiteSpace(intent.Scope))
                {
                    conversation.UpdateDetectedIntent(intent.Stack, intent.Scope);
                    await db.SaveChangesAsync(ct);
                }

                // Merge detected stack into resolved stack if not already set
                if (string.IsNullOrWhiteSpace(resolvedStack) && !string.IsNullOrWhiteSpace(intent.Stack))
                    resolvedStack = intent.Stack;

                // Ask clarification only if LLM says we genuinely need it
                if (intent.NeedsScope || (intent.NeedsStack && string.IsNullOrWhiteSpace(resolvedStack)))
                {
                    var clarification = BuildClarificationPrompt(intent.NeedsScope, intent.NeedsStack, resolvedRepositoryId);
                    return Result<ChatExecutionResponse>.SuccessWith(
                        new ChatExecutionResponse(clarification, Domain.Enums.AgentType.Consultant));
                }
            }
            else if (!string.IsNullOrWhiteSpace(conversation.DetectedStack) && string.IsNullOrWhiteSpace(resolvedStack))
            {
                // Reuse previously detected stack from an earlier message
                resolvedStack = conversation.DetectedStack;
            }
        }

        // Plugins share the same scoped DbContext; keep this flow sequential to avoid
        // "A second operation was started on this context instance" concurrency errors.
        var userProfile = await profilePlugin.GetUserProfileAsync(userId.ToString(), ct);
        var conversationSummary = await summaryPlugin.GetConversationSummaryAsync(
            request.ConversationId.ToString(),
            ct);
        var retrievedContext = await searchPlugin.SearchByMeaningAsync(
            request.Message,
            maxResults: 8,
            cancellationToken: ct);
        var repositoryContext = await repositoryContextPlugin.GetUserRepositoriesContextAsync(
            userId.ToString(),
            resolvedRepositoryId?.ToString(),
            ct);

        var repositoryPriorityHeader = resolvedMode == ConsultantContextMode.RepoBound
            ? "## Existing codebase patterns (HIGHEST priority — never contradict these)\n"
            : "## Existing codebase patterns (background context — do not override explicit preferred stack)\n";

        var repositoryPriorityPolicy = resolvedMode == ConsultantContextMode.RepoBound
            ? "2. CRITICAL: The 'Existing codebase patterns' section above is ground truth. Any recommendation you make MUST be consistent with those patterns.\n"
            : "2. In StackBound/Generic modes, treat 'Existing codebase patterns' as background context only. Do NOT let it override explicit preferred stack.\n";

        var responseKernelBuilder = Kernel.CreateBuilder();
        responseKernelBuilder.Services.AddSingleton(chatService);
        var responseKernel = responseKernelBuilder.Build();

        var responseHistory = new ChatHistory();
        foreach (var item in history)
            responseHistory.Add(item);

        // Prevent cross-stack contamination from earlier assistant turns when the user
        // explicitly pins a stack for this request.
        if (explicitStackInRequest &&
            (resolvedMode == ConsultantContextMode.Generic || resolvedMode == ConsultantContextMode.StackBound))
        {
            var prunedHistory = new ChatHistory();
            var baseSystem = responseHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
            var lastUser = responseHistory.LastOrDefault(m => m.Role == AuthorRole.User);

            if (baseSystem is not null)
                prunedHistory.Add(baseSystem);

            if (lastUser is not null)
                prunedHistory.Add(lastUser);

            responseHistory = prunedHistory;
        }

        var contextMessage = new ChatMessageContent(
            AuthorRole.System,
            "Consultant context (source of truth — you MUST follow every constraint below):\n\n" +
            $"## Effective stack lock\n{resolvedStack ?? "Not specified"}\n\n" +
            $"## User profile\n{userProfile}\n\n" +
            $"## Context mode\n{resolvedMode}\n\n" +
            $"## Preferred stack\n{resolvedStack ?? "Not specified"}\n\n" +
            $"## Active repository id\n{resolvedRepositoryId?.ToString() ?? "Not specified"}\n\n" +
            $"## Conversation summary\n{conversationSummary}\n\n" +
            $"## Knowledge base rules\n{retrievedContext}\n\n" +
            $"{repositoryPriorityHeader}{repositoryContext}\n\n" +
            "## Response policy\n" +
            "1. Tailor every recommendation to the user profile and team context.\n" +
            repositoryPriorityPolicy +
            "3. If the codebase already uses a specific pattern " +
            "(e.g. direct DbContext injection, no repository layer, Minimal API, Clean Architecture), " +
            "REINFORCE that pattern — do not contradict it without explicit justification.\n" +
            "4. If you mention a generic alternative that conflicts with a detected pattern, " +
            "label it 'Alternativa generica (no aplica a este proyecto)' and explain the trade-off.\n" +
            "5. Prioritize knowledge base rules over your general training knowledge.\n" +
            "6. If context mode is StackBound or Generic, avoid imposing repository-specific conventions from a different stack.\n" +
            "7. In RepoBound mode, do NOT push migration unless the user explicitly asks for it, " +
            "or unless there is a clear non-functional mismatch (concurrency/SLO/throughput/ops constraints) backed by evidence.\n" +
            "8. If migration is suggested due to constraints, first state why the current stack may not satisfy the target constraints, " +
            "then offer at least one in-stack mitigation path before migration.\n" +
            "9. If stack preference and repository conventions conflict, ask intent first: optimize current repo, hybrid coexistence, or full migration.\n" +
            "10. If 'Preferred stack' is provided, keep primary architecture and code examples in that stack. " +
            "Only mention other stacks as explicit alternatives with trade-offs.\n" +
            "11. If 'Preferred stack' is provided, do NOT infer or switch the main stack from user profile defaults or knowledge-base snippets.\n" +
            "12. If knowledge-base/project rules are from a different stack than 'Preferred stack', label them as context only and keep implementation in preferred stack.\n" +
            "13. If prior conversation messages suggest a different stack, treat them as outdated context and align to current 'Preferred stack'.\n" +
            "14. Unless the user explicitly asks for full implementation code, avoid very long code blocks and prioritize concise architecture guidance.\n" +
            "15. Never end with incomplete code blocks or dangling fragments. If a code fence is opened, close it.\n" +
            "16. If 'Preferred stack' contains Java/Spring, do not output C#/.NET code as the primary proposal.\n" +
            "17. Output budget: keep the full answer under 250 words by default. Use short bullets, avoid large diagrams.\n" +
            "18. Format response in two layers: short executive summary first (2-3 sentences max), then optional bullet-point detail.\n" +
            "19. Keep practical next steps explicit and actionable.\n" +
            "20. When the user asks for an opinion on a repo, DO NOT ask for more context — you already have the analysis findings. " +
            "Give a direct opinion based on what you see: executive summary of severity, top 3 issues to fix first, and one positive observation.\n" +
            "21. When context mode is RepoBound and the repository has an ABOUT.md intent, use it to frame recommendations in terms of the author's stated vision.\n" +
            "22. When producing modernization suggestions, format each trend bullet as: " +
            "'[Trend: {name} {↑/→}] — {why relevant to THIS project} → {first concrete adoption step}'. " +
            "Only include trends that are compatible with the detected architecture patterns.");

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
            new ChatExecutionResponse(FinalizeAssistantMessage(responseBuilder.ToString()), Domain.Enums.AgentType.Consultant));
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string BuildClarificationPrompt(bool needsScope, bool needsStack, Guid? activeRepositoryId)
    {
        // One question at a time — ask the most important missing piece first.
        if (needsScope && !activeRepositoryId.HasValue)
            return "¿Es para una app nueva desde cero o para un proyecto existente?\n\n" +
                   "Con eso puedo darte una recomendación concreta en lugar de una genérica.";

        if (needsScope && activeRepositoryId.HasValue)
            return "¿Querés trabajar sobre el repo que tenés activo o es un caso nuevo independiente?";

        if (needsStack)
            return "¿Con qué stack o lenguaje querés implementar esto?\n\n" +
                   "No hace falta que sea preciso — con \"algo moderno para backend\" o \"el lenguaje de Google\" ya me alcanza.";

        // Fallback — shouldn't reach here but just in case
        return "Para darte la mejor recomendación: ¿es para un proyecto existente o una app nueva?";
    }

    private static string FinalizeAssistantMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "\n";

        var output = message;
        var fenceCount = output.Split("```", StringSplitOptions.None).Length - 1;
        if (fenceCount % 2 != 0)
            output += "\n```";

        var trimmed = output.TrimEnd();
        var endsLikelyTruncated = trimmed.EndsWith(":", StringComparison.Ordinal) ||
                                  trimmed.EndsWith(",", StringComparison.Ordinal) ||
                                  trimmed.EndsWith("=>", StringComparison.Ordinal) ||
                                  (trimmed.Length > 0 && char.IsDigit(trimmed[^1]));

        if (endsLikelyTruncated)
            output += "\n\n[Nota] La respuesta se recorto por limite de salida. Pedi \"continuar\" para completar el detalle.\n";

        if (!output.EndsWith("\n", StringComparison.Ordinal))
            output += "\n";

        return output;
    }

    private async Task TrackTokenUsageAsync(string userMessage, string assistantMessage, CancellationToken ct)
    {
        try
        {
            var userId   = userAccessor.GetCurrentUserId();
            var tenantId = userAccessor.GetCurrentTenantId();
            if (userId == Guid.Empty) return;

            // Approximate token count: 1 token ≈ 4 characters (GPT-style estimate)
            var estimatedTokens = (long)Math.Ceiling((userMessage.Length + assistantMessage.Length) / 4.0);

            var today   = DateOnly.FromDateTime(DateTime.UtcNow);
            var tracker = await db.TokenUsageTrackers
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Date == today, ct);

            if (tracker is null)
            {
                tracker = new Domain.Entities.TokenUsageTracker(userId, tenantId, today);
                db.TokenUsageTrackers.Add(tracker);
            }

            tracker.ConsumeTokens(estimatedTokens);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Token tracking is best-effort — must not block the user's response.
        }
    }

    private async Task CompactConversationAsync(
        Guid conversationId,
        IChatCompletionService chatService,
        ChatHistory chatHistory,
        CancellationToken ct)
    {
        try
        {
            var compactionPrompt = new ChatHistory();
            compactionPrompt.AddSystemMessage(
                "You are a conversation summarizer. Your only job is to produce a concise summary " +
                "of the conversation below, capturing: the main topic, key decisions made, important " +
                "facts established, and any open questions. Be brief — 3 to 6 sentences maximum.");

            foreach (var msg in chatHistory.Where(m => m.Role != AuthorRole.System))
                compactionPrompt.Add(msg);

            compactionPrompt.AddUserMessage("Summarize this conversation now.");

            var response = await chatService.GetChatMessageContentAsync(compactionPrompt, cancellationToken: ct);
            var summary  = response.Content ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(summary))
                await summaryPlugin.SaveConversationSummaryAsync(
                    conversationId.ToString(), summary, remainingTokens: 0, ct);
        }
        catch
        {
            // Compaction is best-effort — a failure must not block the user's message.
        }
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
