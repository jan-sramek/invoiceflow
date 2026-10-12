using System.ComponentModel.DataAnnotations;

namespace InvoiceFlow.Infrastructure.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    /// <summary>
    /// Directory where the <c>invoiceflow-tenant-databases</c> Secret is mounted, one file per tenant.
    /// </summary>
    [Required]
    public string CredentialsPath { get; set; } = "/var/run/secrets/invoiceflow/tenant-databases";

    /// <summary>
    /// Kept small: every tenant gets its own pool in every replica.
    /// </summary>
    [Range(1, 100)]
    public int MaxPoolSize { get; set; } = 10;

    /// <summary>
    /// How long a tenant's catalog entry and credentials are reused before they are read again.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The tenant every request acts for until tenants come from access tokens.
    /// </summary>
    public Guid? DevelopmentTenantId { get; set; }
}
