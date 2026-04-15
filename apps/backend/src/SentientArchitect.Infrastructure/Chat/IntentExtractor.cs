using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.Infrastructure.Chat;

/// <summary>
/// Uses the LLM to extract structured intent from a user message.
/// Replaces all hardcoded stack/keyword detection.
/// </summary>
public sealed class IntentExtractor(IServiceProvider services) : IIntentExtractor
{
    private const string SystemPrompt = """
        You are an intent classifier for a software architecture assistant.
        Analyze the user message and respond ONLY with a JSON object — no explanation, no markdown.

        JSON schema:
        {
          "stack": "<the technology stack or language mentioned, normalized to its common name, e.g. 'Go', '.NET', 'Node.js', 'Java/Spring', 'Python/FastAPI'> or null if not mentioned",
          "scope": "<one of: 'NewApp' if they are building something new from scratch, 'ExistingRepo' if they refer to an existing codebase or repo, 'Generic' if the question is theoretical/educational>",
          "needsScope": <true if scope is genuinely ambiguous and you cannot determine it>,
          "needsStack": <true if no stack is mentioned or clearly implied and it matters for the answer>
        }

        Rules:
        - If the user says "el lenguaje de Google" → stack = "Go"
        - If the user says "el framework de Netflix" → stack = "Java/Spring"
        - Resolve any colloquial, indirect, or company-attributed reference to its actual technology name.
        - If the message says "nueva app", "desde cero", "todavía no tengo nada", "quiero crear" → scope = "NewApp"
        - If the message mentions "mi repo", "mi proyecto", "este código", "refactorizar" → scope = "ExistingRepo"
        - If the question is purely theoretical ("¿cuál es mejor?", "explicame") → scope = "Generic"
        - needsStack = false if stack is provided OR if the question is scope=Generic (stack doesn't matter)
        - needsScope = false if scope is clear from context
        """;

    public async Task<DetectedIntent> ExtractAsync(string message, CancellationToken ct = default)
    {
        try
        {
            var chatService = services.GetService(typeof(IChatCompletionService)) as IChatCompletionService;
            if (chatService is null)
                return Fallback();
            var history = new ChatHistory();
            history.AddSystemMessage(SystemPrompt);
            history.AddUserMessage(message);

            var settings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = 150,
                    ["temperature"] = 0,
                }
            };

            var response = await chatService.GetChatMessageContentAsync(history, settings, cancellationToken: ct);
            var json = response.Content?.Trim() ?? "{}";

            // Strip markdown code fences if the model wraps the JSON
            if (json.StartsWith("```"))
            {
                json = json.Split('\n').Skip(1).TakeWhile(l => !l.StartsWith("```"))
                           .Aggregate((a, b) => a + "\n" + b).Trim();
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var stack = root.TryGetProperty("stack", out var s) && s.ValueKind != JsonValueKind.Null
                ? s.GetString()
                : null;
            var scope = root.TryGetProperty("scope", out var sc) ? sc.GetString() ?? "Generic" : "Generic";
            var needsScope = root.TryGetProperty("needsScope", out var ns) && ns.GetBoolean();
            var needsStack = root.TryGetProperty("needsStack", out var nst) && nst.GetBoolean();

            return new DetectedIntent(stack, scope, needsScope, needsStack);
        }
        catch
        {
            return Fallback();
        }
    }

    // If the LLM call fails, assume we have enough context and let the main flow continue
    private static DetectedIntent Fallback() =>
        new(Stack: null, Scope: "Generic", NeedsScope: false, NeedsStack: false);
}
