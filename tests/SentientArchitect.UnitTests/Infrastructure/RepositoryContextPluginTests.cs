using FluentAssertions;
using NSubstitute;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.Infrastructure.Agents.Consultant;
using SentientArchitect.UnitTests.Helpers;

namespace SentientArchitect.UnitTests.Infrastructure;

public class RepositoryContextPluginTests
{
    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IApplicationDbContext _db;
    private readonly RepositoryContextPlugin _sut;

    public RepositoryContextPluginTests()
    {
        _db  = Substitute.For<IApplicationDbContext>();
        _sut = new RepositoryContextPlugin(_db);
    }

    [Fact]
    public async Task GetUserRepositoriesContextAsync_ReturnsNoReposMessage_WhenUserHasNoRepositories()
    {
        // Pre-create to avoid NSubstitute nested-call detection
        var repos = AsyncDbSetHelper.Create<RepositoryInfo>();
        _db.Repositories.Returns(repos);

        var result = await _sut.GetUserRepositoriesContextAsync(UserId.ToString());

        result.Should().Contain("No repositories found");
        result.Should().Contain("/api/v1/repositories");
    }

    [Fact]
    public async Task GetUserRepositoriesContextAsync_IncludesRepoUrl_WhenUserHasAnalyzedRepo()
    {
        var repo    = CreateAnalyzedRepo("https://github.com/user/my-dotnet-app");
        var reports = AsyncDbSetHelper.Create<AnalysisReport>();
        var repoSet = AsyncDbSetHelper.Create(repo);

        _db.Repositories.Returns(repoSet);
        _db.AnalysisReports.Returns(reports);

        var result = await _sut.GetUserRepositoriesContextAsync(UserId.ToString());

        result.Should().Contain("my-dotnet-app");
        result.Should().Contain("CODEBASE CONTEXT");
    }

    [Fact]
    public async Task GetUserRepositoriesContextAsync_IncludesPendingRepoNotice_WhenRepoPendingAnalysis()
    {
        var pending = new RepositoryInfo(UserId, TenantId, "https://github.com/user/pending-repo", RepositoryTrust.Internal);
        var reports = AsyncDbSetHelper.Create<AnalysisReport>();
        var repoSet = AsyncDbSetHelper.Create(pending);

        _db.Repositories.Returns(repoSet);
        _db.AnalysisReports.Returns(reports);

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

        var repoSet     = AsyncDbSetHelper.Create(repo);
        var reportSet   = AsyncDbSetHelper.Create(report);
        var findingSet  = AsyncDbSetHelper.Create(directCtxFinding);

        _db.Repositories.Returns(repoSet);
        _db.AnalysisReports.Returns(reportSet);
        _db.AnalysisFindings.Returns(findingSet);

        var result = await _sut.GetUserRepositoriesContextAsync(UserId.ToString());

        result.Should().Contain("Detected patterns");
        result.Should().Contain("DbContext");
        result.Should().Contain("do NOT recommend");
    }

    [Fact]
    public async Task GetUserRepositoriesContextAsync_IncludesImportantWarning()
    {
        var repo    = CreateAnalyzedRepo("https://github.com/user/project");
        var reports = AsyncDbSetHelper.Create<AnalysisReport>();
        var repoSet = AsyncDbSetHelper.Create(repo);

        _db.Repositories.Returns(repoSet);
        _db.AnalysisReports.Returns(reports);

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
        var reports  = AsyncDbSetHelper.Create<AnalysisReport>();
        var repoSet  = AsyncDbSetHelper.Create(analyzed, pending);

        _db.Repositories.Returns(repoSet);
        _db.AnalysisReports.Returns(reports);

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
        var repoSet    = AsyncDbSetHelper.Create(repo);
        var reportSet  = AsyncDbSetHelper.Create(report);
        var findingSet = AsyncDbSetHelper.Create<AnalysisFinding>();

        _db.Repositories.Returns(repoSet);
        _db.AnalysisReports.Returns(reportSet);
        _db.AnalysisFindings.Returns(findingSet);

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
