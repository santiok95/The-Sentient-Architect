using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
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
    // ── Source URLs ──────────────────────────────────────────────────────────

    private const string HnTopStoriesUrl   = "https://hacker-news.firebaseio.com/v0/topstories.json";
    private const string HnItemUrl         = "https://hacker-news.firebaseio.com/v0/item/{0}.json";

    // GitHub Search API — no auth required, 60 req/h unauthenticated
    private const string GitHubSearchUrl =
        "https://api.github.com/search/repositories?q={0}&sort=stars&order=desc&per_page=10";

    // Architecture-focused search queries for GitHub Search API
    private static readonly (string Query, string Category)[] GitHubSearchQueries =
    [
        ("topic:software-architecture stars:>1000",     "Architecture"),
        ("topic:design-patterns stars:>500",            "BestPractice"),
        ("topic:clean-architecture stars:>500",         "BestPractice"),
        ("topic:microservices stars:>1000",             "Architecture"),
        ("topic:devops stars:>1000",                    "DevOps"),
        ("topic:testing stars:>1000",                   "Testing"),
        ("topic:machine-learning stars:>2000",          "Innovation"),
        ("topic:dotnet stars:>500",                     "Framework"),
        ("topic:observability stars:>500",              "DevOps"),
        ("topic:event-driven stars:>300",               "Pattern"),
    ];

    // Dev.to public API — no key required
    private const string DevToApiUrl = "https://dev.to/api/articles?per_page=20&tag={0}";

    // Medium RSS feeds by tag
    private static readonly string[] MediumTags =
    [
        "software-architecture",
        "dotnet",
        "artificial-intelligence",
        "devops",
        "microservices"
    ];

    // GitHub Releases for key repos (owner/repo)
    private static readonly string[] WatchedRepos =
    [
        "dotnet/aspnetcore",
        "dotnet/runtime",
        "microsoft/semantic-kernel",
        "dotnet/efcore",
        "dotnet/aspire",
        "microsoft/garnet",
        "openai/openai-dotnet"
    ];

    private static readonly string[] DevToTags =
    [
        "dotnet",
        "csharp",
        "architecture",
        "ai",
        "devops"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // ── Entry point ──────────────────────────────────────────────────────────

    public async Task ScanAsync(CancellationToken ct = default)
    {
        if (chatService is null)
        {
            logger.LogWarning("IChatCompletionService not registered. Skipping trend scan.");
            return;
        }

        var signals = new List<FeedSignal>();

        // Collect from all sources in parallel
        var tasks = new[]
        {
            FetchGitHubStarredReposAsync(ct),
            FetchHackerNewsAsync(ct),
            FetchDevToAsync(ct),
            FetchMediumRssAsync(ct),
            FetchGitHubReleasesAsync(ct),
        };

        var results = await Task.WhenAll(tasks);
        foreach (var batch in results)
            signals.AddRange(batch);

        logger.LogInformation("Collected {Count} signals from all sources.", signals.Count);

        if (signals.Count == 0)
        {
            logger.LogWarning("No signals collected. Skipping LLM analysis.");
            return;
        }

        // Load user profiles for relevance context
        var profileContext = await BuildProfileContextAsync(ct);

        var trendItems = await AnalyzeWithLlmAsync(signals, profileContext, ct);

        if (trendItems.Count == 0)
        {
            logger.LogWarning("LLM returned no trend items.");
            return;
        }

        await UpsertTrendsAsync(trendItems, ct);
    }

    // ── GitHub Search API (replaces fragile HTML scraping) ───────────────────

    private async Task<List<FeedSignal>> FetchGitHubStarredReposAsync(CancellationToken ct)
    {
        var signals = new List<FeedSignal>();

        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SentientArchitect/1.0");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        foreach (var (query, category) in GitHubSearchQueries)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var url  = string.Format(GitHubSearchUrl, Uri.EscapeDataString(query));
                var json = await client.GetStringAsync(url, ct);

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("items", out var items)) continue;

                foreach (var repo in items.EnumerateArray())
                {
                    var name        = repo.TryGetProperty("full_name", out var fn) ? fn.GetString() : null;
                    var description = repo.TryGetProperty("description", out var d)  ? d.GetString() : null;
                    var htmlUrl     = repo.TryGetProperty("html_url", out var hu)    ? hu.GetString() : null;
                    var stars       = repo.TryGetProperty("stargazers_count", out var s) ? s.GetInt32() : 0;
                    var language    = repo.TryGetProperty("language", out var l)     ? l.GetString() : null;

                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var text = $"[GitHub:{category}] {name} ★{stars}";
                    if (!string.IsNullOrWhiteSpace(description))
                        text += $" — {description[..Math.Min(150, description.Length)]}";
                    if (!string.IsNullOrWhiteSpace(language))
                        text += $" [{language}]";

                    signals.Add(new FeedSignal(text, htmlUrl ?? $"https://github.com/{name}", SignalSource.GitHubSearch, stars, category));
                }

                // Respect GitHub rate limit: 60 req/h unauthenticated → ~1s gap between queries
                await Task.Delay(1100, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "GitHub Search failed for query '{Query}'.", query);
            }
        }

        logger.LogInformation("GitHub Search: {Count} repos.", signals.Count);
        return signals;
    }

    // ── Hacker News ──────────────────────────────────────────────────────────

    private async Task<List<FeedSignal>> FetchHackerNewsAsync(CancellationToken ct)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SentientArchitect/1.0");

            var idsJson = await client.GetStringAsync(HnTopStoriesUrl, ct);
            var ids     = JsonSerializer.Deserialize<int[]>(idsJson);
            if (ids is null || ids.Length == 0) return [];

            var signals = new List<FeedSignal>();

            await Parallel.ForEachAsync(ids.Take(15), new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = ct }, async (id, innerCt) =>
            {
                try
                {
                    var itemJson = await client.GetStringAsync(string.Format(HnItemUrl, id), innerCt);
                    using var doc = JsonDocument.Parse(itemJson);
                    if (doc.RootElement.TryGetProperty("title", out var titleEl))
                    {
                        var title = titleEl.GetString();
                        var url   = doc.RootElement.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            lock (signals)
                                signals.Add(new FeedSignal(title, url ?? "https://news.ycombinator.com", SignalSource.HackerNews));
                        }
                    }
                }
                catch { /* swallow per-item errors */ }
            });

            logger.LogInformation("Hacker News: {Count} stories.", signals.Count);
            return signals;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Hacker News fetch failed.");
            return [];
        }
    }

    // ── Dev.to API ───────────────────────────────────────────────────────────

    private async Task<List<FeedSignal>> FetchDevToAsync(CancellationToken ct)
    {
        var signals = new List<FeedSignal>();

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SentientArchitect/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            foreach (var tag in DevToTags)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var url  = string.Format(DevToApiUrl, tag);
                    var json = await client.GetStringAsync(url, ct);

                    using var doc = JsonDocument.Parse(json);
                    foreach (var article in doc.RootElement.EnumerateArray().Take(5))
                    {
                        var title    = article.TryGetProperty("title", out var t) ? t.GetString() : null;
                        var articleUrl = article.TryGetProperty("url", out var u) ? u.GetString() : null;
                        var description = article.TryGetProperty("description", out var d) ? d.GetString() : null;

                        if (string.IsNullOrWhiteSpace(title)) continue;

                        var text = string.IsNullOrWhiteSpace(description) ? title : $"{title} — {description}";
                        signals.Add(new FeedSignal(text, articleUrl ?? "https://dev.to", SignalSource.DevTo));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Dev.to fetch failed for tag '{Tag}'.", tag);
                }
            }

            logger.LogInformation("Dev.to: {Count} articles.", signals.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dev.to overall fetch failed.");
        }

        return signals;
    }

    // ── Medium RSS ───────────────────────────────────────────────────────────

    private async Task<List<FeedSignal>> FetchMediumRssAsync(CancellationToken ct)
    {
        var signals = new List<FeedSignal>();

        foreach (var tag in MediumTags)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var url = $"https://medium.com/feed/tag/{tag}";
                using var client = httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SentientArchitect/1.0");

                var xml      = await client.GetStringAsync(url, ct);
                var tagItems = ParseRssFeed(xml, "https://medium.com", SignalSource.MediumRss, maxItems: 5);
                signals.AddRange(tagItems);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Medium RSS fetch failed for tag '{Tag}'.", tag);
            }
        }

        logger.LogInformation("Medium RSS: {Count} articles.", signals.Count);
        return signals;
    }

    private static List<FeedSignal> ParseRssFeed(string xml, string fallbackUrl, SignalSource source, int maxItems = 10)
    {
        var signals = new List<FeedSignal>();

        try
        {
            var doc = XDocument.Parse(xml);

            // Atom feeds use the atom namespace; RSS uses no namespace
            XNamespace atom = "http://www.w3.org/2005/Atom";

            // Try Atom first (GitHub Releases uses Atom)
            var atomEntries = doc.Descendants(atom + "entry").Take(maxItems).ToList();
            if (atomEntries.Count > 0)
            {
                foreach (var entry in atomEntries)
                {
                    var title = entry.Element(atom + "title")?.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    var link    = entry.Elements(atom + "link")
                                       .FirstOrDefault(e => e.Attribute("rel")?.Value != "alternate" || true)
                                       ?.Attribute("href")?.Value ?? fallbackUrl;
                    var summary = entry.Element(atom + "summary")?.Value
                               ?? entry.Element(atom + "content")?.Value ?? string.Empty;

                    summary = StripHtml(summary);

                    var text = string.IsNullOrWhiteSpace(summary)
                        ? title
                        : $"{title} — {summary[..Math.Min(200, summary.Length)]}";

                    signals.Add(new FeedSignal(text, link, source));
                }
                return signals;
            }

            // Fall back to RSS 2.0
            var rssItems = doc.Descendants("item").Take(maxItems).ToList();
            foreach (var item in rssItems)
            {
                var title = item.Element("title")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;

                var link    = item.Element("link")?.Value?.Trim() ?? fallbackUrl;
                var summary = item.Element("description")?.Value ?? string.Empty;
                summary     = StripHtml(summary);

                var text = string.IsNullOrWhiteSpace(summary)
                    ? title
                    : $"{title} — {summary[..Math.Min(200, summary.Length)]}";

                signals.Add(new FeedSignal(text, link, source));
            }
        }
        catch
        {
            // Malformed XML — skip
        }

        return signals;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ").Trim();
    }

    // ── GitHub Releases ──────────────────────────────────────────────────────

    private async Task<List<FeedSignal>> FetchGitHubReleasesAsync(CancellationToken ct)
    {
        var signals = new List<FeedSignal>();

        foreach (var repo in WatchedRepos)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var url = $"https://github.com/{repo}/releases.atom";
                using var client = httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SentientArchitect/1.0");

                var xml   = await client.GetStringAsync(url, ct);
                var items = ParseRssFeed(xml, $"https://github.com/{repo}/releases", SignalSource.GitHubReleases, maxItems: 3);

                // Prefix with repo name for LLM context
                foreach (var s in items)
                    signals.Add(s with { Text = $"[{repo}] {s.Text}" });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "GitHub Releases fetch failed for '{Repo}'.", repo);
            }
        }

        logger.LogInformation("GitHub Releases: {Count} entries.", signals.Count);
        return signals;
    }

    // ── Profile context ──────────────────────────────────────────────────────

    private async Task<string> BuildProfileContextAsync(CancellationToken ct)
    {
        try
        {
            var profiles = await db.UserProfiles
                .AsNoTracking()
                .ToListAsync(ct);

            if (profiles.Count == 0) return string.Empty;

            // Aggregate the most common stacks and patterns across all users
            var allStacks = profiles
                .SelectMany(p => p.PreferredStack)
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Take(15)
                .Select(g => g.Key);

            var allPatterns = profiles
                .SelectMany(p => p.KnownPatterns)
                .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key);

            return $"User community stack: {string.Join(", ", allStacks)}. " +
                   $"Known patterns: {string.Join(", ", allPatterns)}.";
        }
        catch
        {
            return string.Empty;
        }
    }

    // ── LLM Analysis ─────────────────────────────────────────────────────────

    private async Task<List<TrendItem>> AnalyzeWithLlmAsync(
        List<FeedSignal> signals, string profileContext, CancellationToken ct)
    {
        var signalBlock = string.Join("\n", signals.Select(s => $"[{s.Source}] {s.Text}"));

        var profileSection = string.IsNullOrWhiteSpace(profileContext)
            ? ""
            : $"\n\nUser community context (use this to prioritize relevance): {profileContext}";

        var prompt =
            "You are a technology trend analyst for a developer knowledge platform.\n" +
            "Analyze the following signals collected from GitHub Search (starred repos), Hacker News, Dev.to, Medium, and GitHub Releases.\n" +
            "Signals prefixed with [GitHub:CategoryHint] come from high-starred architecture repositories — give them extra weight.\n" +
            "Identify up to 15 distinct technology trends visible in this data." +
            profileSection + "\n\n" +
            "For each trend, provide:\n" +
            "- name: the technology, tool, language, framework, or pattern name (derive from the repo/article, not the category hint)\n" +
            "- category: one of Framework, Language, Tool, Pattern, Platform, Library, BestPractice, Innovation, Architecture, DevOps, Testing\n" +
            "  * BestPractice: coding conventions, SOLID, clean code, DDD, CQRS, design patterns\n" +
            "  * Innovation: emerging tech, AI/ML tooling, novel paradigms, experimental frameworks\n" +
            "  * Architecture: system design, distributed systems, microservices, hexagonal arch\n" +
            "  * DevOps: CI/CD, containers, IaC, observability, platform engineering\n" +
            "  * Testing: TDD, BDD, contract testing, test frameworks\n" +
            "- direction: one of Rising, Stable, Declining\n" +
            "- score: relevance score 0.0–1.0 (boost score if relevant to the user community context above; repos with ★10000+ deserve score ≥ 0.7)\n" +
            "- description: 1-2 sentences on why this is trending and why it matters to developers\n" +
            "- sources: URLs from the signals that contributed to this trend\n" +
            "- starCount: integer star count if available from a [GitHub:*] signal, otherwise omit or null\n" +
            "- githubUrl: the GitHub repo URL if available from a [GitHub:*] signal, otherwise omit or null\n\n" +
            "Return ONLY a raw JSON array, no markdown, no code blocks:\n" +
            "[\n" +
            "  {\"name\":\"EventStorming\",\"category\":\"BestPractice\",\"direction\":\"Rising\",\"score\":0.82," +
            "\"description\":\"DDD technique gaining traction.\",\"sources\":[\"https://github.com/...\"],\"starCount\":4200,\"githubUrl\":\"https://github.com/...\"}\n" +
            "]\n\n" +
            "Signals:\n" + signalBlock;

        try
        {
            var history = new ChatHistory();
            history.AddUserMessage(prompt);

            var response = await chatService!.GetChatMessageContentAsync(history, cancellationToken: ct);
            var json     = (response.Content ?? "[]").Trim();

            if (json.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence    = json.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewline > 0 && lastFence > firstNewline)
                    json = json[(firstNewline + 1)..lastFence].Trim();
            }

            return JsonSerializer.Deserialize<List<TrendItem>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM analysis failed.");
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
                logger.LogInformation("New trend: {Name} ({Category}).", item.Name, item.Category);
            }
            else
            {
                trend = await db.TechnologyTrends.FindAsync([existing.Id], ct) ?? existing;
            }

            trend.UpdateRelevance(item.Score, item.Direction, item.Description);

            if (item.StarCount.HasValue && item.StarCount > 0)
                trend.UpdateGitHubStats(item.StarCount.Value, item.GitHubUrl);

            foreach (var source in item.Sources)
                trend.AddSource(source);

            var snapshotExists = await db.TrendSnapshots
                .AnyAsync(s => s.TechnologyTrendId == trend.Id && s.Date == today, ct);

            if (!snapshotExists)
            {
                var snapshot = new TrendSnapshot(trend.Id, item.Score, item.Direction, today, item.Description);
                trend.RecordSnapshot(snapshot);
                db.TrendSnapshots.Add(snapshot);
            }
        }

        var saved = await db.SaveChangesAsync(ct);
        logger.LogInformation("Upserted {Count} trends, {Changes} DB changes.", items.Count, saved);
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private enum SignalSource
    {
        GitHubSearch,
        HackerNews,
        DevTo,
        MediumRss,
        GitHubReleases
    }

    private record FeedSignal(
        string Text,
        string Url,
        SignalSource Source,
        int StarCount = 0,
        string? CategoryHint = null);

    private sealed class TrendItem
    {
        public string Name { get; init; } = string.Empty;
        public TrendCategory Category { get; init; }
        public TrendDirection Direction { get; init; }
        public float Score { get; init; }
        public string? Description { get; init; }
        public List<string> Sources { get; init; } = [];
        public int? StarCount { get; init; }
        public string? GitHubUrl { get; init; }
    }
}
