using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvoiceFlow.Infrastructure.Persistence.Tenant;

/// <summary>
/// Lets <c>dotnet ef</c> create migrations without a database or a running host.
/// </summary>
internal sealed class TenantDbContextDesignFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>();
        TenantDbContext.Configure(options, "Host=localhost;Database=design_time");

        return new TenantDbContext(options.Options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ITenantContext
    {
        public TenantId TenantId => default;
    }
}
