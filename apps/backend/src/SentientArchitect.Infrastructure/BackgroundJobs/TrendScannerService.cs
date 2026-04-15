using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.Infrastructure.BackgroundJobs;

public sealed class TrendScannerService(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<TrendScannerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = configuration.GetValue<int>("AI:TrendScanner:IntervalHours", 6);
        var interval = TimeSpan.FromHours(intervalHours);

        logger.LogInformation("TrendScannerService started. Scan interval: {IntervalHours}h.", intervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunScanCycleAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("TrendScannerService stopped.");
    }

    private async Task RunScanCycleAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting trend scan cycle at {Time}.", DateTimeOffset.UtcNow);

        try
        {
            using var scope = services.CreateScope();
            var scanner = scope.ServiceProvider.GetRequiredService<ITrendScanner>();
            await scanner.ScanAsync(ct);
            logger.LogInformation("Trend scan cycle completed successfully at {Time}.", DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Trend scan cycle was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Trend scan cycle failed. The service will retry at the next interval.");
        }
    }
}
