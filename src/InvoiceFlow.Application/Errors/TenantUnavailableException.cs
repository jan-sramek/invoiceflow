using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

/// <summary>
/// The tenant's database cannot be used right now. Other tenants are not affected.
/// </summary>
public sealed class TenantUnavailableException(Exception? innerException = null)
    : InvoiceFlowException(ApplicationErrors.Tenant.Unavailable, ErrorKind.Unavailable, innerException: innerException);
