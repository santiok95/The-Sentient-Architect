using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
#pragma warning disable SKEXP0001
using Microsoft.SemanticKernel.ChatCompletion;
#pragma warning restore SKEXP0001
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Infrastructure.BackgroundJobs;

public sealed class TrendScanner(
    IApplicationDbContext db,
    IHttpClientFactory httpClientFactory,
    IChatCompletionService? chatService,
    ILogger<TrendScanner> logger) : ITrendScanner
{
    private const string GitHubTrendingUrl = "https://github.com/trending";
    private const string HnTopStoriesUrl   = "https://hacker-news.firebaseio.com/v0/topstories.json";
    private const string HnItemUrl         = "https://hacker-news.firebaseio.com/v0/item/{0}.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task ScanAsync(CancellationToken ct = default)
    {
        if (chatService is null)
        {
            logger.LogWarning("IChatCompletionService is not registered (no API key configured). Skipping trend scan.");
            return;
        }

        var signals = new List<string>();

        var githubRepos = await FetchGitHubTrendingAsync(ct);
        signals.AddRange(githubRepos);
        logger.LogInformation("Fetched {Count} GitHub trending repos.", githubRepos.Count);

        var hnTitles = await FetchHackerNewsTitlesAsync(ct);
        signals.AddRange(hnTitles);
        logger.LogInformation("Fetched {Count} Hacker News story titles.", hnTitles.Count);

        if (signals.Count == 0)
        {
            logger.LogWarning("No signals collected from sources. Skipping LLM analysis.");
            return;
        }

        var trendItems = await AnalyzeWithLlmAsync(signals, ct);
        if (trendItems.Count == 0)
        {
            logger.LogWarning("LLM returned no trend items. Nothing to upsert.");
            return;
        }

        await UpsertTrendsAsync(trendItems, ct);
    }

    // ── GitHub Trending ─────────────────────────────────────────────────────

    private async Task<List<string>> FetchGitHubTrendingAsync(CancellationToken ct)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; SentientArchitect/1.0)");

            var html = await client.GetStringAsync(GitHubTrendingUrl, ct);
            return ParseGitHubRepoNames(html);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch GitHub Trending. Skipping source.");
            return [];
        }
    }

    private static List<string> ParseGitHubRepoNames(string html)
    {
        var results = new List<string>();
        var searchTag = "<h2 class=\"h3 lh-condensed\">";
        var pos = 0;

        while (results.Count < 20)
        {
            var tagStart = html.IndexOf(searchTag, pos, StringComparison.Ordinal);
            if (tagStart < 0) break;

            // Find the first <a href="..."> inside the h2
            var hrefStart = html.IndexOf("<a ", tagStart, StringComparison.Ordinal);
            if (hrefStart < 0) break;

            var closeTag = html.IndexOf('>', hrefStart);
            if (closeTag < 0) break;

            var endTag = html.IndexOf("</a>", closeTag, StringComparison.Ordinal);
            if (endTag < 0) break;

            var repoName = html[(closeTag + 1)..endTag]
                .Replace("\n", "")
                .Replace(" ", "")
                .Trim();

            if (!string.IsNullOrWhiteSpace(repoName))
                results.Add($"GitHub Trending: {repoName}");

            pos = endTag;
        }

        return results;
    }

    // ── Hacker News ─────────────────────────────────────────────────────────

    private async Task<List<string>> FetchHackerNewsTitlesAsync(CancellationToken ct)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SentientArchitect/1.0");

            var idsJson = await client.GetStringAsync(HnTopStoriesUrl, ct);
            var ids = JsonSerializer.Deserialize<int[]>(idsJson);
            if (ids is null || ids.Length == 0) return [];

            var titles = new List<string>();
            var top10 = ids.Take(10);

            await Parallel.ForEachAsync(top10, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (id, innerCt) =>
            {
                try
                {
                    var itemJson = await client.GetStringAsync(string.Format(HnItemUrl, id), innerCt);
                    using var doc = JsonDocument.Parse(itemJson);
                    if (doc.RootElement.TryGetProperty("title", out var titleEl))
                    {
                        var title = titleEl.GetString();
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            lock (titles) titles.Add($"Hacker News: {title}");
                        }
                    }
                }
                catch
                {
                    // swallow per-item failures
                }
            });

            return titles;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch Hacker News stories. Skipping source.");
            return [];
        }
    }

    // ── LLM Analysis ────────────────────────────────────────────────────────

    private async Task<List<TrendItem>> AnalyzeWithLlmAsync(List<string> signals, CancellationToken ct)
    {
        var signalBlock = string.Join("\n", signals);
        var prompt =
            "You are a technology trend analyst. Analyze the following signals collected from GitHub Trending and Hacker News today.\n" +
            "Identify up to 10 distinct technology trends visible in this data.\n\n" +
            "For each trend, provide:\n" +
            "- name: the technology, tool, language, framework, or pattern name\n" +
            "- category: one of Framework, Language, Tool, Pattern, Platform, Library\n" +
            "- direction: one of Rising, Stable, Declining\n" +
            "- score: relevance score from 0.0 to 1.0 (higher = more prominent in the signals)\n" +
            "- description: 1-2 sentence description of why this is trending\n" +
            "- sources: list of source URLs that contributed to this signal (use \"https://github.com/trending\" or \"https://news.ycombinator.com\")\n\n" +
            "Return ONLY a raw JSON array with no markdown, no code blocks, no explanation. Example:\n" +
            "[\n" +
            "  {\n" +
            "    \"name\": \"Rust\",\n" +
            "    \"category\": \"Language\",\n" +
            "    \"direction\": \"Rising\",\n" +
            "    \"score\": 0.85,\n" +
            "    \"description\": \"Rust continues to gain adoption in systems programming and WebAssembly.\",\n" +
            "    \"sources\": [\"https://github.com/trending\"]\n" +
            "  }\n" +
            "]\n\n" +
            "Signals:\n" +
            signalBlock;

        try
        {
            var history = new ChatHistory();
            history.AddUserMessage(prompt);

            var response = await chatService!.GetChatMessageContentAsync(history, cancellationToken: ct);
            var json = response.Content ?? "[]";
            json = json.Trim();

            // Strip markdown code blocks if the LLM wrapped the response anyway
            if (json.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence    = json.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewline > 0 && lastFence > firstNewline)
                    json = json[(firstNewline + 1)..lastFence].Trim();
            }

            var items = JsonSerializer.Deserialize<List<TrendItem>>(json, JsonOptions);
            return items ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM analysis failed. Returning empty trend list.");
            return [];
        }
    }

    // ── Upsert ───────────────────────────────────────────────────────────────

    private async Task UpsertTrendsAsync(List<TrendItem> items, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) continue;

            var existing = await db.TechnologyTrends
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name.ToLower() == item.Name.ToLower(), ct);

            TechnologyTrend trend;

            if (existing is null)
            {
                trend = new TechnologyTrend(item.Name, item.Category);
                db.TechnologyTrends.Add(trend);
                logger.LogInformation("Creating new trend: {Name} ({Category}).", item.Name, item.Category);
            }
            else
            {
                // Re-attach so EF tracks changes
                trend = await db.TechnologyTrends.FindAsync([existing.Id], ct)
                    ?? existing;
            }

            trend.UpdateRelevance(item.Score, item.Direction, item.Description);

            foreach (var source in item.Sources)
                trend.AddSource(source);

            var snapshot = new TrendSnapshot(trend.Id, item.Score, item.Direction, today, item.Description);
            trend.RecordSnapshot(snapshot);
            db.TrendSnapshots.Add(snapshot);
        }

        var saved = await db.SaveChangesAsync(ct);
        logger.LogInformation("Upserted {Count} trends, saved {Changes} changes.", items.Count, saved);
    }

    // ── DTO ──────────────────────────────────────────────────────────────────

    private sealed class TrendItem
    {
        public string Name { get; init; } = string.Empty;
        public TrendCategory Category { get; init; }
        public TrendDirection Direction { get; init; }
        public float Score { get; init; }
        public string? Description { get; init; }
        public List<string> Sources { get; init; } = [];
    }
}
