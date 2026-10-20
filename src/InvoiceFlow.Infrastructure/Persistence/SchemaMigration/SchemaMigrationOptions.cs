using System.ComponentModel.DataAnnotations;

namespace InvoiceFlow.Infrastructure.Persistence.SchemaMigration;

public sealed class SchemaMigrationOptions
{
    public const string SectionName = "SchemaMigration";

    /// <summary>
    /// Directory where the <c>invoiceflow-tenant-migrations</c> Secret is mounted, with the same keys as the
    /// runtime one. Only the migration Job mounts it.
    /// </summary>
    [Required]
    public string CredentialsPath { get; set; } = "/var/run/secrets/invoiceflow/tenant-migrations";

    /// <summary>
    /// PEM CA file for on-premises tenant databases that require certificate verification.
    /// </summary>
    public string? RootCertificatePath { get; set; }
}
