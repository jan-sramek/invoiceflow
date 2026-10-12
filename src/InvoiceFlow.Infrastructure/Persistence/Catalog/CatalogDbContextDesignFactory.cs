using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvoiceFlow.Infrastructure.Persistence.Catalog;

/// <summary>
/// Lets <c>dotnet ef</c> create migrations without a database or a running host.
/// </summary>
internal sealed class CatalogDbContextDesignFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>();
        CatalogDbContext.Configure(options, "Host=localhost;Database=design_time");

        return new CatalogDbContext(options.Options);
    }
}
