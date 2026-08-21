using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RegulatoryCompliance.Infrastructure.Persistences;

namespace RegulatoryCompliance.Infrastructure.BackgroundJobs;

public sealed class RegulatoryComplianceDbHealthCheck(
    RegulatoryComplianceDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("Regulatory Compliance PostgreSQL is reachable.")
            : HealthCheckResult.Unhealthy("Regulatory Compliance PostgreSQL is unreachable.");
}
