using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Errors;
using InvoiceFlow.Domain.Invoices;
using static InvoiceFlow.Domain.Tests.Invoices.TestInvoices;

namespace InvoiceFlow.Domain.Tests.Invoices;

public class InvoiceTests
{
    [Fact]
    public void Upload_NewInvoice_IsUploadedAndRaisesUploaded()
    {
        var invoice = Invoice.Upload(Clerk, Now);

        invoice.Status.ShouldBe(InvoiceStatus.Uploaded);
        invoice.UploadedBy.ShouldBe(Clerk);
        invoice.UploadedAt.ShouldBe(Now);
        invoice.Id.Value.Version.ShouldBe(7);
        invoice.DomainEvents.ShouldHaveSingleItem().ShouldBe(new InvoiceUploaded(invoice.Id, Clerk, Now));
    }

    [Fact]
    public void Upload_TwoInvoices_GetDifferentIds()
    {
        Invoice.Upload(Clerk, Now).Id.ShouldNotBe(Invoice.Upload(Clerk, Now).Id);
    }

    [Fact]
    public void Upload_NewInvoice_HasNoFields()
    {
        Invoice.Upload(Clerk, Now).Fields.ShouldBe(InvoiceFields.Empty);
    }

    [Fact]
    public void UpdateFields_ChangedFields_StoresThemAndRaisesChanges()
    {
        Invoice invoice = InStatus(InvoiceStatus.NeedsReview);
        InvoiceFields edited = ValidFields with { TotalAmount = 1250m, DueDate = null };

        invoice.UpdateFields(Clerk, Now, edited);

        invoice.Fields.ShouldBe(edited);
        InvoiceFieldsCorrected corrected = invoice.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<InvoiceFieldsCorrected>();
        corrected.InvoiceId.ShouldBe(invoice.Id);
        corrected.Actor.ShouldBe(Clerk);
        corrected.Changes.ShouldBe(
        [
            new FieldChange(InvoiceField.DueDate, "2026-10-31", null),
            new FieldChange(InvoiceField.TotalAmount, "1210.00", "1250.00"),
        ]);
    }

    [Fact]
    public void UpdateFields_NothingChanged_RaisesNothing()
    {
        Invoice invoice = InStatus(InvoiceStatus.Extracted);

        invoice.UpdateFields(Clerk, Now, ValidFields with { });

        invoice.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateFields_DraftThatCannotBeSubmitted_IsSaved()
    {
        Invoice invoice = InStatus(InvoiceStatus.NeedsReview);
        InvoiceFields draft = InvoiceFields.Empty with { TotalAmount = 0m };

        invoice.UpdateFields(Clerk, Now, draft);

        invoice.Fields.ShouldBe(draft);
    }

    [Fact]
    public void Submit_RecordsSubmitter()
    {
        Invoice invoice = InStatus(InvoiceStatus.NeedsReview);

        invoice.Submit(Clerk, Now);

        invoice.SubmittedBy.ShouldBe(Clerk);
    }

    [Fact]
    [Trait("Rule", "BR-11")]
    public void Submit_FieldsWithIssues_ThrowsWithIssuesAndChangesNothing()
    {
        Invoice invoice = InStatus(InvoiceStatus.NeedsReview);
        invoice.UpdateFields(Clerk, Now, ValidFields with { SupplierName = null, Subtotal = null, TotalAmount = 0m });
        invoice.ClearDomainEvents();

        InvoiceNotReadyException exception = Should.Throw<InvoiceNotReadyException>(() => invoice.Submit(Clerk, Now));

        exception.Code.ShouldBe(DomainErrors.Invoice.NotReadyForSubmission);
        exception.Kind.ShouldBe(ErrorKind.BusinessRule);
        exception.Issues.ShouldBe(
        [
            new FieldIssue(InvoiceField.SupplierName, DomainErrors.Validation.Required),
            new FieldIssue(InvoiceField.TotalAmount, DomainErrors.Validation.MustBePositive),
        ]);
        invoice.Status.ShouldBe(InvoiceStatus.NeedsReview);
        invoice.SubmittedBy.ShouldBeNull();
        invoice.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "BR-05")]
    public void CheckReadiness_InvoiceDatedTodayInEarliestTimeZone_IsReady()
    {
        Invoice invoice = InStatus(InvoiceStatus.NeedsReview);
        invoice.UpdateFields(Clerk, Now, ValidFields with { InvoiceDate = new DateOnly(2026, 10, 13), DueDate = null });

        invoice.CheckReadiness(new DateTimeOffset(2026, 10, 12, 11, 0, 0, TimeSpan.Zero)).ShouldBeEmpty();
        invoice.CheckReadiness(new DateTimeOffset(2026, 10, 12, 9, 30, 0, TimeSpan.Zero)).ShouldBe(
            [new FieldIssue(InvoiceField.InvoiceDate, DomainErrors.Validation.DateInFuture)]);
    }

