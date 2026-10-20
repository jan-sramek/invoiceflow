using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Audit;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure.Messaging;
using InvoiceFlow.Infrastructure.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Persistence.Tenant;

internal sealed class TenantDbContext(DbContextOptions<TenantDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public const string TenantIdProperty = "TenantId";

    public TenantId TenantId { get; } = tenantContext.TenantId;

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<TenantSettings> Settings => Set<TenantSettings>();

    public DbSet<IdempotencyRecord> IdempotencyKeys => Set<IdempotencyRecord>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Without a connection string, <see cref="Tenancy.TenantConnectionInterceptor"/> routes the connection.
    /// </summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string? connectionString = null) =>
        (connectionString is null ? options.UseNpgsql() : options.UseNpgsql(connectionString))
            .UseSnakeCaseNamingConvention();

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
        // Trigram indexes for searching with ILIKE '%term%'
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration());

        modelBuilder.Entity<TenantSettings>(settings =>
        {
            settings.ToTable("tenant_settings");
            settings.HasKey(s => s.TenantId);
        });

        modelBuilder.Entity<IdempotencyRecord>(idempotency =>
        {
            idempotency.ToTable("idempotency_keys");
            idempotency.HasKey(r => new { r.Key, r.UserId });
            idempotency.Property(r => r.Key).HasMaxLength(128);
            idempotency.Property(r => r.UserId).HasMaxLength(100);
            idempotency.Property(r => r.RequestHash).HasMaxLength(64);
            idempotency.HasIndex(r => r.CreatedAt);
        });

        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable("outbox_messages");
            outbox.HasKey(m => m.Id);
            outbox.Property(m => m.Type).HasMaxLength(100);
            outbox.Property(m => m.Payload).HasColumnType("jsonb");
            outbox.Property(m => m.Headers).HasColumnType("jsonb");
            outbox.HasIndex(m => m.CreatedAt)
                .HasFilter("published_at IS NULL")
                .HasDatabaseName("ix_outbox_messages_unpublished");
        });

        // Second line of defence: the database already belongs to one tenant.
        modelBuilder.Entity<Invoice>().HasQueryFilter(i => EF.Property<TenantId>(i, TenantIdProperty) == TenantId);
        modelBuilder.Entity<AuditEntry>().HasQueryFilter(e => e.TenantId == TenantId);
    }
}
