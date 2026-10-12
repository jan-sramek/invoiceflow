using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InvoiceFlow.Infrastructure.Persistence.Catalog;

/// <summary>
/// Readiness depends on the catalog only, so one tenant's unreachable database never takes a service
/// out of rotation for every tenant.
/// </summary>
internal sealed class CatalogHealthCheck(IDbContextFactory<CatalogDbContext> catalog) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using CatalogDbContext db = await catalog.CreateDbContextAsync(cancellationToken);

        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, "The catalog database is unreachable.");
    }
}
