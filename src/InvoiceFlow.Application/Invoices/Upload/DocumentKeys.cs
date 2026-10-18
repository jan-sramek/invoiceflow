using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Application.Invoices.Upload;

/// <summary>
/// The tenant prefix keeps every tenant's documents apart and lets one tenant's be listed or removed together.
/// </summary>
internal static class DocumentKeys
{
    public static string For(TenantId tenant, Guid documentId) => $"tenants/{tenant.Value:D}/documents/{documentId:D}";
}
