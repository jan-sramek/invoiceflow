using InvoiceFlow.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InvoiceFlow.Infrastructure.Persistence.Catalog;

internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<CatalogTenant> Tenants => Set<CatalogTenant>();

    public DbSet<TenantDatabase> TenantDatabases => Set<TenantDatabase>();

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString).WithoutGssEncryption().ConnectionString)
            .UseSnakeCaseNamingConvention();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<TenantId>().HaveConversion<TenantIdConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogTenant>(tenant =>
        {
            tenant.ToTable("tenants");
            tenant.HasKey(t => t.Id);
            tenant.Property(t => t.Id).ValueGeneratedNever();
            tenant.Property(t => t.Name).HasMaxLength(CatalogTenant.MaxNameLength);
            tenant.Property(t => t.Slug).HasMaxLength(CatalogTenant.MaxSlugLength);
            tenant.HasIndex(t => t.Slug).IsUnique();
            tenant.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
            tenant.HasOne(t => t.Database).WithOne().HasForeignKey<TenantDatabase>(d => d.TenantId);
        });

        modelBuilder.Entity<TenantDatabase>(database =>
        {
            database.ToTable("tenant_databases");
            database.HasKey(d => d.TenantId);
            database.Property(d => d.Location).HasConversion<string>().HasMaxLength(16);
            database.Property(d => d.Host).HasMaxLength(253);
            database.Property(d => d.DatabaseName).HasMaxLength(63);
            database.Property(d => d.SslMode).HasConversion<string>().HasMaxLength(16);
            database.Property(d => d.CredentialsKey).HasMaxLength(253);
            database.Property(d => d.RuntimeRole).HasMaxLength(63);
            database.Property(d => d.SchemaVersion).HasMaxLength(150);
            database.Property(d => d.LastMigrationError).HasMaxLength(TenantDatabase.MaxErrorLength);
        });
    }
}
