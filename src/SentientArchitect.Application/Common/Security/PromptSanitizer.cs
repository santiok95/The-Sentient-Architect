using System.Text.RegularExpressions;

namespace SentientArchitect.Application.Common.Security;

/// <summary>
/// Strips known prompt injection patterns from user-supplied content before it is
/// injected into an LLM context. This is a defence-in-depth measure at the ingestion
/// boundary; it does not replace safe prompt construction practices.
/// </summary>
public static partial class PromptSanitizer
{
    // Common injection openers — case-insensitive, matches leading whitespace variants
    [GeneratedRegex(
        @"(?i)(ignore\s+(all\s+)?(previous|prior|above)\s+instructions?|" +
        @"forget\s+(everything|all|previous|prior|above)|" +
        @"disregard\s+(all\s+)?(previous|prior|above)\s+instructions?|" +
        @"you\s+are\s+now\s+a\s+|" +
        @"act\s+as\s+(a\s+|an\s+)?|" +
        @"new\s+instructions?:|" +
        @"system\s*prompt:|" +
        @"<\s*system\s*>|" +
        @"\[\s*system\s*\]|" +
        @"###\s*(instruction|system|prompt))",
        RegexOptions.Compiled)]
    private static partial Regex InjectionPattern();

    /// <summary>
    /// Removes prompt injection patterns from the input string.
    /// Returns the sanitized string (may be empty if the entire content was an injection).
    /// </summary>
    public static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return InjectionPattern().Replace(input, "[REMOVED]");
    }

    /// <summary>
    /// Returns true if the input contains at least one prompt injection pattern.
    /// Useful for logging/alerting without modifying the content.
    /// </summary>
    public static bool ContainsInjection(string input)
        => !string.IsNullOrWhiteSpace(input) && InjectionPattern().IsMatch(input);
}
