using System.ComponentModel;
using Microsoft.SemanticKernel;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.Infrastructure.Agents.Knowledge;

public sealed class SearchPlugin(
    IVectorStore vectorStore,
    IEmbeddingService embeddingService)
{
    [KernelFunction, Description("Search the knowledge base using natural language. Returns relevant articles, notes, and repo analyses matching the query.")]
    public async Task<string> SearchByMeaningAsync(
        [Description("The user's question or search query in natural language")] string query,
        [Description("User ID to scope the search")] string userId,
        [Description("Tenant ID to include shared content")] string tenantId,
        [Description("Maximum number of results to return")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var userGuid   = Guid.Parse(userId);
        var tenantGuid = Guid.Parse(tenantId);

        var embedding = await embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        var results = await vectorStore.SearchSimilarAsync(
            embedding, userGuid, tenantGuid, maxResults, 0.7f, true, cancellationToken);

        if (!results.Any())
            return "No relevant knowledge found for this query.";

        var lines = results.Select((r, i) =>
            $"{i + 1}. [{r.Score:F2}] {r.ChunkText}");

        return string.Join("\n\n", lines);
    }
}
