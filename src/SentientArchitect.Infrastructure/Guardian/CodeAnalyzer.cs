using System.Text.RegularExpressions;
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

            // ── Architectural pattern detection (aggregate over all files) ────────
            var architectureFindings = await DetectArchitecturalPatternsAsync(csFiles, report.Id, ct);
            findings.AddRange(architectureFindings);

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

            // Cleanup must not invalidate a successfully completed analysis report.
            if (Directory.Exists(clonePath))
            {
                try
                {
                    Directory.Delete(clonePath, true);
                }
                catch (Exception cleanEx)
                {
                    logger.LogWarning(cleanEx,
                        "Analysis completed but cleanup failed for repository {RepositoryInfoId}.",
                        repositoryInfoId);
                }
            }

            try
            {
                await reporter.ReportCompleteAsync(repositoryInfoId, report.Id, ct);
            }
            catch (Exception reportEx)
            {
                logger.LogWarning(reportEx,
                    "Analysis completed but SignalR completion notification failed for repository {RepositoryInfoId}.",
                    repositoryInfoId);
            }

            logger.LogInformation("Analysis completed for repository {RepositoryInfoId}. Report: {ReportId}. {Summary}",
                repositoryInfoId, report.Id, summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analysis failed for repository {RepositoryInfoId}.", repositoryInfoId);

            // If the report is already completed, do not overwrite it to Failed due to
            // post-processing errors (cleanup/notifications).
            if (report?.Status == AnalysisStatus.Completed)
            {
                return;
            }

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

    // ── Architectural pattern detection ───────────────────────────────────────

    /// <summary>
    /// Scans all C# source files for architectural patterns (Repository, direct-DbContext,
    /// Minimal API, MVC Controllers, MediatR, Vertical Slice, Clean Architecture layers)
    /// and returns Architecture-category findings the Consultant Agent can use as context.
    /// </summary>
    internal static async Task<List<AnalysisFinding>> DetectArchitecturalPatternsAsync(
        string[] csFiles,
        Guid reportId,
        CancellationToken ct)
    {
        var repoInterfaces   = new HashSet<string>(StringComparer.Ordinal);
        var repoClasses      = new HashSet<string>(StringComparer.Ordinal);
        var dbContextFiles   = new HashSet<string>(StringComparer.Ordinal);
        var mediatorFiles    = new HashSet<string>(StringComparer.Ordinal);
        var minimalApiFiles  = new HashSet<string>(StringComparer.Ordinal);
        var controllerFiles  = new HashSet<string>(StringComparer.Ordinal);
        var featureFolderFiles = new HashSet<string>(StringComparer.Ordinal);

        var repoIfacePattern  = new Regex(@"^I\w+Repository$",  RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var repoClassPattern  = new Regex(@"Repository$",       RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var mediatorNames     = new HashSet<string>(StringComparer.Ordinal) { "IMediator", "ISender", "IRequestHandler" };
        var minimalApiMethods = new HashSet<string>(StringComparer.Ordinal)
            { "MapGet", "MapPost", "MapPut", "MapDelete", "MapPatch", "MapGroup", "MapEndpoints" };

        foreach (var filePath in csFiles)
        {
            try
            {
                var code = await File.ReadAllTextAsync(filePath, ct);
                var root = await CSharpSyntaxTree.ParseText(code).GetRootAsync(ct);

                foreach (var node in root.DescendantNodes())
                {
                    // Repository interface: interface IXxxRepository
                    if (node is InterfaceDeclarationSyntax iface &&
                        repoIfacePattern.IsMatch(iface.Identifier.Text))
                    {
                        repoInterfaces.Add(filePath);
                    }

                    // Repository class: class XxxRepository (but not built-in domain words)
                    if (node is ClassDeclarationSyntax cls &&
                        repoClassPattern.IsMatch(cls.Identifier.Text) &&
                        cls.Identifier.Text is not ("RepositoryInfo" or "RepositoryTrust"))
                    {
                        repoClasses.Add(filePath);
                    }

                    // Direct DbContext/IApplicationDbContext injection —
                    // handles both traditional constructors and C# 12 primary constructors.
                    if (node is ConstructorDeclarationSyntax ctor)
                    {
                        foreach (var param in ctor.ParameterList.Parameters)
                        {
                            var typeName = param.Type?.ToString() ?? string.Empty;
                            if (typeName.EndsWith("Context", StringComparison.Ordinal) ||
                                typeName.StartsWith("IApplicationDbContext", StringComparison.Ordinal))
                            {
                                dbContextFiles.Add(filePath);
                                break;
                            }
                        }
                    }

                    // Primary constructor syntax (C# 12): class Foo(AppDbContext db)
                    if (node is ClassDeclarationSyntax primaryCtorCls &&
                        primaryCtorCls.ParameterList is { Parameters.Count: > 0 } primaryParams)
                    {
                        foreach (var param in primaryParams.Parameters)
                        {
                            var typeName = param.Type?.ToString() ?? string.Empty;
                            if (typeName.EndsWith("Context", StringComparison.Ordinal) ||
                                typeName.StartsWith("IApplicationDbContext", StringComparison.Ordinal))
                            {
                                dbContextFiles.Add(filePath);
                                break;
                            }
                        }
                    }

                    // MediatR / CQRS
                    if (node is IdentifierNameSyntax id && mediatorNames.Contains(id.Identifier.Text))
                        mediatorFiles.Add(filePath);

                    // Minimal API
                    if (node is InvocationExpressionSyntax inv)
                    {
                        var methodName = (inv.Expression as MemberAccessExpressionSyntax)
                                         ?.Name.Identifier.Text ?? string.Empty;
                        if (minimalApiMethods.Contains(methodName))
                            minimalApiFiles.Add(filePath);
                    }

                    // MVC Controllers
                    if (node is ClassDeclarationSyntax ctrlCls)
                    {
                        var hasAttr = ctrlCls.AttributeLists
                            .SelectMany(a => a.Attributes)
                            .Any(a => a.Name.ToString() is "ApiController" or "Controller");

                        var inherits = ctrlCls.BaseList?.Types
                            .Any(t => t.Type.ToString() is "ControllerBase" or "Controller") ?? false;

                        if (hasAttr || inherits)
                            controllerFiles.Add(filePath);
                    }
                }

                // Vertical Slice / Feature folder organisation (path heuristic)
                if (filePath.Contains("/Features/", StringComparison.OrdinalIgnoreCase) ||
                    filePath.Contains(@"\Features\", StringComparison.OrdinalIgnoreCase))
                {
                    featureFolderFiles.Add(filePath);
                }
            }
            catch
            {
                // Tolerate parse failures — don't abort the whole detection pass
            }
        }

        var findings = new List<AnalysisFinding>();

        // Repository pattern
        if (repoInterfaces.Count > 0)
        {
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Info, "Architecture",
                $"Repository pattern detected: {repoInterfaces.Count} IXxxRepository interface(s). " +
                "NOTE: if the codebase also uses direct DbContext injection, " +
                "adding more repository abstractions contradicts the established convention."));
        }

        if (repoClasses.Count > 0)
        {
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Info, "Architecture",
                $"Repository class implementations detected ({repoClasses.Count} file(s)). " +
                "Verify whether this is intentional or conflicts with a direct-DbContext convention."));
        }

        // Direct DbContext injection (no repo layer)
        if (dbContextFiles.Count > 0)
        {
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Info, "Architecture",
                $"Direct DbContext/IApplicationDbContext injection used in {dbContextFiles.Count} file(s). " +
                "This codebase injects the DbContext directly — " +
                "do NOT recommend adding a repository abstraction layer on top."));
        }

        // MediatR / CQRS
        if (mediatorFiles.Count > 0)
        {
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Info, "Architecture",
                $"MediatR/CQRS pattern detected ({mediatorFiles.Count} file(s) reference IMediator/ISender). " +
                "Architecture recommendations should align with command/query separation."));
        }

        // API style
        if (minimalApiFiles.Count > 0 && controllerFiles.Count == 0)
        {
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Info, "Architecture",
                $"Minimal API endpoints detected ({minimalApiFiles.Count} file(s)). " +
                "This project uses Minimal API — do NOT recommend switching to MVC Controllers."));
        }
        else if (controllerFiles.Count > 0 && minimalApiFiles.Count == 0)
        {
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Info, "Architecture",
                $"MVC Controller pattern detected ({controllerFiles.Count} controller(s)). " +
                "Recommendations should be consistent with the existing Controller-based approach."));
        }
        else if (controllerFiles.Count > 0 && minimalApiFiles.Count > 0)
        {
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Info, "Architecture",
                $"Mixed API style: {minimalApiFiles.Count} Minimal API file(s) and {controllerFiles.Count} Controller(s). " +
                "Consider standardising on one approach."));
        }

        // Vertical Slice / Feature folder
        if (featureFolderFiles.Count > 0)
        {
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Info, "Architecture",
                $"Vertical Slice / Feature-folder organisation detected ({featureFolderFiles.Count} file(s) under a 'Features' folder). " +
                "Architecture advice should preserve this organisation."));
        }

        // Clean Architecture layers (path heuristic)
        var layerKeywords = new[] { "Domain", "Application", "Infrastructure", "Data" };
        var detectedLayers = layerKeywords
            .Where(layer => csFiles.Any(f =>
                f.Contains($"{Path.DirectorySeparatorChar}{layer}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                f.Contains($"/{layer}/", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (detectedLayers.Count >= 3)
        {
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Info, "Architecture",
                $"Clean Architecture layer structure detected: [{string.Join(", ", detectedLayers)}]. " +
                "Respect the existing layer boundaries in all recommendations."));
        }

        return findings;
    }
}
