using InvoiceFlow.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InvoiceFlow.Integration.Tests.Persistence;

public sealed class CatalogHealthCheckTests(PostgresFixture database)
{
    [Fact]
    public async Task Check_CatalogReachable_IsHealthy()
    {
        await using AsyncServiceScope scope = database.OpenScope(default);
        var check = new CatalogHealthCheck(
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<CatalogDbContext>>());
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("catalog", check, HealthStatus.Unhealthy, tags: null),
        };

        HealthCheckResult result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }
}
