using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace InvoiceFlow.Integration.Tests.Persistence;

public sealed class TenantRoutingTests(PostgresFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly Actor Clerk = Actor.User("clerk-1", "Clara Clerk");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Commit_TwoTenants_StoresEachInvoiceInItsTenantsDatabase()
    {
        TestTenant first = await database.CreateTenantAsync();
        TestTenant second = await database.CreateTenantAsync();

        await AddInvoiceAsync(first.Id);
        await AddInvoiceAsync(first.Id);
        await AddInvoiceAsync(second.Id);

        (await CountInvoicesAsync(first.Database)).ShouldBe(2);
        (await CountInvoicesAsync(second.Database)).ShouldBe(1);
    }

    [Fact]
    public async Task Open_CatalogPointsAtAnotherTenantsDatabase_FailsLoudly()
    {
        TestTenant owner = await database.CreateTenantAsync();
        TestTenant misrouted = await database.CreateTenantAsync();
        await ChangeDatabaseAsync(misrouted.Id, d => d.SetProperty(x => x.DatabaseName, owner.Database));

        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(() => GetInvoiceAsync(misrouted.Id));

        exception.Message.ShouldContain($"does not belong to tenant {misrouted.Id}");
    }

    [Fact]
    public async Task Open_SuspendedTenant_IsUnavailable()
    {
        TestTenant tenant = await database.CreateTenantAsync();
        await database.UpdateCatalogAsync(catalog => catalog.Tenants
            .Where(t => t.Id == tenant.Id)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.Status, TenantStatus.Suspended), CancellationToken));

        TenantUnavailableException exception =
            await Should.ThrowAsync<TenantUnavailableException>(() => GetInvoiceAsync(tenant.Id));

        exception.Code.ShouldBe(ApplicationErrors.Tenant.Unavailable);
    }

    [Fact]
    public async Task Open_UnknownTenant_IsUnavailable()
    {
        await Should.ThrowAsync<TenantUnavailableException>(() => GetInvoiceAsync(new TenantId(Guid.CreateVersion7())));
    }

    [Fact]
    public async Task Open_ServerUnreachable_IsUnavailable()
    {
        TestTenant tenant = await database.CreateTenantAsync();
        await ChangeDatabaseAsync(tenant.Id, d => d.SetProperty(x => x.Port, 1));

        TenantUnavailableException exception =
            await Should.ThrowAsync<TenantUnavailableException>(() => GetInvoiceAsync(tenant.Id));

        exception.InnerException.ShouldBeAssignableTo<NpgsqlException>();
    }

    [Fact]
    public async Task Open_CredentialsMissing_IsUnavailable()
    {
        TestTenant tenant = await database.CreateTenantAsync();
        await ChangeDatabaseAsync(tenant.Id, d => d.SetProperty(x => x.CredentialsKey, "not-mounted"));

        await Should.ThrowAsync<TenantUnavailableException>(() => GetInvoiceAsync(tenant.Id));
    }

    [Fact]
    public async Task Open_Synchronously_IsRefused()
    {
        TestTenant tenant = await database.CreateTenantAsync();
        await using AsyncServiceScope scope = database.OpenScope(tenant.Id);
        TenantDbContext context = scope.ServiceProvider.GetRequiredService<TenantDbContext>();

        Should.Throw<InvalidOperationException>(() => context.Invoices.ToList())
            .Message.ShouldContain("asynchronously");
    }

    [Fact]
    public async Task Query_RowsOfAnotherTenantInTheSameDatabase_AreHidden()
    {
        TestTenant tenant = await database.CreateTenantAsync();
        var intruder = new TenantId(Guid.CreateVersion7());
        var invoice = Invoice.Upload(Clerk, Now, InvoiceFlow.Testing.Invoices.TestDocuments.Create(Now));

        await using (TenantDbContext unrouted = database.OpenUnrouted(tenant.Database, intruder))
        {
            unrouted.Invoices.Add(invoice).Property(TenantDbContext.TenantIdProperty).CurrentValue = intruder;
            await unrouted.SaveChangesAsync(CancellationToken);
        }

        (await CountInvoicesAsync(tenant.Database)).ShouldBe(1);
        (await GetInvoiceAsync(tenant.Id, invoice.Id)).ShouldBeNull();
    }

    private Task ChangeDatabaseAsync(
        TenantId tenantId,
        Action<UpdateSettersBuilder<TenantDatabase>> setters) =>
        database.UpdateCatalogAsync(catalog => catalog.TenantDatabases
            .Where(d => d.TenantId == tenantId)
            .ExecuteUpdateAsync(setters, CancellationToken));

    private async Task AddInvoiceAsync(TenantId tenantId)
    {
        await using AsyncServiceScope scope = database.OpenScope(tenantId);
        scope.ServiceProvider.GetRequiredService<IInvoiceRepository>().Add(Invoice.Upload(Clerk, Now, InvoiceFlow.Testing.Invoices.TestDocuments.Create(Now)));
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken);
    }

    private async Task<Invoice?> GetInvoiceAsync(TenantId tenantId, InvoiceId? id = null)
    {
        await using AsyncServiceScope scope = database.OpenScope(tenantId);
        return await scope.ServiceProvider.GetRequiredService<IInvoiceRepository>()
            .GetAsync(id ?? new InvoiceId(Guid.CreateVersion7()), CancellationToken);
    }

    private async Task<long> CountInvoicesAsync(string databaseName)
    {
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString(databaseName));
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT count(*) FROM invoices");
        return (long)(await command.ExecuteScalarAsync(CancellationToken))!;
    }
}
