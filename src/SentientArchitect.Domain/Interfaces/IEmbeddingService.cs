namespace SentientArchitect.Domain.Interfaces;

/// <summary>
/// Generates vector embeddings from text.
/// Implementation will use OpenAI text-embedding-3-small (1536 dimensions) via Semantic Kernel.
/// Defined here so that Application layer use cases can depend on it without 
/// referencing Infrastructure.
/// </summary>
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
