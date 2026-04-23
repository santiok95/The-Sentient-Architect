using FluentAssertions;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.Infrastructure.Agents.Consultant;
using SentientArchitect.UnitTests.Common;

namespace SentientArchitect.UnitTests.Infrastructure;

public class RepositoryContextPluginTests : TestBase
{
    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly RepositoryContextPlugin _sut;

    public RepositoryContextPluginTests()
    {
        _sut = new RepositoryContextPlugin(DbContext);
    }

    [Fact]
    public async Task GetUserRepositoriesContextAsync_ReturnsNoReposMessage_WhenUserHasNoRepositories()
    {
        var result = await _sut.GetUserRepositoriesContextAsync(UserId.ToString());

        result.Should().Contain("No repositories found");
        result.Should().Contain("/api/v1/repositories");
    }

    [Fact]
    public async Task GetUserRepositoriesContextAsync_IncludesRepoUrl_WhenUserHasAnalyzedRepo()
    {
        var repo    = CreateAnalyzedRepo("https://github.com/user/my-dotnet-app");
        DbContext.Repositories.Add(repo);
        await DbContext.SaveChangesAsync();

        var result = await _sut.GetUserRepositoriesContextAsync(UserId.ToString());

        result.Should().Contain("my-dotnet-app");
        result.Should().Contain("CODEBASE CONTEXT");
    }

    [Fact]
    public async Task GetUserRepositoriesContextAsync_IncludesPendingRepoNotice_WhenRepoPendingAnalysis()
    {
        var pending = new RepositoryInfo(UserId, TenantId, "https://github.com/user/pending-repo", RepositoryTrust.Internal);
        DbContext.Repositories.Add(pending);
        await DbContext.SaveChangesAsync();

        var result = await _sut.GetUserRepositoriesContextAsync(UserId.ToString());

        result.Should().Contain("pending-repo");
        result.Should().Contain("pending");
    }

    [Fact]
    public async Task GetUserRepositoriesContextAsync_IncludesArchitecturePatterns_WhenReportHasFindings()
    {
        var repo    = CreateAnalyzedRepo("https://github.com/user/dotnet-api");
        var report  = CreateCompletedReport(repo.Id, "Analysis complete. Found 0 issues.");

        var directCtxFinding = new AnalysisFinding(
            report.Id,
            FindingSeverity.Low,
            "Architecture",
            "Direct DbContext/IApplicationDbContext injection used in 7 file(s). " +
            "do NOT recommend adding a repository abstraction layer on top.");

        DbContext.Repositories.Add(repo);
        DbContext.AnalysisReports.Add(report);
        DbContext.AnalysisFindings.Add(directCtxFinding);
        await DbContext.SaveChangesAsync();

        var result = await _sut.GetUserRepositoriesContextAsync(UserId.ToString());

        result.Should().Contain("Detected patterns");
        result.Should().Contain("DbContext");
        result.Should().Contain("do NOT recommend");
    }

    [Fact]
    public async Task GetUserRepositoriesContextAsync_IncludesImportantWarning()
    {
        var repo    = CreateAnalyzedRepo("https://github.com/user/project");
        DbContext.Repositories.Add(repo);
        await DbContext.SaveChangesAsync();

        var result = await _sut.GetUserRepositoriesContextAsync(UserId.ToString());

        result.Should().Contain("IMPORTANT");
        result.Should().Contain("MUST be consistent");
        result.Should().Contain("Alternativa generica");
    }

    [Fact]
    public async Task GetUserRepositoriesContextAsync_CountsBothAnalysedAndPending()
    {
        var analyzed = CreateAnalyzedRepo("https://github.com/user/analyzed");
        var pending  = new RepositoryInfo(UserId, TenantId, "https://github.com/user/pending", RepositoryTrust.Internal);
        DbContext.Repositories.AddRange(analyzed, pending);
        await DbContext.SaveChangesAsync();

        var result = await _sut.GetUserRepositoriesContextAsync(UserId.ToString());

        result.Should().Contain("2 repositories");
        result.Should().Contain("1 analyzed");
        result.Should().Contain("1 pending");
    }

    [Fact]
    public async Task GetUserRepositoriesContextAsync_IncludesRepoSummary_WhenReportHasSummary()
    {
        var repo       = CreateAnalyzedRepo("https://github.com/user/api");
        var report     = CreateCompletedReport(repo.Id, "Found 3 issues (0 critical) across 42 C# files.");
        DbContext.Repositories.Add(repo);
        DbContext.AnalysisReports.Add(report);
        await DbContext.SaveChangesAsync();

        var result = await _sut.GetUserRepositoriesContextAsync(UserId.ToString());

        result.Should().Contain("42 C# files");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private RepositoryInfo CreateAnalyzedRepo(string url)
    {
        var repo = new RepositoryInfo(UserId, TenantId, url, RepositoryTrust.Internal);
        repo.UpdateGitMetadata("main", null, 2, DateTime.UtcNow.AddDays(-5));
        repo.MarkAnalyzed();
        return repo;
    }

    private static AnalysisReport CreateCompletedReport(Guid repositoryInfoId, string summary)
    {
        var report = new AnalysisReport(repositoryInfoId);
        report.MarkInProgress();
        report.Complete(summary, 0, 0);
        return report;
    }
}
