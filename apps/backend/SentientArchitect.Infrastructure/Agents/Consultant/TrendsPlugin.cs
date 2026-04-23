using System.ComponentModel;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Infrastructure.Agents.Consultant;

public sealed class TrendsPlugin(IApplicationDbContext db)
{
    [KernelFunction, Description(
        "Get technology trends relevant to a given stack or set of keywords. " +
        "Call this when the user asks about modernization opportunities, what technologies are trending, " +
        "or when analyzing an internal project to suggest improvements. " +
        "Returns Rising and Stable trends filtered by relevance to the provided stack keywords.")]
    public async Task<string> GetRelevantTrendsAsync(
        [Description(
            "Stack or technology keywords to filter trends by, e.g. ['dotnet', 'csharp', 'react', 'typescript']. " +
            "Use terms that would appear in trend names, categories, or descriptions."
        )] string[] stackKeywords,
        [Description("Maximum number of trends to return. Default: 10.")] int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var keywords = stackKeywords
            .Select(k => k.Trim().ToLowerInvariant())
            .Where(k => k.Length > 1)
            .ToArray();

        // Fetch ALL qualifying trends first, THEN filter by keyword client-side.
        // A pre-filter Take() would discard keyword-relevant trends that have lower scores than
        // unrelated top-scoring trends (e.g., 30 "System Design" trends at 0.95 blocking 10
        // "Testing" trends at 0.88 from ever reaching the keyword filter).
        var allTrends = await db.TechnologyTrends
            .AsNoTracking()
            .Where(t =>
                (t.Direction == TrendDirection.Rising || t.Direction == TrendDirection.Stable) &&
                t.RelevanceScore >= 0.4f)
            .OrderByDescending(t => t.RelevanceScore)
            .ToListAsync(cancellationToken);

        var trends = keywords.Length > 0
            ? allTrends
                .Where(t =>
                    keywords.Any(kw =>
                        t.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                        (t.Description?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        t.Category.ToString().Contains(kw, StringComparison.OrdinalIgnoreCase)))
                .Take(maxResults)
                .ToList()
            : allTrends.Take(maxResults).ToList();

        if (trends.Count == 0)
        {
            // Fallback: return top Rising trends regardless of keyword filter
            trends = await db.TechnologyTrends
                .AsNoTracking()
                .Where(t => t.Direction == TrendDirection.Rising && t.RelevanceScore >= 0.5f)
                .OrderByDescending(t => t.RelevanceScore)
                .Take(maxResults)
                .ToListAsync(cancellationToken);

            if (trends.Count == 0)
                return "No hay trends registrados aún. El scanner de trends actualiza automáticamente cada 6 horas.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## Trends tecnológicos relevantes ({trends.Count} encontrados)");
        sb.AppendLine();
        sb.AppendLine("Usa estos trends para sugerir oportunidades de modernización concretas y respaldadas por datos reales del ecosistema.");
        sb.AppendLine();

        foreach (var t in trends)
        {
            var direction = t.Direction switch
            {
                TrendDirection.Rising   => "↑ En auge",
                TrendDirection.Stable   => "→ Estable",
                TrendDirection.Declining => "↓ Declinando",
                _ => t.Direction.ToString()
            };

            sb.AppendLine($"### [{t.Category}] {t.Name} — {direction}");

            if (!string.IsNullOrWhiteSpace(t.Description))
                sb.AppendLine(t.Description);

            if (t.StarCount.HasValue)
                sb.AppendLine($"- GitHub stars: {t.StarCount:N0}");

            if (!string.IsNullOrWhiteSpace(t.GitHubUrl))
                sb.AppendLine($"- Repositorio: {t.GitHubUrl}");

            if (t.Sources.Count > 0)
                sb.AppendLine($"- Fuentes: {string.Join(", ", t.Sources.Take(2))}");

            sb.AppendLine();
        }

        sb.AppendLine(
            "IMPORTANTE: Al recomendar un trend, siempre explicá " +
            "(1) por qué es relevante para ESTE proyecto específicamente, " +
            "(2) cómo se integraría con el stack actual, y " +
            "(3) cuál sería el primer paso concreto para adoptarlo.");

        return sb.ToString();
    }
}
