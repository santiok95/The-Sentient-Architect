using System.ComponentModel;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Domain.Entities;
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
        [Description("Optional repository ID to scope context to a single repository")] string? repositoryId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return "Could not resolve user context. Continue with generic guidance and ask for repository scope.";

        Guid? repositoryGuid = null;
        if (!string.IsNullOrWhiteSpace(repositoryId))
        {
            if (!Guid.TryParse(repositoryId, out var parsedRepositoryGuid))
                return "Repository scope is invalid. Ask the user to provide a valid repository id.";

            repositoryGuid = parsedRepositoryGuid;
        }

        var reposQuery = db.Repositories
            .AsNoTracking()
            .Where(r => r.UserId == userGuid);

        if (repositoryGuid.HasValue)
            reposQuery = reposQuery.Where(r => r.Id == repositoryGuid.Value);

        var repos = await reposQuery.ToListAsync(cancellationToken);

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

            if (!string.IsNullOrWhiteSpace(repo.AboutContent))
            {
                sb.AppendLine();
                sb.AppendLine("**Intención del proyecto (ABOUT.md del autor):**");
                sb.AppendLine(repo.AboutContent);
                sb.AppendLine("↑ Usá esta información para alinear tus recomendaciones con la visión del autor.");
            }

            var latestReport = await db.AnalysisReports
                .AsNoTracking()
                .Where(r => r.RepositoryInfoId == repo.Id && r.Status == AnalysisStatus.Completed)
                .OrderByDescending(r => r.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestReport is null)
                continue;

            if (!string.IsNullOrWhiteSpace(latestReport.Summary))
                sb.AppendLine($"- Analysis summary: {latestReport.Summary}");

            var allFindings = await db.AnalysisFindings
                .AsNoTracking()
                .Where(f => f.AnalysisReportId == latestReport.Id)
                .ToListAsync(cancellationToken);

            var architectureFindings = allFindings.Where(f => f.Category == "Architecture").ToList();
            if (architectureFindings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**Detected patterns (MUST be respected in every recommendation):**");
                foreach (var finding in architectureFindings)
                    sb.AppendLine($"  - {finding.Message}");
            }

            // Surface High and Critical findings so the consultant can reason about real issues
            var importantFindings = allFindings
                .Where(f => f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.High)
                .OrderBy(f => f.Severity)
                .ToList();

            if (importantFindings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"**Code quality findings — {latestReport.TotalFindings} total ({latestReport.CriticalFindings} critical):**");
                foreach (var group in importantFindings.GroupBy(f => f.Category))
                {
                    sb.AppendLine($"  [{group.Key}]");
                    foreach (var f in group)
                        sb.AppendLine($"    - [{f.Severity}] {f.Message} ({f.FilePath})");
                }
            }

            var medLowCount = allFindings.Count(f => f.Severity == FindingSeverity.Medium || f.Severity == FindingSeverity.Low);
            if (medLowCount > 0)
                sb.AppendLine($"- Additionally {medLowCount} medium/low findings (ask user if they want detail).");
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
