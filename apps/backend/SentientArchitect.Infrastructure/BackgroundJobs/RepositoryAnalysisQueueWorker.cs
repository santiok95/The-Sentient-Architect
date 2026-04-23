using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Infrastructure.BackgroundJobs;

public sealed class RepositoryAnalysisQueueWorker(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<RepositoryAnalysisQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollIntervalSeconds = Math.Max(1, configuration.GetValue<int>("Guardian:AnalysisQueue:PollIntervalSeconds", 3));
        var maxConcurrentJobs = Math.Max(1, configuration.GetValue<int>("Guardian:AnalysisQueue:MaxConcurrentJobs", 1));
        var pollInterval = TimeSpan.FromSeconds(pollIntervalSeconds);
        var runningJobs = new List<Task>();

        logger.LogInformation(
            "RepositoryAnalysisQueueWorker started. Poll interval: {PollIntervalSeconds}s. Max concurrency: {MaxConcurrentJobs}.",
            pollIntervalSeconds,
            maxConcurrentJobs);

        while (!stoppingToken.IsCancellationRequested)
        {
            runningJobs.RemoveAll(job => job.IsCompleted);

            while (runningJobs.Count < maxConcurrentJobs)
            {
                var claimedJob = await TryClaimNextReportAsync(stoppingToken);
                if (claimedJob is null)
                    break;

                runningJobs.Add(ProcessClaimedReportAsync(claimedJob.Value.RepositoryId, claimedJob.Value.ReportId, stoppingToken));
            }

            try
            {
                await Task.Delay(runningJobs.Count == 0 ? pollInterval : TimeSpan.FromMilliseconds(500), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (runningJobs.Count > 0)
            await Task.WhenAll(runningJobs);

        logger.LogInformation("RepositoryAnalysisQueueWorker stopped.");
    }

    private async Task<(Guid RepositoryId, Guid ReportId)?> TryClaimNextReportAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        while (!ct.IsCancellationRequested)
        {
            var pendingReport = await db.AnalysisReports
                .AsNoTracking()
                .Where(r => r.Status == AnalysisStatus.Pending)
                .OrderBy(r => r.CreatedAt)
                .Select(r => new { r.Id, r.RepositoryInfoId })
                .FirstOrDefaultAsync(ct);

            if (pendingReport is null)
                return null;

            var claimedRows = await db.AnalysisReports
                .Where(r => r.Id == pendingReport.Id && r.Status == AnalysisStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, AnalysisStatus.InProgress)
                    .SetProperty(r => r.CompletedAt, (DateTime?)null)
                    .SetProperty(r => r.ErrorMessage, (string?)null)
                    .SetProperty(r => r.Summary, (string?)null)
                    .SetProperty(r => r.TotalFindings, 0)
                    .SetProperty(r => r.CriticalFindings, 0), ct);

            if (claimedRows == 1)
                return (pendingReport.RepositoryInfoId, pendingReport.Id);
        }

        return null;
    }

    private async Task ProcessClaimedReportAsync(Guid repositoryId, Guid reportId, CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var analyzer = scope.ServiceProvider.GetRequiredService<ICodeAnalyzer>();
            await analyzer.AnalyzeAsync(repositoryId, reportId, ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation(
                "Repository analysis worker cancelled while processing repository {RepositoryId}, report {ReportId}.",
                repositoryId,
                reportId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Repository analysis worker failed while dispatching repository {RepositoryId}, report {ReportId}.",
                repositoryId,
                reportId);
        }
    }
}