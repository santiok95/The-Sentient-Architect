using FluentAssertions;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Application.Features.Repositories.GetAnalysisReport;
using SentientArchitect.Application.Features.Repositories.GetRepositoryAnalysis;
using SentientArchitect.Application.Features.Repositories.GetRepositoryReports;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.UnitTests.Common;

namespace SentientArchitect.UnitTests.Application.Features.Repositories;

public class RepositoryAnalysisReadAuthorizationUseCaseTests : TestBase
{
    [Fact]
    public async Task GetRepositoryAnalysis_ShouldReturnNotFound_WhenRepositoryBelongsToAnotherUser()
    {
        var repository = await CreateRepositoryAsync(Guid.NewGuid());
        var useCase = new GetRepositoryAnalysisUseCase(DbContext);

        var result = await useCase.ExecuteAsync(
            new GetRepositoryAnalysisRequest(repository.Id, Guid.NewGuid()));

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetRepositoryAnalysis_ShouldReturnRepositoryData_WhenRepositoryBelongsToCurrentUser()
    {
        var ownerUserId = Guid.NewGuid();
        var repository = await CreateRepositoryAsync(ownerUserId);
        await CreateReportAsync(repository.Id, FindingSeverity.High);
        var useCase = new GetRepositoryAnalysisUseCase(DbContext);

        var result = await useCase.ExecuteAsync(
            new GetRepositoryAnalysisRequest(repository.Id, ownerUserId));

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.RepositoryInfo.GitUrl.Should().Be(repository.RepositoryUrl);
        result.Data.Reports.Should().ContainSingle();
    }

    [Fact]
    public async Task GetRepositoryReports_ShouldReturnNotFound_WhenRepositoryBelongsToAnotherUser()
    {
        var repository = await CreateRepositoryAsync(Guid.NewGuid());
        await CreateReportAsync(repository.Id, FindingSeverity.Low);
        var useCase = new GetRepositoryReportsUseCase(DbContext);

        var result = await useCase.ExecuteAsync(
            new GetRepositoryReportsRequest(repository.Id, Guid.NewGuid()));

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetRepositoryReports_ShouldReturnReports_WhenRepositoryBelongsToCurrentUser()
    {
        var ownerUserId = Guid.NewGuid();
        var repository = await CreateRepositoryAsync(ownerUserId);
        var report = await CreateReportAsync(repository.Id, FindingSeverity.Medium);
        var useCase = new GetRepositoryReportsUseCase(DbContext);

        var result = await useCase.ExecuteAsync(
            new GetRepositoryReportsRequest(repository.Id, ownerUserId));

        result.Succeeded.Should().BeTrue();
        result.Data.Should().ContainSingle(r => r.Id == report.Id);
    }

    [Fact]
    public async Task GetAnalysisReport_ShouldReturnNotFound_WhenReportBelongsToAnotherUser()
    {
        var repository = await CreateRepositoryAsync(Guid.NewGuid());
        var report = await CreateReportAsync(repository.Id, FindingSeverity.Critical);
        var useCase = new GetAnalysisReportUseCase(DbContext);

        var result = await useCase.ExecuteAsync(
            new GetAnalysisReportRequest(report.Id, Guid.NewGuid()));

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetAnalysisReport_ShouldReturnReport_WhenReportBelongsToCurrentUser()
    {
        var ownerUserId = Guid.NewGuid();
        var repository = await CreateRepositoryAsync(ownerUserId);
        var report = await CreateReportAsync(repository.Id, FindingSeverity.Critical);
        var useCase = new GetAnalysisReportUseCase(DbContext);

        var result = await useCase.ExecuteAsync(
            new GetAnalysisReportRequest(report.Id, ownerUserId));

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(report.Id);
        result.Data.Findings.Should().ContainSingle(f => f.Severity == "Critical");
    }

    private async Task<RepositoryInfo> CreateRepositoryAsync(Guid ownerUserId)
    {
        var repository = new RepositoryInfo(
            ownerUserId,
            ownerUserId,
            $"https://github.com/{ownerUserId:N}/repo",
            RepositoryTrust.Internal);

        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        return repository;
    }

    private async Task<AnalysisReport> CreateReportAsync(Guid repositoryId, FindingSeverity severity)
    {
        var report = new AnalysisReport(repositoryId);
        report.MarkInProgress();
        report.Complete("Analysis complete.", 1, severity == FindingSeverity.Critical ? 1 : 0);

        var finding = new AnalysisFinding(
            report.Id,
            severity,
            "Security",
            "Test finding",
            "src/Program.cs",
            42);

        DbContext.AnalysisReports.Add(report);
        DbContext.AnalysisFindings.Add(finding);
        await DbContext.SaveChangesAsync();

        return report;
    }
}