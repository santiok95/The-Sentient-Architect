namespace SentientArchitect.Application.Common.Interfaces;

public record DetectedIntent(
    string? Stack,       // e.g. "Go", ".NET", "Node.js" — null if not mentioned
    string Scope,        // "NewApp" | "ExistingRepo" | "Generic"
    bool NeedsScope,     // true if scope is still unclear
    bool NeedsStack);    // true if stack is still unclear

public interface IIntentExtractor
{
    Task<DetectedIntent> ExtractAsync(string message, CancellationToken ct = default);
}
