using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Infrastructure.Guardian;

public sealed class CodeAnalyzer(
    IApplicationDbContext db,
    IAnalysisProgressReporter reporter,
    ILogger<CodeAnalyzer> logger) : ICodeAnalyzer
{
    public async Task AnalyzeAsync(Guid repositoryInfoId, CancellationToken ct = default)
    {
        AnalysisReport? report = null;
        string? clonePath = null;

        try
        {
            var repositoryInfo = await db.Repositories
                .FirstOrDefaultAsync(r => r.Id == repositoryInfoId, ct);

            if (repositoryInfo is null)
            {
                logger.LogWarning("RepositoryInfo {RepositoryInfoId} not found — skipping analysis.", repositoryInfoId);
                return;
            }

            // Create report
            report = new AnalysisReport(repositoryInfoId);
            db.AnalysisReports.Add(report);
            await db.SaveChangesAsync(ct);

            report.MarkInProgress();
            await db.SaveChangesAsync(ct);

            await reporter.ReportProgressAsync(repositoryInfoId, 5, "Starting analysis...", ct);

            // Clone repository
            clonePath = Path.Combine(Path.GetTempPath(), "sentient-repos", repositoryInfoId.ToString());
            if (Directory.Exists(clonePath))
                Directory.Delete(clonePath, true);

            LibGit2Sharp.Repository.Clone(repositoryInfo.RepositoryUrl, clonePath);

            repositoryInfo.SetLocalPath(clonePath);
            await db.SaveChangesAsync(ct);

            await reporter.ReportProgressAsync(repositoryInfoId, 20, "Repository cloned", ct);

            // Extract git metadata
            using (var gitRepo = new LibGit2Sharp.Repository(clonePath))
            {
                var defaultBranch = gitRepo.Head.FriendlyName;
                var lastCommit = gitRepo.Commits.FirstOrDefault()?.Author.When.UtcDateTime;
                var contributorCount = gitRepo.Commits
                    .Select(c => c.Author.Email)
                    .Distinct()
                    .Count();

                repositoryInfo.UpdateGitMetadata(defaultBranch, null, contributorCount, lastCommit);
                await db.SaveChangesAsync(ct);
            }

            await reporter.ReportProgressAsync(repositoryInfoId, 35, "Git metadata extracted", ct);

            // Find all .cs files (exclude obj/bin)
            var csFiles = Directory.GetFiles(clonePath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(@"\obj\") && !f.Contains(@"\bin\")
                         && !f.Contains("/obj/") && !f.Contains("/bin/"))
                .ToArray();

            await reporter.ReportProgressAsync(repositoryInfoId, 50, $"Analyzing {csFiles.Length} C# files...", ct);

            // Analyze each file with Roslyn
            var findings = new List<AnalysisFinding>();

            foreach (var filePath in csFiles)
            {
                try
                {
                    var code = await File.ReadAllTextAsync(filePath, ct);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = await tree.GetRootAsync(ct);

                    foreach (var node in root.DescendantNodes())
                    {
                        // Empty catch blocks
                        if (node is CatchClauseSyntax catchClause
                            && catchClause.Block.Statements.Count == 0)
                        {
                            var lineSpan = tree.GetLineSpan(catchClause.Span);
                            findings.Add(new AnalysisFinding(
                                report.Id,
                                FindingSeverity.Warning,
                                "CodeQuality",
                                "Empty catch block swallows exceptions",
                                filePath,
                                lineSpan.StartLinePosition.Line + 1));
                        }

                        // God class (> 500 lines)
                        if (node is ClassDeclarationSyntax classDecl)
                        {
                            var lineSpan = tree.GetLineSpan(classDecl.Span);
                            var lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
                            if (lineCount > 500)
                            {
                                findings.Add(new AnalysisFinding(
                                    report.Id,
                                    FindingSeverity.Warning,
                                    "CodeQuality",
                                    $"Class '{classDecl.Identifier.Text}' has {lineCount} lines — consider splitting",
                                    filePath,
                                    lineSpan.StartLinePosition.Line + 1));
                            }
                        }
                    }

                    // TODO/FIXME in comments
                    foreach (var trivia in root.DescendantTrivia())
                    {
                        var triviaKind = trivia.RawKind;
                        if (triviaKind == (int)SyntaxKind.SingleLineCommentTrivia
                            || triviaKind == (int)SyntaxKind.MultiLineCommentTrivia)
                        {
                            var text = trivia.ToString();
                            if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase)
                                || text.Contains("FIXME", StringComparison.OrdinalIgnoreCase))
                            {
                                var lineSpan = tree.GetLineSpan(trivia.Span);
                                var trimmedText = text.Trim();
                                if (trimmedText.Length > 200)
                                    trimmedText = trimmedText[..200];

                                findings.Add(new AnalysisFinding(
                                    report.Id,
                                    FindingSeverity.Info,
                                    "TechDebt",
                                    trimmedText,
                                    filePath,
                                    lineSpan.StartLinePosition.Line + 1));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to analyze file {FilePath}.", filePath);
                }
            }

            // Security scan for external repos
            if (repositoryInfo.Trust == RepositoryTrust.External)
            {
                var buildFilePatterns = new[] { "*.csproj", "*.targets", "*.props", "*.ps1", "*.sh" };
                foreach (var pattern in buildFilePatterns)
                {
                    var buildFiles = Directory.GetFiles(clonePath, pattern, SearchOption.TopDirectoryOnly);
                    foreach (var buildFile in buildFiles)
                    {
                        try
                        {
                            var content = await File.ReadAllTextAsync(buildFile, ct);
                            if (content.Contains("<Exec Command=", StringComparison.OrdinalIgnoreCase)
                                || content.Contains("exec(", StringComparison.OrdinalIgnoreCase)
                                || content.Contains("Process.Start(", StringComparison.OrdinalIgnoreCase))
                            {
                                var filename = Path.GetFileName(buildFile);
                                findings.Add(new AnalysisFinding(
                                    report.Id,
                                    FindingSeverity.Critical,
                                    "Security",
                                    $"Suspicious code execution found in build files: {filename}",
                                    buildFile));
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to scan build file {BuildFile}.", buildFile);
                        }
                    }
                }
            }

            await reporter.ReportProgressAsync(repositoryInfoId, 80, "Finalizing report...", ct);

            // Persist findings
            foreach (var finding in findings)
                db.AnalysisFindings.Add(finding);

            var criticalCount = findings.Count(f => f.Severity == FindingSeverity.Critical);
            var summary = $"Analysis complete. Found {findings.Count} issues ({criticalCount} critical) across {csFiles.Length} C# files.";

            report.Complete(summary, findings.Count, criticalCount);
            repositoryInfo.MarkAnalyzed();
            repositoryInfo.ClearLocalPath();

            await db.SaveChangesAsync(ct);

            // Cleanup
            if (Directory.Exists(clonePath))
                Directory.Delete(clonePath, true);

            await reporter.ReportCompleteAsync(repositoryInfoId, report.Id, ct);

            logger.LogInformation("Analysis completed for repository {RepositoryInfoId}. Report: {ReportId}. {Summary}",
                repositoryInfoId, report.Id, summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analysis failed for repository {RepositoryInfoId}.", repositoryInfoId);

            if (report is not null)
            {
                try
                {
                    report.Fail(ex.Message);
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception saveEx)
                {
                    logger.LogError(saveEx, "Failed to persist error state for report.");
                }
            }

            try
            {
                await reporter.ReportErrorAsync(repositoryInfoId, ex.Message, ct);
            }
            catch (Exception reportEx)
            {
                logger.LogError(reportEx, "Failed to report error via SignalR.");
            }

            if (clonePath is not null && Directory.Exists(clonePath))
            {
                try { Directory.Delete(clonePath, true); }
                catch (Exception cleanEx) { logger.LogWarning(cleanEx, "Failed to clean up clone directory."); }
            }
        }
    }
}
