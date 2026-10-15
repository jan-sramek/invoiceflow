using System.Net;
using System.Text.Json;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Integration.Tests.Persistence;

namespace InvoiceFlow.Integration.Tests.Api;

public sealed class InvoiceListTests(PostgresFixture database) : InvoiceApiTests(database)
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly InvoiceDate = new(2026, 10, 1);
    private static readonly DateOnly PastDueDate = new(2026, 10, 5);
    private static readonly Actor Clerk = Actor.User("clerk-1", "Clara Clerk");
    private static readonly Actor Approver = Actor.User("approver-1", "Adam Approver");

    [Fact]
    [Trait("Requirement", "FR-LST-01")]
    [Trait("Requirement", "FR-CAN-02")]
    public async Task List_ByDefault_HidesCancelledAndShowsNewestUploadFirst()
    {
        Invoice older = Draft(minute: 1);
        Invoice newer = Draft(minute: 2);
        Invoice cancelled = Draft(minute: 3);
        cancelled.Cancel(Clerk, Start.AddMinutes(4), "Sent twice");
        await SeedAllAsync(older, newer, cancelled);

        JsonElement body = await ListAsync("");

        Ids(body).ShouldBe([newer.Id.Value, older.Id.Value]);
        body.GetProperty("totalCount").GetInt32().ShouldBe(2);
        body.GetProperty("page").GetInt32().ShouldBe(1);
        body.GetProperty("pageSize").GetInt32().ShouldBe(25);
    }

    [Fact]
    [Trait("Requirement", "FR-CAN-02")]
    public async Task List_StatusFilter_MatchesAnyGivenStatusIncludingCancelled()
    {
        var uploaded = Invoice.Upload(Clerk, Start);
        Invoice inReview = Draft(minute: 1);
        Invoice cancelled = Draft(minute: 2);
        cancelled.Cancel(Clerk, Start.AddMinutes(3), "Sent twice");
        await SeedAllAsync(uploaded, inReview, cancelled);

        JsonElement body = await ListAsync("status=Cancelled&status=Uploaded");

        Ids(body).ShouldBe([cancelled.Id.Value, uploaded.Id.Value]);
    }

    [Fact]
    [Trait("Requirement", "FR-LST-01")]
    public async Task List_Page_ReturnsThatSliceAndTheTotal()
    {
        Invoice[] invoices = [.. Enumerable.Range(1, 5).Select(minute => Draft(minute))];
        await SeedAllAsync(invoices);

        JsonElement body = await ListAsync("sort=uploadedAt&page=2&pageSize=2");

        Ids(body).ShouldBe([invoices[2].Id.Value, invoices[3].Id.Value]);
        body.GetProperty("totalCount").GetInt32().ShouldBe(5);
        body.GetProperty("page").GetInt32().ShouldBe(2);
        body.GetProperty("pageSize").GetInt32().ShouldBe(2);
    }

    [Theory]
    [Trait("Requirement", "FR-LST-03")]
    [InlineData("acme", new[] { 0, 1 })]
    [InlineData("GLOB", new[] { 2 })]
    [InlineData("a_b", new[] { 3 })]
    [InlineData("%", new int[0])]
    public async Task List_Search_MatchesPartOfSupplierOrNumberInAnyCase(string search, int[] expected)
    {
        Invoice[] invoices =
        [
            Draft(minute: 1, supplier: "Acme s.r.o.", number: "FV-1"),
            Draft(minute: 2, supplier: "Initech", number: "ACME-7"),
            Draft(minute: 3, supplier: "Globex", number: "FV-2"),
            Draft(minute: 4, supplier: "A_B Trading", number: "FV-3"),
            Draft(minute: 5, supplier: "AxB Trading", number: "FV-4"),
        ];
        await SeedAllAsync(invoices);

        JsonElement body = await ListAsync($"search={Uri.EscapeDataString(search)}&sort=uploadedAt");

        Ids(body).ShouldBe([.. expected.Select(index => invoices[index].Id.Value)]);
    }

    [Fact]
    [Trait("Requirement", "FR-LST-02")]
    public async Task List_Filters_AllApplyTogether()
    {
        Invoice match = Draft(minute: 1, total: 500m, invoiceDate: new DateOnly(2026, 10, 2));
        Invoice tooSmall = Draft(minute: 2, total: 100m, invoiceDate: new DateOnly(2026, 10, 2));
        Invoice otherCurrency = Draft(minute: 3, total: 500m, currency: "EUR", invoiceDate: new DateOnly(2026, 10, 2));
        Invoice tooEarly = Draft(minute: 4, total: 500m, invoiceDate: new DateOnly(2026, 9, 1));
        await SeedAllAsync(match, tooSmall, otherCurrency, tooEarly);

        JsonElement body = await ListAsync(
            "totalAmountFrom=200&totalAmountTo=500&currency=czk&invoiceDateFrom=2026-10-01&invoiceDateTo=2026-10-02");

        Ids(body).ShouldBe([match.Id.Value]);
    }

    [Fact]
    [Trait("Requirement", "FR-PAY-02")]
    [Trait("Requirement", "BR-02")]
    public async Task List_Overdue_OnlyApprovedInvoicesPastTheirDueDate()
    {
        Invoice overdue = Approved(Draft(minute: 1, dueDate: PastDueDate));
        Invoice notDueYet = Approved(Draft(minute: 2, dueDate: Today().AddDays(30)));
        Invoice withoutDueDate = Approved(Draft(minute: 3));
        Invoice notApproved = Draft(minute: 4, dueDate: PastDueDate);
        await SeedAllAsync(overdue, notDueYet, withoutDueDate, notApproved);

        JsonElement filtered = await ListAsync("overdue=true");
        JsonElement all = await ListAsync("sort=uploadedAt");

        Ids(filtered).ShouldBe([overdue.Id.Value]);
        all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("isOverdue").GetBoolean())
            .ShouldBe([true, false, false, false]);
    }

    [Theory]
    [Trait("Requirement", "FR-LST-01")]
    [InlineData("dueDate", new[] { 1, 2, 0 })]
    [InlineData("-dueDate", new[] { 2, 1, 0 })]
    public async Task List_SortByDueDate_PutsMissingDatesLast(string sort, int[] expected)
    {
        Invoice[] invoices =
        [
            Draft(minute: 1),
            Draft(minute: 2, dueDate: new DateOnly(2026, 10, 20)),
            Draft(minute: 3, dueDate: new DateOnly(2026, 11, 20)),
        ];
        await SeedAllAsync(invoices);

        JsonElement body = await ListAsync($"sort={sort}");

        Ids(body).ShouldBe([.. expected.Select(index => invoices[index].Id.Value)]);
    }

    [Fact]
    [Trait("Requirement", "FR-LST-01")]
    public async Task List_SortByStatus_FollowsTheLifecycle()
    {
        Invoice approved = Approved(Draft(minute: 1));
        Invoice inReview = Draft(minute: 2);
        var uploaded = Invoice.Upload(Clerk, Start.AddMinutes(3));
        await SeedAllAsync(approved, inReview, uploaded);

        JsonElement body = await ListAsync("sort=status");

        Ids(body).ShouldBe([uploaded.Id.Value, inReview.Id.Value, approved.Id.Value]);
    }

    [Fact]
    public async Task List_InvalidParameters_ReportsEveryOne()
    {
        HttpResponseMessage response = await Client.GetAsync(
            "/api/v1/invoices?invoiceDateFrom=2026-10-10&invoiceDateTo=2026-10-01&currency=XYZ&sort=amount&pageSize=500",
            CancellationToken);
        JsonElement problem = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.GetProperty("code").GetString().ShouldBe("validation.failed");
        problem.GetProperty("errors").EnumerateArray()
            .Select(e => (e.GetProperty("pointer").GetString(), e.GetProperty("code").GetString()))
            .ShouldBe(
            [
                ("/invoiceDateTo", "validation.range_reversed"),
                ("/currency", "validation.currency_invalid"),
                ("/sort", "validation.not_allowed"),
                ("/pageSize", "validation.out_of_range"),
            ]);
    }

    [Fact]
    public async Task List_UnreadableValues_ReportsInvalidFormat()
    {
        HttpResponseMessage response = await Client.GetAsync("/api/v1/invoices?page=first&status=Lost", CancellationToken);
        JsonElement problem = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.GetProperty("errors").EnumerateArray()
            .Select(e => (e.GetProperty("pointer").GetString(), e.GetProperty("code").GetString()))
            .ShouldBe([("/page", "validation.invalid_format"), ("/status", "validation.invalid_format")], ignoreOrder: true);
    }

    [Fact]
    [Trait("Requirement", "NFR-SEC-03")]
    public async Task List_InvoicesOfAnotherTenant_AreNeverListed()
    {
        TestTenant other = await Database.CreateTenantAsync();
        await AddAsync(other.Id, Draft(minute: 1));
        Invoice own = Draft(minute: 2);
        await SeedAllAsync(own);

        JsonElement body = await ListAsync("");

        Ids(body).ShouldBe([own.Id.Value]);
        body.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    private static Invoice Draft(
        int minute,
        string supplier = "Acme s.r.o.",
        string number = "FV-2026-0117",
        decimal total = 1210m,
        string currency = "CZK",
        DateOnly? invoiceDate = null,
        DateOnly? dueDate = null)
    {
        DateTimeOffset at = Start.AddMinutes(minute);
        var invoice = Invoice.Upload(Clerk, at);
        invoice.EnterManually(Clerk, at);
        invoice.UpdateFields(Clerk, at, new InvoiceFields
        {
            SupplierName = SupplierName.Create(supplier),
            InvoiceNumber = InvoiceNumber.Create(number),
            InvoiceDate = invoiceDate ?? InvoiceDate,
            DueDate = dueDate,
            Currency = CurrencyCode.Create(currency),
            TotalAmount = total,
        });
        return invoice;
    }

    private static Invoice Approved(Invoice invoice)
    {
        invoice.Submit(Clerk, Start.AddHours(1));
        invoice.Approve(Approver, Start.AddHours(2));
        return invoice;
    }

    private static DateOnly Today() => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    private async Task SeedAllAsync(params Invoice[] invoices)
    {
        foreach (Invoice invoice in invoices)
        {
            await AddAsync(Tenant.Id, invoice);
        }
    }

    private async Task<JsonElement> ListAsync(string query)
    {
        HttpResponseMessage response = await Client.GetAsync($"/api/v1/invoices?{query}", CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await JsonAsync(response);
    }

    private static Guid[] Ids(JsonElement body) =>
        [.. body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
}
