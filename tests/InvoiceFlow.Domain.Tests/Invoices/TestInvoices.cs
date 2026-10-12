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

    /// <summary>
    /// An invoice uploaded by the clerk and moved to <paramref name="status"/>, with its events cleared.
    /// </summary>
    public static Invoice InStatus(InvoiceStatus status)
    {
        var invoice = Invoice.Upload(Clerk, Now);

        foreach (InvoiceAction action in PathFromUploaded[status])
        {
            Perform(invoice, action);
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
}
