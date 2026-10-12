using InvoiceFlow.Domain.Tenants;
using Npgsql;

namespace InvoiceFlow.Infrastructure.Persistence.Catalog;

internal enum DatabaseLocation
{
    Managed,
    OnPremises,
}

/// <summary>
/// Where a tenant's database lives. Credentials are not stored here, only the key of the tenant's
/// entry in the mounted <c>invoiceflow-tenant-databases</c> Secret.
/// </summary>
internal sealed class TenantDatabase
{
    public required TenantId TenantId { get; init; }

    public required DatabaseLocation Location { get; set; }

    public required string Host { get; set; }

    public required int Port { get; set; }

    public required string DatabaseName { get; set; }

    public required SslMode SslMode { get; set; }

    public required string CredentialsKey { get; set; }
}
