using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Infrastructure.Persistence.Catalog;

internal enum TenantStatus
{
    Provisioning,
    Active,
    Suspended,
}

internal sealed class CatalogTenant
{
    public const int MaxNameLength = 200;
    public const int MaxSlugLength = 63;

    public required TenantId Id { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public required TenantStatus Status { get; set; }

    public required DateTimeOffset CreatedAt { get; init; }

    public TenantDatabase? Database { get; set; }
}
