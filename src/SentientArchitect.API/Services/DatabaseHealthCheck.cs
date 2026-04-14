using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.API.Services;

/// <summary>
/// Readiness health check: verifies PostgreSQL is reachable by executing a lightweight query.
/// </summary>
public class DatabaseHealthCheck(IApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Lightweight existence check — no data returned, just tests the connection.
            await db.Conversations.AnyAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable.", ex);
        }
    }
}
