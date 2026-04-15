using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.Infrastructure.AI;

/// <summary>
/// Placeholder implementation of IEmbeddingService.
/// Replaced by a real provider (OpenAI, Anthropic, etc.) once API keys are configured.
/// </summary>
internal sealed class NullEmbeddingService : IEmbeddingService
{
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        => throw new InvalidOperationException(
            "No AI provider is configured. Set up an embedding service (OpenAI, Anthropic) in appsettings.json.");

    public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
        => throw new InvalidOperationException(
            "No AI provider is configured. Set up an embedding service (OpenAI, Anthropic) in appsettings.json.");
}
