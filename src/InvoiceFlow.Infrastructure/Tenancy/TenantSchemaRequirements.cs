using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Tenancy;

/// <summary>
/// The migration id the running binary expects every active tenant database to have applied.
/// </summary>
internal static class TenantSchemaRequirements
{
    private static readonly Lazy<string> Latest = new(ReadLatestMigrationId);

    public static string LatestMigrationId => Latest.Value;

    private static string ReadLatestMigrationId()
    {
        DbContextOptionsBuilder<TenantDbContext> options = new();
        TenantDbContext.Configure(options, "Host=127.0.0.1;Database=unused;Username=unused;Password=unused");
        using var context = new TenantDbContext(options.Options, new FixedTenantContext(default));
        return context.Database.GetMigrations().Last();
    }
}
