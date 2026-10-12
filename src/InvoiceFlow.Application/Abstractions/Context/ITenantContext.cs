using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Application.Abstractions.Context;

public interface ITenantContext
{
    TenantId TenantId { get; }
}
