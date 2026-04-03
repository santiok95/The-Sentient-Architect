using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Infrastructure.Chat;

public sealed class AnthropicOrchestrator
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

    public async Task<Result<string>> RunAsync(
        IChatCompletionService chatService,
        Kernel kernel,
        ChatHistory history,
        Func<string, CancellationToken, Task>? onToken,
        CancellationToken ct)
    {
        try
        {
            var fullResponse = new System.Text.StringBuilder();
            var settings = new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            };

            while (true)
            {
                var textBuilder = new System.Text.StringBuilder();
                var functionCallBuilder = new FunctionCallContentBuilder();

                await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(history, settings, kernel, ct))
                {
                    if (!string.IsNullOrEmpty(chunk.Content))
                    {
                        textBuilder.Append(chunk.Content);
                        fullResponse.Append(chunk.Content);

                        if (onToken is not null)
                            await onToken(chunk.Content, ct);
                    }

                    functionCallBuilder.Append(chunk);
                }

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

                if (functionCalls.Count == 0)
                    functionCalls.AddRange(BuildFunctionCallsFromInvokeBlocks(textBuilder.ToString()));

                var assistantMessage = new ChatMessageContent(AuthorRole.Assistant, textBuilder.ToString());
                foreach (var fc in functionCalls)
                    assistantMessage.Items.Add(fc);
                history.Add(assistantMessage);

                if (functionCalls.Count == 0)
                    break;

                var toolResults = new ChatMessageContentItemCollection();
                foreach (var functionCall in functionCalls)
                {
                    try
                    {
                        var funcResult = await functionCall.InvokeAsync(kernel, ct);
                        toolResults.Add(new FunctionResultContent(functionCall, funcResult.Result));
                    }
                    catch (Exception ex)
                    {
                        toolResults.Add(new FunctionResultContent(functionCall, $"Tool execution failed: {ex.Message}"));
                    }
                }

                history.Add(new ChatMessageContent(AuthorRole.Tool, toolResults));
            }

            return Result<string>.SuccessWith(fullResponse.ToString());
        }
        catch (Exception ex)
        {
            return Result<string>.Failure([ex.Message], ErrorType.Failure);
        }
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
}
