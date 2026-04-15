namespace SentientArchitect.Application.Features.Knowledge.IngestKnowledge;

/// <summary>
/// Strips known prompt injection patterns from user-supplied text before it is
/// persisted or injected into LLM prompts. Not a silver bullet — defense in depth.
/// </summary>
internal static class PromptInjectionSanitizer
{
    private static readonly string[] DangerousPatterns =
    [
        "ignore all previous instructions",
        "ignore previous instructions",
        "disregard previous",
        "forget previous",
        "new instructions:",
        "system prompt:",
        "[system]",
        "[inst]",
        "[/inst]",
        "<|system|>",
        "<|user|>",
        "<|assistant|>",
        "###instruction",
        "### instruction",
        "###system",
        "### system",
    ];

    /// <summary>
    /// Removes dangerous prompt injection patterns from the input string.
    /// Comparison is case-insensitive; original casing is preserved for safe content.
    /// </summary>
    public static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var result = input;
        foreach (var pattern in DangerousPatterns)
            result = Replace(result, pattern, "[removed]");

        return result;
    }

    private static string Replace(string source, string pattern, string replacement)
    {
        var index = source.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            source = string.Concat(source.AsSpan(0, index), replacement, source.AsSpan(index + pattern.Length));
            index = source.IndexOf(pattern, index + replacement.Length, StringComparison.OrdinalIgnoreCase);
        }
        return source;
    }
}
