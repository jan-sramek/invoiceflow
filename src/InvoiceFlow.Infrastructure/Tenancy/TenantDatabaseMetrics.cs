using System.Diagnostics.Metrics;
using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Infrastructure.Tenancy;

/// <summary>
/// Per-tenant database open/fail and circuit-breaker signals for the OpenTelemetry metrics pipeline.
/// </summary>
internal sealed class TenantDatabaseMetrics
{
    public const string MeterName = "InvoiceFlow.Tenancy";

    private readonly Counter<long> _opens;
    private readonly Counter<long> _failures;
    private readonly Counter<long> _breakerRejections;
    private readonly ObservableGauge<int> _breakerOpen;

    private readonly TenantDatabaseResilience _resilience;

    public TenantDatabaseMetrics(IMeterFactory meterFactory, TenantDatabaseResilience resilience)
    {
        _resilience = resilience;
        Meter meter = meterFactory.Create(MeterName);
        _opens = meter.CreateCounter<long>("tenant_db.opens", description: "Successful tenant database opens.");
        _failures = meter.CreateCounter<long>("tenant_db.failures", description: "Failed tenant database opens.");
        _breakerRejections = meter.CreateCounter<long>(
            "tenant_db.breaker_rejections",
            description: "Requests rejected because the per-tenant circuit breaker is open.");
        // Keep the gauge instrument rooted for the process lifetime.
        _breakerOpen = meter.CreateObservableGauge(
            "tenant_db.breaker_open",
            ObserveBreakerOpen,
            description: "1 when the per-tenant circuit breaker is open, otherwise 0.");
        _ = _breakerOpen;
    }

    public void RecordOpen(TenantId tenantId) =>
        _opens.Add(1, KeyValuePair.Create<string, object?>("tenant.id", tenantId.Value.ToString("D")));

    public void RecordFailure(TenantId tenantId) =>
        _failures.Add(1, KeyValuePair.Create<string, object?>("tenant.id", tenantId.Value.ToString("D")));

    public void RecordBreakerRejection(TenantId tenantId) =>
        _breakerRejections.Add(1, KeyValuePair.Create<string, object?>("tenant.id", tenantId.Value.ToString("D")));

    private IEnumerable<Measurement<int>> ObserveBreakerOpen()
    {
        foreach ((Guid tenantId, bool open) in _resilience.BreakerOpenSnapshot())
        {
            yield return new Measurement<int>(
                open ? 1 : 0,
                KeyValuePair.Create<string, object?>("tenant.id", tenantId.ToString("D")));
        }
    }
}
