#pragma warning disable SKEXP0001
using Microsoft.SemanticKernel.Embeddings;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.Infrastructure.AI;

internal sealed class OpenAIEmbeddingService(ITextEmbeddingGenerationService embeddingGenerator)
    : IEmbeddingService
{
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var result = await embeddingGenerator.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return result.ToArray();
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts, CancellationToken ct = default)
    {
        var results = await embeddingGenerator.GenerateEmbeddingsAsync(texts.ToList(), cancellationToken: ct);
        return results.Select(r => r.ToArray()).ToList();
    }
}
#pragma warning restore SKEXP0001
