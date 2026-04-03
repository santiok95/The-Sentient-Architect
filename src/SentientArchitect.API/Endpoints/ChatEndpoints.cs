using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.RegularExpressions;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Hubs;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Conversations.Chat;
using SentientArchitect.Domain.Enums;
using SentientArchitect.Infrastructure.Agents;
using SentientArchitect.Infrastructure.Agents.Consultant;
using SentientArchitect.Infrastructure.Agents.Knowledge;

namespace SentientArchitect.API.Endpoints;

public class ChatEndpoints : IEndpointModule
{
    private static readonly Regex InvokeNameRegex =
        new("<invoke\\s+name=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InvokeBlockRegex =
        new("<invoke\\s+name=\"([^\"]+)\"\\s*>(.*?)</invoke>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ParameterRegex =
        new("<parameter\\s+name=\"([^\"]+)\"\\s*>(.*?)</parameter>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> ToolToPlugin = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SearchByMeaning"] = "Search",
        ["IngestContent"] = "Ingest",
        ["GetUserProfile"] = "Profile",
        ["GetConversationSummary"] = "Summary",
        ["SaveConversationSummary"] = "Summary",
    };

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

    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations")
            .WithTags("Chat")
            .RequireAuthorization();

        group.MapPost("/{conversationId:guid}/chat", async (
            [FromRoute] Guid conversationId,
            [FromBody] ChatRequest body,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] SaveMessageUseCase saveMessageUseCase,
            [FromServices] IHubContext<ConversationHub> hubContext,
            [FromServices] KnowledgeAgentFactory knowledgeFactory,
            [FromServices] ConsultantAgentFactory consultantFactory,
            [FromServices] ILogger<ChatEndpoints> logger,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();

            var result = await saveMessageUseCase.ExecuteAsync(
                new SaveMessageRequest(conversationId, userId, body.Message, MessageRole.User), ct);

            if (!result.Succeeded)
                return Results.NotFound(result.Errors);

            var history = new ChatHistory();
            foreach (var msg in result.Data!)
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

            var services  = httpContext.RequestServices;
            var groupName = conversationId.ToString();
            var fullResponse = new System.Text.StringBuilder();

            try
            {
                var kernel = string.Equals(body.AgentType, "Consultant", StringComparison.OrdinalIgnoreCase)
                    ? consultantFactory.CreateKernel(services)
                    : knowledgeFactory.CreateKernel(services);

                var systemPrompt = string.Equals(body.AgentType, "Consultant", StringComparison.OrdinalIgnoreCase)
                    ? ConsultantSystemPrompt
                    : KnowledgeSystemPrompt;

                // Prepend system message if not already present
                if (history.Count == 0 || history[0].Role != AuthorRole.System)
                    history.Insert(0, new ChatMessageContent(AuthorRole.System, systemPrompt));

                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                // Anthropic tool_use/tool_result protocol has proven unstable through the bridge.
                // For Knowledge agent, run deterministic RAG (direct plugin call), then generate
                // a normal answer with no tool-calling enabled.
                if (!string.Equals(body.AgentType, "Consultant", StringComparison.OrdinalIgnoreCase))
                {
                    var searchPlugin = services.GetRequiredService<SearchPlugin>();
                    var retrievedContext = await searchPlugin.SearchByMeaningAsync(
                        body.Message,
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

                    var noToolSettings = new PromptExecutionSettings();
                    await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
                        responseHistory, noToolSettings, responseKernel, ct))
                    {
                        if (!string.IsNullOrEmpty(chunk.Content))
                        {
                            fullResponse.Append(chunk.Content);
                            await hubContext.Clients.Group(groupName)
                                .SendAsync("ReceiveToken", chunk.Content, cancellationToken: ct);
                        }
                    }
                }
                else
                {
                    var settings = new PromptExecutionSettings
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    };

                    // Agentic loop: keep invoking until no tool calls remain
                    while (true)
                    {
                        var textBuilder = new System.Text.StringBuilder();
                        var functionCallBuilder = new FunctionCallContentBuilder();

                        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
                            history, settings, kernel, ct))
                        {
                            if (!string.IsNullOrEmpty(chunk.Content))
                            {
                                textBuilder.Append(chunk.Content);
                                fullResponse.Append(chunk.Content);
                                await hubContext.Clients.Group(groupName)
                                    .SendAsync("ReceiveToken", chunk.Content, cancellationToken: ct);
                            }

                            functionCallBuilder.Append(chunk);
                        }

                        // Build FunctionCallContent objects from accumulated data
                        var invokeNamesFromText = ExtractInvokeNames(textBuilder.ToString());
                        var functionCalls = functionCallBuilder
                            .Build()
                            .Select((call, index) =>
                        {
                            var originalName = call.FunctionName;
                            var resolved = ResolveFunctionNameAndPlugin(
                                call.FunctionName,
                                call.PluginName,
                                index,
                                invokeNamesFromText);

                            if (string.IsNullOrWhiteSpace(resolved.Name) ||
                                resolved.Name.StartsWith("toolu_", StringComparison.OrdinalIgnoreCase))
                                return null;

                            // If name/plugin changed due to fallback resolution, rebuild the call while preserving args.
                            if (!string.Equals(originalName, resolved.Name, StringComparison.Ordinal) ||
                                !string.Equals(call.PluginName, resolved.Plugin, StringComparison.Ordinal))
                            {
                                return new FunctionCallContent(
                                    functionName: resolved.Name,
                                    pluginName: resolved.Plugin,
                                    id: call.Id,
                                    arguments: call.Arguments);
                            }

                            return call;
                        })
                        .Where(fc => fc is not null)
                        .Select(fc => fc!)
                        .ToList();

                        // Anthropic fallback: if no structured calls were recovered but the model emitted
                        // pseudo-XML invoke blocks, convert them to callable FunctionCallContent.
                        if (functionCalls.Count == 0)
                        {
                            var xmlFallbackCalls = BuildFunctionCallsFromInvokeBlocks(textBuilder.ToString());
                            functionCalls.AddRange(xmlFallbackCalls);
                        }

                        // Add assistant turn to history
                        var assistantMessage = new ChatMessageContent(AuthorRole.Assistant, textBuilder.ToString());
                        foreach (var fc in functionCalls)
                            assistantMessage.Items.Add(fc);
                        history.Add(assistantMessage);

                        if (functionCalls.Count == 0)
                            break;

                        logger.LogInformation(
                            "Chat loop tool calls | ConversationId={ConversationId} Count={Count} Calls={Calls}",
                            conversationId,
                            functionCalls.Count,
                            string.Join(", ", functionCalls.Select(c => $"{c.Id}:{c.PluginName ?? "<null>"}.{c.FunctionName}")));

                        // Anthropic requires tool_result blocks in the very next message
                        // after a tool_use. Aggregate all results into one message.
                        var toolResults = new ChatMessageContentItemCollection();
                        foreach (var functionCall in functionCalls)
                        {
                            try
                            {
                                var funcResult = await functionCall.InvokeAsync(kernel, ct);
                                toolResults.Add(new FunctionResultContent(functionCall, funcResult.Result));
                                logger.LogInformation(
                                    "Tool result emitted | ConversationId={ConversationId} ToolId={ToolId} Function={Plugin}.{Function}",
                                    conversationId,
                                    functionCall.Id,
                                    functionCall.PluginName ?? "<null>",
                                    functionCall.FunctionName);
                            }
                            catch (Exception ex)
                            {
                                // Return an explicit tool_result error so every tool_use id
                                // receives a matching tool_result block.
                                toolResults.Add(new FunctionResultContent(functionCall, $"Tool execution failed: {ex.Message}"));
                                logger.LogWarning(
                                    ex,
                                    "Tool execution failed | ConversationId={ConversationId} ToolId={ToolId} Function={Plugin}.{Function}",
                                    conversationId,
                                    functionCall.Id,
                                    functionCall.PluginName ?? "<null>",
                                    functionCall.FunctionName);
                            }
                        }

                        history.Add(new ChatMessageContent(AuthorRole.Tool, toolResults));
                    }
                }

                await saveMessageUseCase.ExecuteAsync(
                    new SaveMessageRequest(conversationId, userId, fullResponse.ToString(), MessageRole.Assistant), ct);

                await hubContext.Clients.Group(groupName).SendAsync("ReceiveComplete", ct);
            }
            catch (Exception ex)
            {
                await hubContext.Clients.Group(groupName).SendAsync("ReceiveError", ex.Message, ct);
                return Results.Problem(ex.Message);
            }

            return Results.Ok();
        })
        .WithName("Chat")
        .WithOpenApi();
    }

    private static List<string> ExtractInvokeNames(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        return InvokeNameRegex
            .Matches(content)
            .Select(m => m.Groups[1].Value)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
    }

    private static (string Name, string? Plugin) ResolveFunctionNameAndPlugin(
        string? rawName,
        string? rawPlugin,
        int callIndex,
        IReadOnlyList<string> invokeNamesFromText)
    {
        var plugin = string.IsNullOrWhiteSpace(rawPlugin) ? null : rawPlugin;
        var name = rawName ?? string.Empty;

        // Anthropic can emit tool call ids (toolu_*) in Name. In that case,
        // recover the real function name from assistant text invoke markers.
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith("toolu_", StringComparison.OrdinalIgnoreCase))
        {
            if (callIndex < invokeNamesFromText.Count)
                name = invokeNamesFromText[callIndex];
        }

        if (name.Contains('-', StringComparison.Ordinal))
        {
            var parts = name.Split('-', 2);
            if (parts.Length == 2)
            {
                plugin ??= parts[0];
                name = parts[1];
            }
        }

        if (plugin is null && ToolToPlugin.TryGetValue(name, out var mappedPlugin))
            plugin = mappedPlugin;

        return (name, plugin);
    }

    private static List<FunctionCallContent> BuildFunctionCallsFromInvokeBlocks(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        var calls = new List<FunctionCallContent>();
        foreach (Match invokeMatch in InvokeBlockRegex.Matches(content))
        {
            var functionName = invokeMatch.Groups[1].Value?.Trim();
            if (string.IsNullOrWhiteSpace(functionName))
                continue;

            if (functionName.StartsWith("toolu_", StringComparison.OrdinalIgnoreCase))
                continue;

            var args = new KernelArguments();
            var body = invokeMatch.Groups[2].Value;
            foreach (Match param in ParameterRegex.Matches(body))
            {
                var paramName = param.Groups[1].Value?.Trim();
                if (string.IsNullOrWhiteSpace(paramName))
                    continue;

                var paramValue = System.Net.WebUtility.HtmlDecode(param.Groups[2].Value).Trim();
                args[paramName] = paramValue;
            }

            ToolToPlugin.TryGetValue(functionName, out var pluginName);
            calls.Add(new FunctionCallContent(
                id: Guid.NewGuid().ToString("N"),
                pluginName: pluginName,
                functionName: functionName,
                arguments: args));
        }

        return calls;
    }

    private record ChatRequest(string Message, string AgentType = "Knowledge");
}
