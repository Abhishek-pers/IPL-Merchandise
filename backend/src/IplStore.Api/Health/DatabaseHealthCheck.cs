using IplStore.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IplStore.Api.Health;

/// <summary>
/// Readiness probe: the instance only receives traffic (load balancer / Container Apps) once
/// it can reach PostgreSQL. Liveness (/health/live) deliberately does NOT check the DB, so a
/// database outage doesn't make the orchestrator restart every healthy API instance.
/// </summary>
internal sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly StoreDbContext _db;

    public DatabaseHealthCheck(StoreDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL reachable")
                : HealthCheckResult.Unhealthy("PostgreSQL not reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL check failed", ex);
        }
    }
}
