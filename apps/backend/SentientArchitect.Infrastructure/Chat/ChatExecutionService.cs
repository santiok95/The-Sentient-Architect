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

namespace SentientArchitect.Infrastructure.Chat;

public sealed class ChatExecutionService(
    IServiceProvider services,
    KnowledgeAgentFactory knowledgeFactory,
    ConsultantAgentFactory consultantFactory,
    RadarAgentFactory radarFactory,
    AnthropicOrchestrator orchestrator,
    SummaryPlugin summaryPlugin,
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
        1. When a user asks a question, ALWAYS call Search-SearchByMeaning first.
        2. If you find relevant information, PRIORITIZE IT OVER YOUR GENERAL KNOWLEDGE.
        3. Cite the document titles you found.
        4. If nothing is found, say so clearly before offering general advice.
        5. Structure answers as: project rule → why it matters → practical example.
        6. Quote relevant fragments and end quotes with: '> Fuente: <document title>'.
        7. Never mix normative project rules with generic advice in the same sentence.
           Label generic advice explicitly.
        8. In folder structure examples, use descriptive names (e.g. 'PdfGenerator', 'AuthService'),
           not 'SentientArchitect'.
        """;

    private const string ConsultantSystemPrompt = """
        You are the Architecture Consultant for The Sentient Architect.
        Your role is to provide expert software architecture advice tailored to the developer's
        existing codebase and professional context.

        The user's profile and conversation summary are already provided in the context below.
        You also have these tools for gathering additional context ON DEMAND:
        - Search-SearchByMeaning: Search the knowledge base for project-specific rules and conventions.
          Call this when the user asks about patterns, best practices, or architecture decisions.
        - RepositoryContext-GetUserRepositoriesContext: Get the architectural patterns and findings
          detected in the user's actual analyzed repositories.
          Call this when you need to verify what patterns the codebase already uses, or when
          recommendations must align with existing conventions.
        - Trends-GetRelevantTrends: Get technology trends relevant to a given stack or keywords.
          Call this when the user asks about modernization, tool recommendations, or alternatives.

        MANDATORY RULES — never violate these:
        1. Before making architecture recommendations, call Search-SearchByMeaning to check for
           project-specific rules that might override general best practices.
        2. Before recommending patterns, call RepositoryContext-GetUserRepositoriesContext to verify
           what patterns the codebase already uses. NEVER contradict detected conventions.
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
        10. When the user asks about modernization, call Trends-GetRelevantTrends and format
            each result as: [Trend: {name} {direction}] — {why relevant} → {first concrete step}.
            In RepoBound mode, only include trends COMPATIBLE with detected architecture patterns.
        """;

     private const string RadarSystemPrompt = """
          You are the Radar Agent for The Sentient Architect.
          Your purpose is to provide trend-driven recommendations with transparent source attribution.
          You have access to:
          - Trends-GetRelevantTrends: primary source of ecosystem trend data.
          - Search-SearchByMeaning: secondary validation source against the user's documented project rules.

          MANDATORY RULES:
          1. ALWAYS call Trends-GetRelevantTrends first for trend questions.
          2. Use Search-SearchByMeaning only AFTER trends are retrieved, and only to validate conflicts with the user's documented rules.
          3. Every factual line must include explicit source attribution:
              - Use [Radar] for trend data from Trends-GetRelevantTrends.
              - Use [Brain] for knowledge-base validation from Search-SearchByMeaning.
          4. NEVER label Brain results as Radar results.
          5. If radar trends and brain rules conflict, include a section titled exactly '## Conflicto detectado' with:
              - trend recommendation,
              - conflicting brain rule,
              - final recommendation and rationale.
          6. If trends are missing or too weak, state this explicitly and do not compensate with generic Brain-only recommendations.
              Use: 'El radar no tiene data suficiente sobre este tema. Considera correr un scan manual.'
          7. Use this response structure:
              - ## Resumen
              - ## Trends detectados
              - ## Validacion contra tu proyecto (optional)
              - ## Conflicto detectado (optional)
              - ## Fuentes (only if Brain sources were used)
          """;

    public async Task<Result<ChatExecutionResponse>> ExecuteAsync(
        ChatExecutionRequest request,
        IReadOnlyList<ConversationMessage> history,
        Func<string, CancellationToken, Task>? onToken = null,
        CancellationToken ct = default)
    {
        try
        {
            var kernel = request.AgentType switch
            {
                Domain.Enums.AgentType.Consultant => consultantFactory.CreateKernel(services),
                Domain.Enums.AgentType.Radar      => radarFactory.CreateKernel(services),
                _                                 => knowledgeFactory.CreateKernel(services),
            };

            var selectedPrompt = request.AgentType switch
            {
                Domain.Enums.AgentType.Consultant => ConsultantSystemPrompt,
                Domain.Enums.AgentType.Radar      => RadarSystemPrompt,
                _                                 => KnowledgeSystemPrompt,
            };

            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = BuildChatHistory(history, selectedPrompt);

            if (request.ShouldCompact)
                await CompactConversationAsync(request.ConversationId, chatService, chatHistory, ct);

            var result = request.AgentType switch
            {
                Domain.Enums.AgentType.Consultant => await RunConsultantFlowAsync(request, chatService, chatHistory, kernel, onToken, ct),
                Domain.Enums.AgentType.Radar      => await RunRadarFlowAsync(request, chatService, chatHistory, kernel, onToken, ct),
                _                                 => await RunKnowledgeFlowAsync(request, chatService, chatHistory, kernel, onToken, ct),
            };

            if (result.Succeeded && result.Data is not null)
                await TrackTokenUsageAsync(request.Message, result.Data.AssistantMessage, ct);

            return result;
        }
        catch (Exception ex)
        {
            return Result<ChatExecutionResponse>.Failure([ex.Message], ErrorType.Failure);
        }
    }

    private async Task<Result<ChatExecutionResponse>> RunKnowledgeFlowAsync(
        ChatExecutionRequest request,
        IChatCompletionService chatService,
        ChatHistory history,
        Kernel kernel,
        Func<string, CancellationToken, Task>? onToken,
        CancellationToken ct)
    {
        var result = await orchestrator.RunAsync(chatService, kernel, history, onToken, ct);

        if (!result.Succeeded)
            return Result<ChatExecutionResponse>.Failure(result.Errors, result.ErrorType);

        return Result<ChatExecutionResponse>.SuccessWith(
            new ChatExecutionResponse(FinalizeAssistantMessage(result.Data!), Domain.Enums.AgentType.Knowledge));
    }

    private async Task<Result<ChatExecutionResponse>> RunConsultantFlowAsync(
        ChatExecutionRequest request,
        IChatCompletionService chatService,
        ChatHistory history,
        Kernel kernel,
        Func<string, CancellationToken, Task>? onToken,
        CancellationToken ct)
    {
        var userId = userAccessor.GetCurrentUserId();

        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.UserId == userId, ct);

        var resolvedMode = request.ContextMode ?? conversation?.ContextMode ?? ConsultantContextMode.Auto;
        var resolvedStack = FirstNonEmpty(request.PreferredStack, conversation?.PreferredStack);
        var resolvedRepositoryId = request.ActiveRepositoryId ?? conversation?.ActiveRepositoryId;
        var explicitStackInRequest = !string.IsNullOrWhiteSpace(request.PreferredStack);

        if (resolvedMode == ConsultantContextMode.Auto && conversation is not null)
        {
            var hasEnoughContext = !string.IsNullOrWhiteSpace(resolvedStack) ||
                                   resolvedRepositoryId.HasValue ||
                                   !string.IsNullOrWhiteSpace(conversation.DetectedStack) ||
                                   !string.IsNullOrWhiteSpace(conversation.DetectedScope);

            if (!hasEnoughContext || string.IsNullOrWhiteSpace(conversation.DetectedScope))
            {
                var intent = await intentExtractor.ExtractAsync(request.Message, ct);

                if (!string.IsNullOrWhiteSpace(intent.Stack) || !string.IsNullOrWhiteSpace(intent.Scope))
                {
                    conversation.UpdateDetectedIntent(intent.Stack, intent.Scope);
                    await db.SaveChangesAsync(ct);
                }

                if (string.IsNullOrWhiteSpace(resolvedStack) && !string.IsNullOrWhiteSpace(intent.Stack))
                    resolvedStack = intent.Stack;

                if (intent.NeedsScope || (intent.NeedsStack && string.IsNullOrWhiteSpace(resolvedStack)))
                {
                    var clarification = BuildClarificationPrompt(intent.NeedsScope, intent.NeedsStack, resolvedRepositoryId);
                    return Result<ChatExecutionResponse>.SuccessWith(
                        new ChatExecutionResponse(clarification, Domain.Enums.AgentType.Consultant));
                }
            }
            else if (!string.IsNullOrWhiteSpace(conversation.DetectedStack) && string.IsNullOrWhiteSpace(resolvedStack))
            {
                resolvedStack = conversation.DetectedStack;
            }
        }

        // Pre-fetch profile + summary in parallel using independent scopes
        using var profileScope = services.CreateScope();
        using var summaryScope = services.CreateScope();

        var scopedProfile = profileScope.ServiceProvider.GetRequiredService<ProfilePlugin>();
        var scopedSummary = summaryScope.ServiceProvider.GetRequiredService<SummaryPlugin>();

        var profileTask = scopedProfile.GetUserProfileAsync(ct);
        var summaryTask = scopedSummary.GetConversationSummaryAsync(
            request.ConversationId.ToString(), ct);

        await Task.WhenAll(profileTask, summaryTask);

        var userProfile = await profileTask;
        var conversationSummary = await summaryTask;

        var repositoryPriorityPolicy = resolvedMode == ConsultantContextMode.RepoBound
            ? "2. CRITICAL: When you retrieve repository context, treat detected patterns as ground truth. Any recommendation MUST be consistent with those patterns.\n"
            : "2. In StackBound/Generic modes, treat repository context as background only. Do NOT let it override explicit preferred stack.\n";

        var responseHistory = new ChatHistory();
        foreach (var item in history)
            responseHistory.Add(item);

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
            "Consultant context:\n\n" +
            $"## Effective stack lock\n{resolvedStack ?? "Not specified"}\n\n" +
            $"## User profile\n{userProfile}\n\n" +
            $"## Context mode\n{resolvedMode}\n\n" +
            $"## Preferred stack\n{resolvedStack ?? "Not specified"}\n\n" +
            $"## Active repository id\n{resolvedRepositoryId?.ToString() ?? "Not specified"}\n\n" +
            $"## Conversation summary\n{conversationSummary}\n\n" +
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
            "or unless there is a clear non-functional mismatch backed by evidence.\n" +
            "8. If migration is suggested, first state why the current stack may not satisfy constraints, " +
            "then offer at least one in-stack mitigation path before migration.\n" +
            "9. If stack preference and repository conventions conflict, ask intent first: optimize current repo, hybrid coexistence, or full migration.\n" +
            "10. If 'Preferred stack' is provided, keep primary architecture and code examples in that stack.\n" +
            "11. If 'Preferred stack' is provided, do NOT infer or switch the main stack from user profile defaults or knowledge-base snippets.\n" +
            "12. Unless the user explicitly asks for full implementation code, avoid very long code blocks.\n" +
            "13. Never end with incomplete code blocks or dangling fragments. If a code fence is opened, close it.\n" +
            "14. Output budget: keep the full answer under 250 words by default. Use short bullets.\n" +
            "15. Format response in two layers: short executive summary first, then optional bullet-point detail.\n" +
            "16. Keep practical next steps explicit and actionable.\n" +
            "17. When the user asks for an opinion on a repo, call RepositoryContext-GetUserRepositoriesContext and give a direct opinion: " +
            "executive summary of severity, top 3 issues to fix first, and one positive observation.\n" +
            "18. When context mode is RepoBound and the repository has an ABOUT.md intent, use it to frame recommendations.\n" +
            "19. When producing modernization suggestions, format each trend bullet as: " +
            "'[Trend: {name} {↑/→}] — {why relevant to THIS project} → {first concrete adoption step}'.\n" +
            "20. If any knowledge base rule has a [Source: <url>] field, include those URLs ONLY at the very end " +
            "under a '**Fuentes**' section as markdown links: '- [Title](url)'. Do NOT inline URLs in the body.");

        if (responseHistory.Count > 0 && responseHistory[0].Role == AuthorRole.System)
            responseHistory.Insert(1, contextMessage);
        else
            responseHistory.Insert(0, contextMessage);

        var result = await orchestrator.RunAsync(chatService, kernel, responseHistory, onToken, ct);

        if (!result.Succeeded)
            return Result<ChatExecutionResponse>.Failure(result.Errors, result.ErrorType);

        return Result<ChatExecutionResponse>.SuccessWith(
            new ChatExecutionResponse(FinalizeAssistantMessage(result.Data!), Domain.Enums.AgentType.Consultant));
    }

    private async Task<Result<ChatExecutionResponse>> RunRadarFlowAsync(
        ChatExecutionRequest request,
        IChatCompletionService chatService,
        ChatHistory history,
        Kernel kernel,
        Func<string, CancellationToken, Task>? onToken,
        CancellationToken ct)
    {
        var result = await orchestrator.RunAsync(chatService, kernel, history, onToken, ct);

        if (!result.Succeeded)
            return Result<ChatExecutionResponse>.Failure(result.Errors, result.ErrorType);

        return Result<ChatExecutionResponse>.SuccessWith(
            new ChatExecutionResponse(FinalizeAssistantMessage(result.Data!), Domain.Enums.AgentType.Radar));
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
