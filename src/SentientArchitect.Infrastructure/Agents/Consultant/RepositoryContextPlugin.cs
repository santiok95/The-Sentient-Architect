using System.ComponentModel;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Infrastructure.Agents.Consultant;

public sealed class RepositoryContextPlugin(IApplicationDbContext db)
{
    [KernelFunction, Description(
        "Get a summary of the user's previously analyzed repositories, including the architectural " +
        "patterns, conventions, and code-quality findings detected in their actual codebase. " +
        "ALWAYS call this before making architecture recommendations so that your advice is " +
        "aligned with — not contradictory to — the patterns already in use.")]
    public async Task<string> GetUserRepositoriesContextAsync(
        [Description("User ID to fetch repository context for")] string userId,
        CancellationToken cancellationToken = default)
    {
        var userGuid = Guid.Parse(userId);

        var repos = await db.Repositories
            .AsNoTracking()
            .Where(r => r.UserId == userGuid)
            .ToListAsync(cancellationToken);

        if (repos.Count == 0)
            return "No repositories found for this user. " +
                   "Recommend that they submit a repository for analysis " +
                   "(POST /api/v1/repositories) so you can give codebase-specific advice.";

        var analyzed = repos.Where(r => r.LastAnalyzedAt.HasValue).ToList();
        var pending  = repos.Where(r => !r.LastAnalyzedAt.HasValue).ToList();

        var sb = new StringBuilder();
        sb.AppendLine(
            $"CODEBASE CONTEXT — {repos.Count} repositor{(repos.Count == 1 ? "y" : "ies")} on record " +
            $"({analyzed.Count} analyzed, {pending.Count} pending):");

        foreach (var repo in analyzed)
        {
            sb.AppendLine();
            sb.AppendLine($"### {repo.RepositoryUrl}");
            if (repo.DefaultBranch is not null)
                sb.AppendLine($"- Default branch: {repo.DefaultBranch}");
            if (repo.ContributorCount.HasValue)
                sb.AppendLine($"- Contributors: {repo.ContributorCount}");
            if (repo.LastCommitAt.HasValue)
                sb.AppendLine($"- Last commit: {repo.LastCommitAt.Value:yyyy-MM-dd}");
            sb.AppendLine($"- Analyzed on: {repo.LastAnalyzedAt!.Value:yyyy-MM-dd}");

            var latestReport = await db.AnalysisReports
                .AsNoTracking()
                .Where(r => r.RepositoryInfoId == repo.Id && r.Status == AnalysisStatus.Completed)
                .OrderByDescending(r => r.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestReport is null)
                continue;

            if (!string.IsNullOrWhiteSpace(latestReport.Summary))
                sb.AppendLine($"- Analysis summary: {latestReport.Summary}");

            var architectureFindings = await db.AnalysisFindings
                .AsNoTracking()
                .Where(f => f.AnalysisReportId == latestReport.Id && f.Category == "Architecture")
                .ToListAsync(cancellationToken);

            if (architectureFindings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**Detected patterns (MUST be respected in every recommendation):**");
                foreach (var finding in architectureFindings)
                    sb.AppendLine($"  - {finding.Message}");
            }
        }

        if (pending.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Repositories pending analysis: " +
                          string.Join(", ", pending.Select(r => r.RepositoryUrl)));
        }

        sb.AppendLine();
        sb.AppendLine(
            "IMPORTANT: Every recommendation you give MUST be consistent with the patterns above. " +
            "Never silently recommend a pattern that contradicts an established convention in this codebase. " +
            "If you mention a generic alternative that conflicts with a detected pattern, " +
            "label it explicitly as 'Alternativa generica (no aplica a este proyecto)' and explain why.");

        return sb.ToString();
    }
}
