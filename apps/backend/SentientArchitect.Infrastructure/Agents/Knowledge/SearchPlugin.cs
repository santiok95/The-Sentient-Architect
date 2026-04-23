using System;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.Infrastructure.Agents.Knowledge;

public sealed class SearchPlugin(
    IApplicationDbContext db,
    IVectorStore vectorStore,
    IEmbeddingService embeddingService,
    IUserAccessor userAccessor,
    ILogger<SearchPlugin> logger)
{
    [KernelFunction, Description("Search the knowledge base using natural language. Returns relevant articles, notes, and repo analyses matching the query.")]
    public async Task<string> SearchByMeaningAsync(
        [Description("The user's question or search query in natural language")] string query,
        [Description("Maximum number of results to return")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var userId   = userAccessor.GetCurrentUserId();
        var tenantId = userAccessor.GetCurrentTenantId();

        // Search logic starts here
        var embedding = await embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        // 1. Try Vector Search
        var results = await vectorStore.SearchSimilarAsync(
            embedding, userId, tenantId, maxResults, 0.35f, true, false, cancellationToken);

        var searchLines = new List<string>();

        if (results.Any())
        {
            searchLines.AddRange(results.Select((r, i) =>
            {
                var urlPart = !string.IsNullOrWhiteSpace(r.SourceUrl) ? $" [Source: {r.SourceUrl}]" : "";
                return $"{i + 1}. [Project Rule: {r.Title}]{urlPart} [Relevance: {r.Score:F2}]\nContext: {r.ChunkText}";
            }));
        }

        // 2. Keyword Fallback (Atomic keywords) - ALWAYS do keywords too to be safe
        var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .Select(w => w.Trim().ToLowerInvariant())
            .ToList();

        if (keywords.Count > 0)
        {
            // Fetch items for current scope or shared/empty tenant
            var items = await db.KnowledgeItems
                .AsNoTracking()
                .Where(k => k.UserId == userId || k.TenantId == tenantId || k.TenantId == Guid.Empty)
                .ToListAsync(cancellationToken);

            var matches = items
                .Where(k => keywords.Any(kw => 
                    k.Title.Contains(kw, StringComparison.OrdinalIgnoreCase) || 
                    k.OriginalContent.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var m in matches)
            {
                if (!searchLines.Any(l => l.Contains(m.Title)))
                {
                    var preview = m.OriginalContent.Length > 800
                        ? string.Concat(m.OriginalContent.AsSpan(0, 800), "…")
                        : m.OriginalContent;
                    var urlPart = !string.IsNullOrWhiteSpace(m.SourceUrl) ? $" [Source: {m.SourceUrl}]" : "";
                    searchLines.Add($"{searchLines.Count + 1}. [Project Rule: {m.Title}]{urlPart} (Detected via keywords: {string.Join(", ", keywords)})\nContent: {preview}");
                }
            }
        }

        if (!searchLines.Any())
        {
            logger.LogWarning("DATABASE SEARCH | Result: NOTHING FOUND for query '{Query}'", query);
            return "No relevant project documentation found. Remind the user to document these rules.";
        }

        logger.LogInformation("DATABASE SEARCH | Result: FOUND {Count} items.", searchLines.Count);
        return "I found the following PROJECT RULES. YOU MUST FOLLOW THESE IN YOUR RESPONSE:\n\n" +
               string.Join("\n\n", searchLines);
    }
}
