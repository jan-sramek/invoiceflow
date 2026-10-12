using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Domain.Tests.Invoices;

[Trait("Rule", "BR-01")]
[Trait("Rule", "BR-12")]
public class InvoiceTransitionTests
{
    // Written out separately from the table in Invoice, so a change to either one fails a test.
    private static readonly Dictionary<(InvoiceStatus From, InvoiceAction Action), (InvoiceStatus To, Type Event)> Allowed =
        new()
        {
            [(InvoiceStatus.Uploaded, InvoiceAction.StartProcessing)] = (InvoiceStatus.Processing, typeof(InvoiceProcessingStarted)),
            [(InvoiceStatus.Uploaded, InvoiceAction.EnterManually)] = (InvoiceStatus.NeedsReview, typeof(InvoiceManualEntryStarted)),
            [(InvoiceStatus.Uploaded, InvoiceAction.Cancel)] = (InvoiceStatus.Cancelled, typeof(InvoiceCancelled)),
            [(InvoiceStatus.Processing, InvoiceAction.CompleteExtraction)] = (InvoiceStatus.Extracted, typeof(InvoiceExtracted)),
            [(InvoiceStatus.Processing, InvoiceAction.RequireReview)] = (InvoiceStatus.NeedsReview, typeof(InvoiceReviewRequired)),
            [(InvoiceStatus.Processing, InvoiceAction.FailExtraction)] = (InvoiceStatus.ExtractionFailed, typeof(InvoiceExtractionFailed)),
            [(InvoiceStatus.ExtractionFailed, InvoiceAction.RetryExtraction)] = (InvoiceStatus.Processing, typeof(InvoiceExtractionRetried)),
            [(InvoiceStatus.ExtractionFailed, InvoiceAction.EnterManually)] = (InvoiceStatus.NeedsReview, typeof(InvoiceManualEntryStarted)),
            [(InvoiceStatus.ExtractionFailed, InvoiceAction.Cancel)] = (InvoiceStatus.Cancelled, typeof(InvoiceCancelled)),
            [(InvoiceStatus.Extracted, InvoiceAction.UpdateFields)] = (InvoiceStatus.Extracted, typeof(InvoiceFieldsCorrected)),
            [(InvoiceStatus.Extracted, InvoiceAction.Submit)] = (InvoiceStatus.PendingApproval, typeof(InvoiceSubmitted)),
            [(InvoiceStatus.Extracted, InvoiceAction.Cancel)] = (InvoiceStatus.Cancelled, typeof(InvoiceCancelled)),
            [(InvoiceStatus.NeedsReview, InvoiceAction.UpdateFields)] = (InvoiceStatus.NeedsReview, typeof(InvoiceFieldsCorrected)),
            [(InvoiceStatus.NeedsReview, InvoiceAction.Submit)] = (InvoiceStatus.PendingApproval, typeof(InvoiceSubmitted)),
            [(InvoiceStatus.NeedsReview, InvoiceAction.Cancel)] = (InvoiceStatus.Cancelled, typeof(InvoiceCancelled)),
            [(InvoiceStatus.PendingApproval, InvoiceAction.Approve)] = (InvoiceStatus.Approved, typeof(InvoiceApproved)),
            [(InvoiceStatus.PendingApproval, InvoiceAction.Reject)] = (InvoiceStatus.Rejected, typeof(InvoiceRejected)),
            [(InvoiceStatus.PendingApproval, InvoiceAction.Cancel)] = (InvoiceStatus.Cancelled, typeof(InvoiceCancelled)),
            [(InvoiceStatus.Rejected, InvoiceAction.Reopen)] = (InvoiceStatus.NeedsReview, typeof(InvoiceReopened)),
            [(InvoiceStatus.Rejected, InvoiceAction.Cancel)] = (InvoiceStatus.Cancelled, typeof(InvoiceCancelled)),
            [(InvoiceStatus.Approved, InvoiceAction.MarkPaid)] = (InvoiceStatus.Paid, typeof(InvoicePaid)),
        };

    public static TheoryData<InvoiceStatus, InvoiceAction, InvoiceStatus, Type> AllowedTransitions()
    {
        var data = new TheoryData<InvoiceStatus, InvoiceAction, InvoiceStatus, Type>();

        foreach (((InvoiceStatus from, InvoiceAction action), (InvoiceStatus to, Type eventType)) in Allowed)
        {
            data.Add(from, action, to, eventType);
        }

        return data;
    }

    public static TheoryData<InvoiceStatus, InvoiceAction> NotAllowedTransitions()
    {
        var data = new TheoryData<InvoiceStatus, InvoiceAction>();

        foreach (InvoiceStatus from in Enum.GetValues<InvoiceStatus>())
        {
            foreach (InvoiceAction action in Enum.GetValues<InvoiceAction>().Where(a => !Allowed.ContainsKey((from, a))))
            {
                data.Add(from, action);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void Perform_AllowedAction_MovesToNextStatusAndRaisesEvent(
        InvoiceStatus from, InvoiceAction action, InvoiceStatus to, Type eventType)
    {
        Invoice invoice = TestInvoices.InStatus(from);

        TestInvoices.Perform(invoice, action);

        invoice.Status.ShouldBe(to);
        IDomainEvent domainEvent = invoice.DomainEvents.ShouldHaveSingleItem();
        domainEvent.GetType().ShouldBe(eventType);
        domainEvent.Actor.ShouldBe(TestInvoices.ActorFor(action));
        domainEvent.OccurredAt.ShouldBe(TestInvoices.Now);

        if (domainEvent is InvoiceStatusChanged change)
        {
            change.InvoiceId.ShouldBe(invoice.Id);
            change.From.ShouldBe(from);
            change.To.ShouldBe(to);
        }
    }

    [Theory]
    [MemberData(nameof(NotAllowedTransitions))]
    public void Perform_ActionNotAllowedInStatus_ThrowsAndChangesNothing(InvoiceStatus from, InvoiceAction action)
    {
        Invoice invoice = TestInvoices.InStatus(from);

        InvalidTransitionException exception =
            Should.Throw<InvalidTransitionException>(() => TestInvoices.Perform(invoice, action));

        exception.Code.ShouldBe(DomainErrors.Invoice.InvalidTransition);
        exception.Kind.ShouldBe(ErrorKind.Conflict);
        exception.From.ShouldBe(from);
        exception.Action.ShouldBe(action);
        exception.Params["from"].ShouldBe(from.ToString());
        exception.Params["action"].ShouldBe(action.ToString());
        invoice.Status.ShouldBe(from);
        invoice.DomainEvents.ShouldBeEmpty();
    }
}
