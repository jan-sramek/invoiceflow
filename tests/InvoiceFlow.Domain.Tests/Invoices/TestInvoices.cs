using System.Security.Cryptography;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Domain.Tests.Invoices;

internal static class TestInvoices
{
    public const string Reason = "Wrong supplier";
    public const string PaymentReference = "BANK-2026-0042";

    public static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 30, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 12);

    public static readonly Actor Clerk = Actor.User("clerk-1", "Clara Clerk");
    public static readonly Actor Approver = Actor.User("approver-1", "Adam Approver");
    public static readonly Actor Worker = Actor.System("invoiceflow-worker");

    public static readonly InvoiceFields ValidFields = new()
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

    public static readonly InvoiceFields CorrectedFields = ValidFields with
    {
        SupplierName = SupplierName.Create("Acme Czech s.r.o."),
    };

    private static readonly Dictionary<InvoiceStatus, InvoiceAction[]> PathFromUploaded = new()
    {
        [InvoiceStatus.Uploaded] = [],
        [InvoiceStatus.Processing] = [InvoiceAction.StartProcessing],
        [InvoiceStatus.Extracted] = [InvoiceAction.StartProcessing, InvoiceAction.CompleteExtraction],
        [InvoiceStatus.NeedsReview] = [InvoiceAction.EnterManually],
        [InvoiceStatus.ExtractionFailed] = [InvoiceAction.StartProcessing, InvoiceAction.FailExtraction],
        [InvoiceStatus.PendingApproval] = [InvoiceAction.EnterManually, InvoiceAction.Submit],
        [InvoiceStatus.Approved] = [InvoiceAction.EnterManually, InvoiceAction.Submit, InvoiceAction.Approve],
        [InvoiceStatus.Rejected] = [InvoiceAction.EnterManually, InvoiceAction.Submit, InvoiceAction.Reject],
        [InvoiceStatus.Paid] =
            [InvoiceAction.EnterManually, InvoiceAction.Submit, InvoiceAction.Approve, InvoiceAction.MarkPaid],
        [InvoiceStatus.Cancelled] = [InvoiceAction.Cancel],
    };

    public static InvoiceDocument Document(string? sha256 = null, string fileName = "invoice.pdf")
    {
        string hash = sha256 ?? Convert.ToHexString(SHA256.HashData("test-pdf"u8.ToArray())).ToLowerInvariant();
        var id = Guid.CreateVersion7(Now);
        return InvoiceDocument.Create(id, $"tenant/{id:N}", fileName, 1024, hash);
    }

    /// <summary>
    /// An invoice uploaded by the clerk and moved to <paramref name="status"/>, with its events cleared.
    /// It gets <see cref="ValidFields"/> as soon as fields can be edited.
    /// </summary>
    public static Invoice InStatus(InvoiceStatus status)
    {
        var invoice = Invoice.Upload(Clerk, Now, Document());

        foreach (InvoiceAction action in PathFromUploaded[status])
        {
            Perform(invoice, action);
            FillWhenEditable(invoice);
        }

        invoice.ClearDomainEvents();
        return invoice;
    }

    /// <summary>
    /// Performs <paramref name="action"/> with valid arguments by <see cref="ActorFor"/>.
    /// </summary>
    public static void Perform(Invoice invoice, InvoiceAction action)
    {
        Actor actor = ActorFor(action);

        switch (action)
        {
            case InvoiceAction.StartProcessing:
                invoice.StartProcessing(actor, Now);
                break;
            case InvoiceAction.CompleteExtraction:
                invoice.CompleteExtraction(actor, Now);
                break;
            case InvoiceAction.RequireReview:
                invoice.RequireReview(actor, Now);
                break;
            case InvoiceAction.FailExtraction:
                invoice.FailExtraction(actor, Now, Reason);
                break;
            case InvoiceAction.RetryExtraction:
                invoice.RetryExtraction(actor, Now);
                break;
            case InvoiceAction.EnterManually:
                invoice.EnterManually(actor, Now);
                break;
            case InvoiceAction.UpdateFields:
                invoice.UpdateFields(actor, Now, CorrectedFields);
                break;
            case InvoiceAction.Submit:
                invoice.Submit(actor, Now);
                break;
            case InvoiceAction.Approve:
                invoice.Approve(actor, Now);
                break;
            case InvoiceAction.Reject:
                invoice.Reject(actor, Now, Reason);
                break;
            case InvoiceAction.Reopen:
                invoice.Reopen(actor, Now);
                break;
            case InvoiceAction.MarkPaid:
                invoice.MarkPaid(actor, Now, Today, PaymentReference);
                break;
            case InvoiceAction.Cancel:
                invoice.Cancel(actor, Now, Reason);
                break;
            case InvoiceAction.AcknowledgeNotDuplicate:
                invoice.AcknowledgeNotDuplicate(actor, Now, Reason);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    /// <summary>
    /// The worker runs extraction, the approver decides, the clerk does everything else.
    /// </summary>
    public static Actor ActorFor(InvoiceAction action) => action switch
    {
        InvoiceAction.StartProcessing
            or InvoiceAction.CompleteExtraction
            or InvoiceAction.RequireReview
            or InvoiceAction.FailExtraction => Worker,
        InvoiceAction.Approve or InvoiceAction.Reject or InvoiceAction.MarkPaid => Approver,
        _ => Clerk,
    };

    private static void FillWhenEditable(Invoice invoice)
    {
        if (invoice.Status is InvoiceStatus.Extracted or InvoiceStatus.NeedsReview && invoice.Fields == InvoiceFields.Empty)
        {
            invoice.UpdateFields(Clerk, Now, ValidFields);
        }
    }
}
