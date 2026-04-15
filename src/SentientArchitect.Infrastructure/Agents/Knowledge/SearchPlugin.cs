using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Security;
using SentientArchitect.Domain.Constants;

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
                var safeTitle = PromptSanitizer.Sanitize(r.Title);
                var safeChunk = PromptSanitizer.Sanitize(r.ChunkText);

                if (PromptSanitizer.ContainsInjection(r.Title) || PromptSanitizer.ContainsInjection(r.ChunkText))
                    logger.LogWarning("PROMPT INJECTION DETECTED | KnowledgeItem title='{Title}' may contain injection patterns.", r.Title);

                return $"{i + 1}. [Project Rule: {safeTitle}] [Relevance: {r.Score:F2}]\nContext: {safeChunk}";
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
                .Where(k => k.UserId == userId || k.TenantId == tenantId || k.TenantId == TenantIds.Shared)
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
                    var safeTitle   = PromptSanitizer.Sanitize(m.Title);
                    var safeContent = PromptSanitizer.Sanitize(m.OriginalContent);

                    if (PromptSanitizer.ContainsInjection(m.Title) || PromptSanitizer.ContainsInjection(m.OriginalContent))
                        logger.LogWarning("PROMPT INJECTION DETECTED | KnowledgeItem title='{Title}' may contain injection patterns.", m.Title);

                    searchLines.Add($"{searchLines.Count + 1}. [Project Rule: {safeTitle}] (Detected via keywords: {string.Join(", ", keywords)})\nContent: {safeContent}");
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
