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
    public const int MaxErrorLength = 2000;

    public required TenantId TenantId { get; init; }

    public required DatabaseLocation Location { get; set; }

    public required string Host { get; set; }

    public required int Port { get; set; }

    public required string DatabaseName { get; set; }

    public required SslMode SslMode { get; set; }

    public required string CredentialsKey { get; set; }

    /// <summary>
    /// The login the application connects as; the migrator grants it data access after every run.
    /// </summary>
    public required string RuntimeRole { get; set; }

    /// <summary>
    /// The last applied migration. Migration ids start with a timestamp, so they sort in order.
    /// </summary>
    public string? SchemaVersion { get; set; }

    public DateTimeOffset? LastMigratedAt { get; set; }

    /// <summary>
    /// Why the last run failed; cleared by the next successful one.
    /// </summary>
    public string? LastMigrationError { get; set; }
}
