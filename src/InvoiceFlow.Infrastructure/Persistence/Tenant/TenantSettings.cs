using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Infrastructure.Persistence.Tenant;

/// <summary>
/// The single row that says which tenant owns the database. Written once, when the database is provisioned.
/// </summary>
internal sealed class TenantSettings
{
    public required TenantId TenantId { get; init; }
}
