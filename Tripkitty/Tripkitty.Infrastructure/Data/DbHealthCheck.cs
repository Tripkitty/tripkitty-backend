using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tripkitty.Infrastructure.Data;

namespace Tripkitty.Infrastructure.Data;

public class DbHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the database");
    }
}
