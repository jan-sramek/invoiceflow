using InvoiceFlow.Application.Audit;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Application.Tests.Audit;

public class AuditEntryFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly Actor Clerk = Actor.User("clerk-1", "Clara Clerk");
    private static readonly Actor Approver = Actor.User("approver-1", "Adam Approver");
    private static readonly Actor Worker = Actor.System("invoiceflow-worker");
    private static readonly TenantId Tenant = new(Guid.NewGuid());

    private static readonly InvoiceDocument Document = InvoiceDocument.Create(
        Guid.CreateVersion7(), "tenants/t/documents/d", "FV-2026-0117.pdf", 48_213, new string('a', 64));

    private static readonly InvoiceFields Fields = new()
    {
        SupplierName = SupplierName.Create("Acme s.r.o."),
        InvoiceNumber = InvoiceNumber.Create("FV-2026-0117"),
        InvoiceDate = new DateOnly(2026, 10, 1),
        Currency = CurrencyCode.Create("CZK"),
        TotalAmount = 1210.00m,
    };

    [Fact]
    [Trait("Requirement", "NFR-AUD-01")]
    public void Create_EveryDomainEvent_MapsToItsOwnAction()
    {
        IDomainEvent[] events = EveryKindOfEvent();
        Type[] allEventTypes =
        [
            .. typeof(IDomainEvent).Assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IDomainEvent))),
        ];

        events.Select(e => e.GetType()).Distinct().ShouldBe(allEventTypes, ignoreOrder: true);
        events.DistinctBy(e => e.GetType())
            .Select(e => AuditEntryFactory.Create(e, Tenant, null).Action)
            .ShouldBeUnique();
    }

    [Fact]
    public void Create_CopiesEventContext()
    {
        var invoice = Invoice.Upload(Clerk, Now, Document);

        AuditEntry entry = AuditEntryFactory.Create(invoice.DomainEvents[0], Tenant, "4bf92f3577b34da6a3ce929d0e0e4736");

        entry.Id.Version.ShouldBe(7);
        entry.TenantId.ShouldBe(Tenant);
        entry.OccurredAt.ShouldBe(Now);
        entry.Actor.ShouldBe(Clerk);
        entry.Action.ShouldBe(AuditActions.InvoiceUploaded);
        entry.SubjectType.ShouldBe(AuditEntryFactory.InvoiceSubject);
        entry.SubjectId.ShouldBe(invoice.Id.Value);
        entry.Changes.ShouldBe([new AuditChange("fileName", null, "FV-2026-0117.pdf")]);
        entry.Reason.ShouldBeNull();
        entry.CorrelationId.ShouldBe("4bf92f3577b34da6a3ce929d0e0e4736");
    }

    [Fact]
    public void Create_FieldsCorrected_ListsChangedFieldsByApiName()
    {
        Invoice invoice = InNeedsReview();
        invoice.UpdateFields(Clerk, Now, Fields with { TotalAmount = 1250m, PurchaseOrderReference = PurchaseOrderReference.Create("PO-9") });

        AuditEntry entry = AuditEntryFactory.Create(invoice.DomainEvents[^1], Tenant, null);

        entry.Action.ShouldBe(AuditActions.InvoiceFieldsCorrected);
        entry.Changes.ShouldBe(
        [
            new AuditChange("totalAmount", "1210.00", "1250.00"),
            new AuditChange("purchaseOrderReference", null, "PO-9"),
        ]);
    }

    [Fact]
    public void Create_StatusChangeWithReason_RecordsStatusAndReason()
    {
        Invoice invoice = InNeedsReview();
        invoice.Submit(Clerk, Now);
        invoice.Reject(Approver, Now, "Wrong supplier");

        AuditEntry entry = AuditEntryFactory.Create(invoice.DomainEvents[^1], Tenant, null);

        entry.Action.ShouldBe(AuditActions.InvoiceRejected);
        entry.Changes.ShouldBe([new AuditChange("status", "PendingApproval", "Rejected")]);
        entry.Reason.ShouldBe("Wrong supplier");
    }

    [Fact]
    public void Create_Paid_RecordsPaymentDetails()
    {
        Invoice invoice = InNeedsReview();
        invoice.Submit(Clerk, Now);
        invoice.Approve(Approver, Now);
        invoice.MarkPaid(Approver, Now, new DateOnly(2026, 10, 12), "BANK-42");

        AuditEntry entry = AuditEntryFactory.Create(invoice.DomainEvents[^1], Tenant, null);

        entry.Changes.ShouldBe(
        [
            new AuditChange("status", "Approved", "Paid"),
            new AuditChange("paidOn", null, "2026-10-12"),
            new AuditChange("paymentReference", null, "BANK-42"),
        ]);
    }

    private static Invoice InNeedsReview()
    {
        var invoice = Invoice.Upload(Clerk, Now, Document);
        invoice.EnterManually(Clerk, Now);
        invoice.UpdateFields(Clerk, Now, Fields);
        return invoice;
    }

    private static IDomainEvent[] EveryKindOfEvent()
    {
        var main = Invoice.Upload(Clerk, Now, Document);
        main.StartProcessing(Worker, Now);
        main.FailExtraction(Worker, Now, "No text layer");
        main.RetryExtraction(Clerk, Now);
        main.CompleteExtraction(Worker, Now);
        main.UpdateFields(Clerk, Now, Fields);
        main.Submit(Clerk, Now);
        main.Reject(Approver, Now, "Wrong supplier");
        main.Reopen(Clerk, Now);
        main.Submit(Clerk, Now);
        main.Approve(Approver, Now);
        main.MarkPaid(Approver, Now, new DateOnly(2026, 10, 12), "BANK-42");
        main.RecordDownload(Approver, Now);

        var reviewed = Invoice.Upload(Clerk, Now, Document);
        reviewed.StartProcessing(Worker, Now);
        reviewed.RequireReview(Worker, Now);
        reviewed.Cancel(Clerk, Now, "Duplicate");

        var manual = Invoice.Upload(Clerk, Now, Document);
        manual.EnterManually(Clerk, Now);

        return
        [
            .. main.DomainEvents,
            .. reviewed.DomainEvents.Where(e => e is InvoiceReviewRequired or InvoiceCancelled),
            .. manual.DomainEvents.OfType<InvoiceManualEntryStarted>(),
        ];
    }
}
