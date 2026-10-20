using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Audit;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure.Messaging;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using InvoiceFlow.Infrastructure.Tenancy;
using InvoiceFlow.Testing.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Integration.Tests.Persistence;

public sealed class ProcessingSweeperTests(PostgresFixture database) : IAsyncLifetime
{
    private static readonly Actor Clerk = Actor.User("clerk-1", "Clara Clerk");
    private static readonly Actor Worker = Actor.System("invoiceflow-worker");

    private TestTenant _tenant = null!;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _tenant = await database.CreateTenantAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SweepTenant_StuckProcessing_WritesOutboxRow()
    {
        DateTimeOffset now = TimeProvider.System.GetUtcNow();
        var invoice = Invoice.Upload(Clerk, now, TestDocuments.Create(now));
        invoice.StartProcessing(Worker, now);
        await AddAsync(invoice);
        await BackdateProcessingAuditAsync(invoice.Id, now.AddHours(-2));

        ProcessingSweeper sweeper = CreateSweeper(stuckAfter: TimeSpan.FromMinutes(30));
        int count = await sweeper.SweepTenantAsync(_tenant.Id, CancellationToken);

        count.ShouldBe(1);
        await using AsyncServiceScope scope = database.OpenScope(_tenant.Id);
        List<OutboxMessage> outbox = await scope.ServiceProvider.GetRequiredService<TenantDbContext>()
            .OutboxMessages
            .Where(m => m.PublishedAt == null)
            .ToListAsync(CancellationToken);
        outbox.Count.ShouldBe(1);
        outbox[0].Payload.ShouldContain(invoice.Id.Value.ToString("D"));
    }

    [Fact]
    public async Task SweepTenant_RecentProcessing_DoesNotRequeue()
    {
        DateTimeOffset now = TimeProvider.System.GetUtcNow();
        var invoice = Invoice.Upload(Clerk, now, TestDocuments.Create(now));
        invoice.StartProcessing(Worker, now);
        await AddAsync(invoice);

        ProcessingSweeper sweeper = CreateSweeper(stuckAfter: TimeSpan.FromMinutes(30));
        int count = await sweeper.SweepTenantAsync(_tenant.Id, CancellationToken);

        count.ShouldBe(0);
    }

    [Fact]
    public async Task SweepTenant_ExistingUnpublishedOutbox_DoesNotDuplicate()
    {
        DateTimeOffset now = TimeProvider.System.GetUtcNow();
        var invoice = Invoice.Upload(Clerk, now, TestDocuments.Create(now));
        invoice.StartProcessing(Worker, now);
        await AddAsync(invoice);
        await BackdateProcessingAuditAsync(invoice.Id, now.AddHours(-2));

        await using (AsyncServiceScope scope = database.OpenScope(_tenant.Id))
        {
            TenantDbContext db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.CreateVersion7(now),
                Type = OutboxMessage.InvoiceUploadedType,
                Payload = $$"""{"tenantId":"{{_tenant.Id.Value:D}}","invoiceId":"{{invoice.Id.Value:D}}"}""",
                CreatedAt = now,
            });
            await db.SaveChangesAsync(CancellationToken);
        }

        ProcessingSweeper sweeper = CreateSweeper(stuckAfter: TimeSpan.FromMinutes(30));
        int count = await sweeper.SweepTenantAsync(_tenant.Id, CancellationToken);

        count.ShouldBe(0);
    }

    private ProcessingSweeper CreateSweeper(TimeSpan stuckAfter) =>
        new(
            database.Services.GetRequiredService<IDbContextFactory<CatalogDbContext>>(),
            database.Services.GetRequiredService<TenantConnectionResolver>(),
            Options.Create(new ProcessingSweeperOptions { StuckAfter = stuckAfter, BatchSize = 50 }),
            Options.Create(new RabbitMqOptions { Host = "localhost", VirtualHost = "/", UserName = "x", Password = "x" }),
            TimeProvider.System,
            NullLoggerFactory.Instance,
            NullLogger<ProcessingSweeper>.Instance);

    private async Task AddAsync(Invoice invoice)
    {
        await using AsyncServiceScope scope = database.OpenScope(_tenant.Id);
        scope.ServiceProvider.GetRequiredService<IInvoiceRepository>().Add(invoice);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken);
    }

    private async Task BackdateProcessingAuditAsync(InvoiceId invoiceId, DateTimeOffset occurredAt)
    {
        await using AsyncServiceScope scope = database.OpenScope(_tenant.Id);
        TenantDbContext db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
             UPDATE audit_events
             SET occurred_at = {occurredAt}
             WHERE subject_id = {invoiceId.Value}
               AND action = {AuditActions.InvoiceProcessingStarted}
             """,
            CancellationToken);
    }
}
