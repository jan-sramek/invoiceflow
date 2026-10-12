using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Infrastructure.Tenancy;

/// <summary>
/// Acts for one known tenant outside a request or message, for example while migrating its database.
/// </summary>
internal sealed class FixedTenantContext(TenantId tenantId) : ITenantContext
{
    public TenantId TenantId { get; } = tenantId;
}