    [Fact]
    public void ClearDomainEvents_RemovesRaisedEvents()
    {
        var invoice = Invoice.Upload(Clerk, Now);

        invoice.ClearDomainEvents();

        invoice.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void FailExtraction_RecordsTrimmedReason()
    {
        Invoice invoice = InStatus(InvoiceStatus.Processing);

        invoice.FailExtraction(Worker, Now, "  No text layer  ");

        invoice.DomainEvents.ShouldHaveSingleItem().ShouldBe(new InvoiceExtractionFailed(
            invoice.Id, Worker, Now, InvoiceStatus.Processing, InvoiceStatus.ExtractionFailed, "No text layer"));
    }

    [Fact]
    [Trait("Rule", "BR-09")]
    public void Reject_RecordsTrimmedReason()
    {
        Invoice invoice = InStatus(InvoiceStatus.PendingApproval);

        invoice.Reject(Approver, Now, "  Wrong supplier ");

        invoice.DomainEvents.ShouldHaveSingleItem().ShouldBe(new InvoiceRejected(
            invoice.Id, Approver, Now, InvoiceStatus.PendingApproval, InvoiceStatus.Rejected, "Wrong supplier"));
    }

    [Fact]
    [Trait("Rule", "BR-09")]
    public void Cancel_RecordsTrimmedReason()
    {
        Invoice invoice = InStatus(InvoiceStatus.Rejected);

        invoice.Cancel(Clerk, Now, " Duplicate of INV-17 ");

        invoice.DomainEvents.ShouldHaveSingleItem().ShouldBe(new InvoiceCancelled(
            invoice.Id, Clerk, Now, InvoiceStatus.Rejected, InvoiceStatus.Cancelled, "Duplicate of INV-17"));
    }

    [Theory]
    [Trait("Rule", "BR-09")]
    [InlineData(InvoiceAction.Reject, null)]
    [InlineData(InvoiceAction.Reject, "")]
    [InlineData(InvoiceAction.Reject, "   ")]
    [InlineData(InvoiceAction.Cancel, null)]
    [InlineData(InvoiceAction.Cancel, "")]
    [InlineData(InvoiceAction.Cancel, "   ")]
    [InlineData(InvoiceAction.FailExtraction, "")]
    public void ActionWithReason_MissingReason_ThrowsAndChangesNothing(InvoiceAction action, string? reason)
    {
        Invoice invoice = InStatus(StatusAllowing(action));

        DomainValidationException exception =
            Should.Throw<DomainValidationException>(() => PerformWithReason(invoice, action, reason!));

        exception.Code.ShouldBe(DomainErrors.Validation.Required);
        exception.Params["field"].ShouldBe("reason");
        invoice.Status.ShouldBe(StatusAllowing(action));
        invoice.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [Trait("Rule", "BR-09")]
    [InlineData(InvoiceAction.Reject)]
    [InlineData(InvoiceAction.Cancel)]
    [InlineData(InvoiceAction.FailExtraction)]
    public void ActionWithReason_ReasonTooLong_Throws(InvoiceAction action)
    {
        Invoice invoice = InStatus(StatusAllowing(action));
        string reason = new('x', Invoice.MaxReasonLength + 1);

        DomainValidationException exception =
            Should.Throw<DomainValidationException>(() => PerformWithReason(invoice, action, reason));

        exception.Code.ShouldBe(DomainErrors.Validation.MaxLength);
        exception.Params["max"].ShouldBe(Invoice.MaxReasonLength);
        invoice.Status.ShouldBe(StatusAllowing(action));
    }

    [Theory]
    [Trait("Rule", "BR-09")]
    [InlineData(InvoiceAction.Reject)]
    [InlineData(InvoiceAction.Cancel)]
    [InlineData(InvoiceAction.FailExtraction)]
    public void ActionWithReason_ReasonAtMaxLength_Succeeds(InvoiceAction action)
    {
        Invoice invoice = InStatus(StatusAllowing(action));

        PerformWithReason(invoice, action, new string('x', Invoice.MaxReasonLength));

        invoice.Status.ShouldNotBe(StatusAllowing(action));
    }

    [Theory]
    [Trait("Rule", "BR-07")]
    [InlineData(InvoiceAction.Approve)]
    [InlineData(InvoiceAction.Reject)]
    public void Decide_BySubmitter_IsForbidden(InvoiceAction action)
    {
        Invoice invoice = InStatus(InvoiceStatus.PendingApproval);

        ForbiddenException exception = Should.Throw<ForbiddenException>(() => Decide(invoice, action, Clerk));

        exception.Code.ShouldBe(DomainErrors.Approval.OwnInvoice);
        exception.Kind.ShouldBe(ErrorKind.Forbidden);
        invoice.Status.ShouldBe(InvoiceStatus.PendingApproval);
        invoice.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [Trait("Rule", "BR-07")]
    [InlineData(InvoiceAction.Approve)]
    [InlineData(InvoiceAction.Reject)]
    public void Decide_BySubmitterWithChangedDisplayName_IsForbidden(InvoiceAction action)
    {
        Invoice invoice = InStatus(InvoiceStatus.PendingApproval);
        var renamedClerk = Actor.User(Clerk.Id, "Clara Married-Name");

        Should.Throw<ForbiddenException>(() => Decide(invoice, action, renamedClerk));
    }

    [Fact]
    [Trait("Rule", "BR-07")]
    public void Approve_AfterResubmissionByAnotherUser_OriginalSubmitterMayApprove()
    {
        Invoice invoice = InStatus(InvoiceStatus.Rejected);
        invoice.Reopen(Clerk, Now);
        invoice.Submit(Approver, Now);

        invoice.Approve(Clerk, Now);

        invoice.Status.ShouldBe(InvoiceStatus.Approved);
    }

    [Fact]
    [Trait("Rule", "BR-13")]
    public void MarkPaid_StoresPaymentAndRaisesPaid()
    {
        Invoice invoice = InStatus(InvoiceStatus.Approved);

        invoice.MarkPaid(Approver, Now, Today, " BANK-2026-0042 ");

        invoice.PaidOn.ShouldBe(Today);
        invoice.PaymentReference.ShouldBe("BANK-2026-0042");
        invoice.DomainEvents.ShouldHaveSingleItem().ShouldBe(new InvoicePaid(
            invoice.Id, Approver, Now, InvoiceStatus.Approved, InvoiceStatus.Paid, Today, "BANK-2026-0042"));
    }

    [Fact]
    [Trait("Rule", "BR-13")]
    public void MarkPaid_DateAfterTodayEverywhere_ThrowsAndChangesNothing()
    {
        Invoice invoice = InStatus(InvoiceStatus.Approved);
        var nineThirtyUtc = new DateTimeOffset(2026, 10, 12, 9, 30, 0, TimeSpan.Zero);

        BusinessRuleException exception = Should.Throw<BusinessRuleException>(
            () => invoice.MarkPaid(Approver, nineThirtyUtc, new DateOnly(2026, 10, 13), PaymentReference));

        exception.Code.ShouldBe(DomainErrors.Invoice.PaymentDateInFuture);
        exception.Kind.ShouldBe(ErrorKind.BusinessRule);
        invoice.Status.ShouldBe(InvoiceStatus.Approved);
        invoice.PaidOn.ShouldBeNull();
        invoice.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "BR-13")]
    public void MarkPaid_TodayInEarliestTimeZone_IsAccepted()
    {
        Invoice invoice = InStatus(InvoiceStatus.Approved);
        var elevenUtc = new DateTimeOffset(2026, 10, 12, 11, 0, 0, TimeSpan.Zero);

        invoice.MarkPaid(Approver, elevenUtc, new DateOnly(2026, 10, 13), PaymentReference);

        invoice.Status.ShouldBe(InvoiceStatus.Paid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkPaid_MissingPaymentReference_Throws(string reference)
    {
        Invoice invoice = InStatus(InvoiceStatus.Approved);

        DomainValidationException exception =
            Should.Throw<DomainValidationException>(() => invoice.MarkPaid(Approver, Now, Today, reference));

        exception.Code.ShouldBe(DomainErrors.Validation.Required);
        exception.Params["field"].ShouldBe("paymentReference");
        invoice.Status.ShouldBe(InvoiceStatus.Approved);
    }

    [Fact]
    public void MarkPaid_PaymentReferenceTooLong_Throws()
    {
        Invoice invoice = InStatus(InvoiceStatus.Approved);
        string reference = new('x', Invoice.MaxPaymentReferenceLength + 1);

        DomainValidationException exception =
            Should.Throw<DomainValidationException>(() => invoice.MarkPaid(Approver, Now, Today, reference));

        exception.Code.ShouldBe(DomainErrors.Validation.MaxLength);
        exception.Params["max"].ShouldBe(Invoice.MaxPaymentReferenceLength);
    }

    private static InvoiceStatus StatusAllowing(InvoiceAction action) => action switch
    {
        InvoiceAction.FailExtraction => InvoiceStatus.Processing,
        _ => InvoiceStatus.PendingApproval,
    };

    private static void PerformWithReason(Invoice invoice, InvoiceAction action, string reason)
    {
        switch (action)
        {
            case InvoiceAction.Reject:
                invoice.Reject(Approver, Now, reason);
                break;
            case InvoiceAction.Cancel:
                invoice.Cancel(Clerk, Now, reason);
                break;
            case InvoiceAction.FailExtraction:
                invoice.FailExtraction(Worker, Now, reason);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private static void Decide(Invoice invoice, InvoiceAction action, Actor actor)
    {
        if (action == InvoiceAction.Approve)
        {
            invoice.Approve(actor, Now);
        }
        else
        {
            invoice.Reject(actor, Now, Reason);
        }
    }
}
