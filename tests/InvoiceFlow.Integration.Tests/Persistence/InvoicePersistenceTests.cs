using System.Diagnostics;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Audit;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace InvoiceFlow.Integration.Tests.Persistence;

public sealed class InvoicePersistenceTests(PostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly Actor Clerk = Actor.User("clerk-1", "Clara Clerk");
    private static readonly Actor Approver = Actor.User("approver-1", "Adam Approver");

    private static readonly InvoiceFields Fields = new()
    {
        SupplierName = SupplierName.Create("Acme s.r.o."),
        InvoiceNumber = InvoiceNumber.Create("FV-2026-0117"),
        InvoiceDate = new DateOnly(2026, 10, 1),
        DueDate = new DateOnly(2026, 10, 31),
        Currency = CurrencyCode.Create("CZK"),
        Subtotal = 1000.00m,
        TaxAmount = 210.00m,
        TotalAmount = 1210.00m,
        PurchaseOrderReference = PurchaseOrderReference.Create("PO-77"),
    };

    private TestTenant _testTenant = null!;

    private TenantId Tenant => _testTenant.Id;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _testTenant = await database.CreateTenantAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Commit_PaidInvoice_LoadsWithEveryValue()
    {
        Invoice invoice = PaidInvoice();
        await AddAsync(invoice);

        Invoice? loaded = await GetAsync(invoice.Id);

        loaded.ShouldNotBeNull();
        loaded.Status.ShouldBe(InvoiceStatus.Paid);
        loaded.Fields.ShouldBe(Fields);
        loaded.UploadedBy.ShouldBe(Clerk);
        loaded.UploadedAt.ShouldBe(Start);
        loaded.SubmittedBy.ShouldBe(Clerk);
        loaded.PaidOn.ShouldBe(new DateOnly(2026, 10, 12));
        loaded.PaymentReference.ShouldBe("BANK-42");
        loaded.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Commit_NewInvoice_LoadsWithoutOptionalValues()
    {
        var invoice = Invoice.Upload(Clerk, Start);
        await AddAsync(invoice);

        Invoice? loaded = await GetAsync(invoice.Id);

        loaded.ShouldNotBeNull();
        loaded.Fields.ShouldBe(InvoiceFields.Empty);
        loaded.SubmittedBy.ShouldBeNull();
        loaded.PaidOn.ShouldBeNull();
    }

    [Fact]
    public async Task Commit_ChangedInvoice_SavesChanges()
    {
        var invoice = Invoice.Upload(Clerk, Start);
        invoice.EnterManually(Clerk, Start);
        invoice.UpdateFields(Clerk, Start, Fields);
        await AddAsync(invoice);
        InvoiceFields edited = Fields with { DueDate = null, TotalAmount = 1250m };

        await using (AsyncServiceScope scope = database.OpenScope(Tenant))
        {
            Invoice loaded = (await Repository(scope).GetAsync(invoice.Id, CancellationToken)).ShouldNotBeNull();
            loaded.UpdateFields(Clerk, Start.AddMinutes(1), edited);
            await UnitOfWork(scope).CommitAsync(CancellationToken);
        }

        (await GetAsync(invoice.Id)).ShouldNotBeNull().Fields.ShouldBe(edited);
    }

    [Fact]
    public async Task Commit_ClearsDomainEvents()
    {
        var invoice = Invoice.Upload(Clerk, Start);

        await AddAsync(invoice);

        invoice.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Commit_WritesOneAuditEntryPerEventInTheSameOrder()
    {
        Invoice invoice = PaidInvoice();

        await AddAsync(invoice);

        IReadOnlyList<AuditRow> rows = await AuditRowsAsync(invoice.Id);
        rows.Select(r => r.Action).ShouldBe(
        [
            AuditActions.InvoiceUploaded,
            AuditActions.InvoiceManualEntryStarted,
            AuditActions.InvoiceFieldsCorrected,
            AuditActions.InvoiceSubmitted,
            AuditActions.InvoiceApproved,
            AuditActions.InvoicePaid,
        ]);
        rows.ShouldAllBe(r => r.TenantId == Tenant.Value);
        rows[4].ActorId.ShouldBe(Approver.Id);
        rows[4].ActorName.ShouldBe(Approver.Name);
        rows[2].Changes.ShouldContain("\"supplierName\"");
        rows[3].Changes.ShouldContain("\"PendingApproval\"");
    }

    [Fact]
    public async Task Commit_WithinTrace_StoresTraceIdAsCorrelationId()
    {
        using Activity activity = new Activity("test").Start();
        var invoice = Invoice.Upload(Clerk, Start);

        await AddAsync(invoice);

        (await AuditRowsAsync(invoice.Id)).ShouldHaveSingleItem().CorrelationId.ShouldBe(activity.TraceId.ToHexString());
    }

    [Fact]
    public async Task Commit_ReasonOfRejection_IsStored()
    {
        var invoice = Invoice.Upload(Clerk, Start);
        invoice.EnterManually(Clerk, Start);
        invoice.UpdateFields(Clerk, Start, Fields);
        invoice.Submit(Clerk, Start.AddMinutes(1));
        invoice.Reject(Approver, Start.AddMinutes(2), "Wrong supplier");

        await AddAsync(invoice);

        (await AuditRowsAsync(invoice.Id))[^1].Reason.ShouldBe("Wrong supplier");
    }

    [Fact]
    public async Task Commit_InvoiceChangedMeanwhile_ThrowsAndWritesNoAudit()
    {
        var invoice = Invoice.Upload(Clerk, Start);
        invoice.EnterManually(Clerk, Start);
        invoice.UpdateFields(Clerk, Start, Fields);
        await AddAsync(invoice);

        await using AsyncServiceScope first = database.OpenScope(Tenant);
        await using AsyncServiceScope second = database.OpenScope(Tenant);
        Invoice firstCopy = (await Repository(first).GetAsync(invoice.Id, CancellationToken)).ShouldNotBeNull();
        Invoice secondCopy = (await Repository(second).GetAsync(invoice.Id, CancellationToken)).ShouldNotBeNull();

        firstCopy.Cancel(Clerk, Start.AddMinutes(1), "Duplicate");
        await UnitOfWork(first).CommitAsync(CancellationToken);
        secondCopy.Submit(Clerk, Start.AddMinutes(2));
        ConcurrencyException exception =
            await Should.ThrowAsync<ConcurrencyException>(() => UnitOfWork(second).CommitAsync(CancellationToken));

        exception.Code.ShouldBe(ApplicationErrors.Concurrency.StaleVersion);
        (await GetAsync(invoice.Id)).ShouldNotBeNull().Status.ShouldBe(InvoiceStatus.Cancelled);
        (await AuditRowsAsync(invoice.Id)).Select(r => r.Action).ShouldNotContain(AuditActions.InvoiceSubmitted);
    }

    [Fact]
    public async Task Get_InvoiceOfAnotherTenant_ReturnsNothing()
    {
        var invoice = Invoice.Upload(Clerk, Start);
        await AddAsync(invoice);

        await using AsyncServiceScope otherTenant = database.OpenScope((await database.CreateTenantAsync()).Id);

        (await Repository(otherTenant).GetAsync(invoice.Id, CancellationToken)).ShouldBeNull();
        TenantDbContext context = otherTenant.ServiceProvider.GetRequiredService<TenantDbContext>();
        (await context.AuditEntries.AnyAsync(e => e.SubjectId == invoice.Id.Value, CancellationToken)).ShouldBeFalse();
    }

    private static Invoice PaidInvoice()
    {
        var invoice = Invoice.Upload(Clerk, Start);
        invoice.EnterManually(Clerk, Start.AddMinutes(1));
        invoice.UpdateFields(Clerk, Start.AddMinutes(2), Fields);
        invoice.Submit(Clerk, Start.AddMinutes(3));
        invoice.Approve(Approver, Start.AddMinutes(4));
        invoice.MarkPaid(Approver, Start.AddMinutes(5), new DateOnly(2026, 10, 12), "BANK-42");
        return invoice;
    }

    private static IInvoiceRepository Repository(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();

    private static IUnitOfWork UnitOfWork(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    private async Task AddAsync(Invoice invoice)
    {
        await using AsyncServiceScope scope = database.OpenScope(Tenant);
        Repository(scope).Add(invoice);
        await UnitOfWork(scope).CommitAsync(CancellationToken);
    }

    private async Task<Invoice?> GetAsync(InvoiceId id)
    {
        await using AsyncServiceScope scope = database.OpenScope(Tenant);
        return await Repository(scope).GetAsync(id, CancellationToken);
    }

    // Plain SQL, so the test checks what is stored rather than what EF Core maps back.
    private async Task<IReadOnlyList<AuditRow>> AuditRowsAsync(InvoiceId id)
    {
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString(_testTenant.Database));
        await using NpgsqlCommand command = dataSource.CreateCommand(
            """
            SELECT tenant_id, action, actor_id, actor_name, changes::text, reason, correlation_id
            FROM audit_events
            WHERE subject_type = 'invoice' AND subject_id = $1
            ORDER BY occurred_at
            """);
        command.Parameters.Add(new NpgsqlParameter { Value = id.Value });
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(CancellationToken);

        List<AuditRow> rows = [];
        while (await reader.ReadAsync(CancellationToken))
        {
            rows.Add(new AuditRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return rows;
    }

    private sealed record AuditRow(
        Guid TenantId,
        string Action,
        string ActorId,
        string ActorName,
        string Changes,
        string? Reason,
        string? CorrelationId);
}
