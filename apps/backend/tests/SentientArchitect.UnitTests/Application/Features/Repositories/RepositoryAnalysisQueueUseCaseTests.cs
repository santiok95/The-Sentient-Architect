using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Application.Features.Repositories.EnqueueRepositoryAnalysis;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.UnitTests.Common;

namespace SentientArchitect.UnitTests.Application.Features.Repositories;

public class RepositoryAnalysisQueueUseCaseTests : TestBase
{
    private readonly IAnalysisProgressReporter _progressReporter = Substitute.For<IAnalysisProgressReporter>();

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRepositoryBelongsToAnotherUser()
    {
        var ownerUserId = Guid.NewGuid();
        var repository = await CreateRepositoryAsync(ownerUserId);
        var useCase = new EnqueueRepositoryAnalysisUseCase(DbContext, _progressReporter);

        var result = await useCase.ExecuteAsync(
            new EnqueueRepositoryAnalysisRequest(repository.Id, Guid.NewGuid()));

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
        await _progressReporter.DidNotReceiveWithAnyArgs()
            .ReportProgressAsync(default, default, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreatePendingReport_WhenRepositoryBelongsToCurrentUser()
    {
        var ownerUserId = Guid.NewGuid();
        var repository = await CreateRepositoryAsync(ownerUserId);
        var useCase = new EnqueueRepositoryAnalysisUseCase(DbContext, _progressReporter);

        var result = await useCase.ExecuteAsync(
            new EnqueueRepositoryAnalysisRequest(repository.Id, ownerUserId));

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);

        var queuedReport = await DbContext.AnalysisReports.FirstAsync(r => r.Id == result.Data);
        queuedReport.RepositoryInfoId.Should().Be(repository.Id);
        queuedReport.Status.Should().Be(AnalysisStatus.Pending);

        await _progressReporter.Received(1)
            .ReportProgressAsync(repository.Id, 0, "Analisis en cola...", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReuseExistingActiveReport_WhenPendingReportAlreadyExists()
    {
        var ownerUserId = Guid.NewGuid();
        var repository = await CreateRepositoryAsync(ownerUserId);
        var existingReport = new AnalysisReport(repository.Id);

        DbContext.AnalysisReports.Add(existingReport);
        await DbContext.SaveChangesAsync();

        var useCase = new EnqueueRepositoryAnalysisUseCase(DbContext, _progressReporter);

        var result = await useCase.ExecuteAsync(
            new EnqueueRepositoryAnalysisRequest(repository.Id, ownerUserId));

        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(existingReport.Id);

        var reportCount = await DbContext.AnalysisReports.CountAsync(r => r.RepositoryInfoId == repository.Id);
        reportCount.Should().Be(1);

        await _progressReporter.DidNotReceiveWithAnyArgs()
            .ReportProgressAsync(default, default, default!, default);
    }

    private async Task<RepositoryInfo> CreateRepositoryAsync(Guid ownerUserId)
    {
        var repository = new RepositoryInfo(
            ownerUserId,
            ownerUserId,
            $"https://github.com/{ownerUserId:N}/queued-repo",
            RepositoryTrust.Internal);

        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        return repository;
    }
}