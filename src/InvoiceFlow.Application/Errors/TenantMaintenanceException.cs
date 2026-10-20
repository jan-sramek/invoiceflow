using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

/// <summary>
/// The tenant's schema is missing, behind, or last migration failed. Other tenants are not affected.
/// </summary>
public sealed class TenantMaintenanceException()
    : InvoiceFlowException(ApplicationErrors.Tenant.Maintenance, ErrorKind.Unavailable);
