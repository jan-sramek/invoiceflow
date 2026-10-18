using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using InvoiceFlow.Application.Audit;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using InvoiceFlow.Integration.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Integration.Tests.Api;

public sealed class InvoiceLifecycleTests(PostgresFixture database) : InvoiceApiTests(database)
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    // The same users the development personas sign in as.
    private static readonly Actor Clerk = Actor.User("dev-clerk", "Clara Clerk");
    private static readonly Actor Approver = Actor.User("dev-approver", "Adam Approver");

    [Fact]
    [Trait("Requirement", "FR-APR-02")]
    public async Task Approve_InvoiceSubmittedBySomeoneElse_IsApproved()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(PendingApproval());

        HttpResponseMessage response = await PostAsync(id, "approve", etag, persona: "approver");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull().ShouldNotBe(etag);
        (await JsonAsync(response)).GetProperty("status").GetString().ShouldBe("Approved");
    }

    [Theory]
    [Trait("Requirement", "BR-07")]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Decision_OnOwnInvoice_IsForbidden(string action)
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(PendingApproval());
        object? body = action == "reject" ? new { reason = "Wrong supplier" } : null;

        HttpResponseMessage response = await PostAsync(id, action, etag, body, persona: "clerk");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe("approval.own_invoice");
        (await GetAsync(id)).Headers.ETag.ShouldBe(etag);
    }

    [Fact]
    [Trait("Requirement", "FR-APR-03")]
    public async Task Reject_WithReason_KeepsTrimmedReasonInHistory()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(PendingApproval());

        HttpResponseMessage response = await PostAsync(
            id, "reject", etag, new { reason = "  Wrong supplier  " }, persona: "approver");

        (await JsonAsync(response)).GetProperty("status").GetString().ShouldBe("Rejected");
        AuditEntry entry = await LastAuditEntryAsync(id);
        entry.Reason.ShouldBe("Wrong supplier");
        entry.Actor.Id.ShouldBe("dev-approver");
    }

    [Fact]
    [Trait("Requirement", "FR-APR-03")]
    public async Task Reject_WithoutReason_ReportsField()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(PendingApproval());

        HttpResponseMessage response = await PostAsync(id, "reject", etag, new { }, persona: "approver");
        JsonElement problem = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.GetProperty("code").GetString().ShouldBe("validation.failed");
        JsonElement error = problem.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("pointer").GetString().ShouldBe("/reason");
        error.GetProperty("code").GetString().ShouldBe("validation.required");
    }

    [Fact]
    [Trait("Requirement", "FR-APR-03")]
    public async Task Reopen_RejectedInvoice_ReturnsToReview()
    {
        Invoice invoice = PendingApproval();
        invoice.Reject(Approver, Start.AddMinutes(5), "Wrong supplier");
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(invoice);

        HttpResponseMessage response = await PostAsync(id, "reopen", etag);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("status").GetString().ShouldBe("NeedsReview");
    }

    [Fact]
    [Trait("Requirement", "FR-PAY-01")]
    public async Task MarkPaid_ApprovedInvoice_RecordsPayment()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(Approved());

        HttpResponseMessage response = await PostAsync(
            id, "mark-paid", etag, new { paidOn = "2026-10-12", paymentReference = " BANK-42 " }, persona: "finance");
        JsonElement body = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("status").GetString().ShouldBe("Paid");
        body.GetProperty("paidOn").GetString().ShouldBe("2026-10-12");
        body.GetProperty("paymentReference").GetString().ShouldBe("BANK-42");
    }

    [Fact]
    [Trait("Requirement", "FR-PAY-01")]
    public async Task MarkPaid_DateInFuture_IsRefused()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(Approved());
        DateOnly dayAfterTomorrow = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime).AddDays(2);

        HttpResponseMessage response = await PostAsync(
            id, "mark-paid", etag, new { paidOn = dayAfterTomorrow, paymentReference = "BANK-42" }, persona: "finance");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe("invoice.payment_date_in_future");
        (await GetAsync(id)).Headers.ETag.ShouldBe(etag);
    }

    [Fact]
    public async Task MarkPaid_MissingValues_ReportsEveryField()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(Approved());

        HttpResponseMessage response = await PostAsync(id, "mark-paid", etag, new { }, persona: "finance");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").EnumerateArray()
            .Select(e => (e.GetProperty("pointer").GetString(), e.GetProperty("code").GetString()))
            .ShouldBe(
            [
                ("/paidOn", "validation.required"),
                ("/paymentReference", "validation.required"),
            ]);
    }

    [Fact]
    [Trait("Requirement", "FR-CAN-01")]
    public async Task Cancel_InvoiceAwaitingApproval_KeepsReasonInHistory()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(PendingApproval());

        HttpResponseMessage response = await PostAsync(id, "cancel", etag, new { reason = "Sent twice" });

        (await JsonAsync(response)).GetProperty("status").GetString().ShouldBe("Cancelled");
        (await LastAuditEntryAsync(id)).Reason.ShouldBe("Sent twice");
    }

    [Fact]
    [Trait("Requirement", "FR-CAN-01")]
    public async Task Cancel_ApprovedInvoice_ReturnsInvalidTransition()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(Approved());

        HttpResponseMessage response = await PostAsync(id, "cancel", etag, new { reason = "Sent twice" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe("invoice.invalid_transition");
    }

    private static Invoice PendingApproval()
    {
        var invoice = Invoice.Upload(Clerk, Start, InvoiceFlow.Testing.Invoices.TestDocuments.Create(Start));
        invoice.EnterManually(Clerk, Start.AddMinutes(1));
        invoice.UpdateFields(Clerk, Start.AddMinutes(2), new InvoiceFields
        {
            SupplierName = SupplierName.Create("Acme s.r.o."),
            InvoiceNumber = InvoiceNumber.Create("FV-2026-0117"),
            InvoiceDate = new DateOnly(2026, 10, 1),
            DueDate = new DateOnly(2026, 10, 31),
            Currency = CurrencyCode.Create("CZK"),
            Subtotal = 1000.00m,
            TaxAmount = 210.00m,
            TotalAmount = 1210.00m,
        });
        invoice.Submit(Clerk, Start.AddMinutes(3));
        return invoice;
    }

    private static Invoice Approved()
    {
        Invoice invoice = PendingApproval();
        invoice.Approve(Approver, Start.AddMinutes(4));
        return invoice;
    }

    private async Task<AuditEntry> LastAuditEntryAsync(InvoiceId id)
    {
        await using AsyncServiceScope scope = Database.OpenScope(Tenant.Id);
        return await scope.ServiceProvider.GetRequiredService<TenantDbContext>().AuditEntries
            .Where(e => e.SubjectId == id.Value)
            .OrderByDescending(e => e.OccurredAt)
            .FirstAsync(CancellationToken);
    }
}
