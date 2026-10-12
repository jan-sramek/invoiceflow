using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Audit;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Persistence.Tenant;

internal sealed class TenantDbContext(DbContextOptions<TenantDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public const string TenantIdProperty = "TenantId";

    public TenantId TenantId { get; } = tenantContext.TenantId;

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<TenantId>().HaveConversion<TenantIdConverter>();
        configurationBuilder.Properties<InvoiceId>().HaveConversion<InvoiceIdConverter>();
        configurationBuilder.Properties<CurrencyCode>().HaveConversion<CurrencyCodeConverter>().HaveMaxLength(3);
        configurationBuilder.Properties<SupplierName>()
            .HaveConversion<SupplierNameConverter>().HaveMaxLength(SupplierName.MaxLength);
        configurationBuilder.Properties<InvoiceNumber>()
            .HaveConversion<InvoiceNumberConverter>().HaveMaxLength(InvoiceNumber.MaxLength);
        configurationBuilder.Properties<PurchaseOrderReference>()
            .HaveConversion<PurchaseOrderReferenceConverter>().HaveMaxLength(PurchaseOrderReference.MaxLength);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration());

        // Second line of defence: the database already belongs to one tenant.
        modelBuilder.Entity<Invoice>().HasQueryFilter(i => EF.Property<TenantId>(i, TenantIdProperty) == TenantId);
        modelBuilder.Entity<AuditEntry>().HasQueryFilter(e => e.TenantId == TenantId);
    }
}
