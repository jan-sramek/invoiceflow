using FluentValidation.Results;
using InvoiceFlow.Application.Invoices.ListInvoices;

namespace InvoiceFlow.Application.Tests.Invoices;

public class ListInvoicesValidatorTests
{
    [Fact]
    public void Validate_Defaults_AreValid()
    {
        new ListInvoicesValidator().Validate(new ListInvoicesQuery()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(ListInvoicesQuery.MaxPage + 1, 25)]
    [InlineData(1, 0)]
    [InlineData(1, ListInvoicesQuery.MaxPageSize + 1)]
    public void Validate_PagingOutOfRange_ReportsLimits(int page, int pageSize)
    {
        ValidationResult result = new ListInvoicesValidator().Validate(new ListInvoicesQuery { Page = page, PageSize = pageSize });

        ValidationFailure failure = result.Errors.ShouldHaveSingleItem();
        failure.ErrorCode.ShouldBe("validation.out_of_range");
        ((IReadOnlyDictionary<string, object?>)failure.CustomState).Keys.ShouldBe(["field", "min", "max"]);
    }

    [Fact]
    public void Validate_RangesThatEndBeforeTheyStart_ReportsEachEnd()
    {
        var query = new ListInvoicesQuery
        {
            InvoiceDateFrom = new DateOnly(2026, 10, 2),
            InvoiceDateTo = new DateOnly(2026, 10, 1),
            DueDateFrom = new DateOnly(2026, 10, 2),
            DueDateTo = new DateOnly(2026, 10, 1),
            TotalAmountFrom = 10m,
            TotalAmountTo = 9.99m,
        };

        ValidationResult result = new ListInvoicesValidator().Validate(query);

        result.Errors.Select(e => (e.PropertyName, e.ErrorCode)).ShouldBe(
        [
            ("InvoiceDateTo", "validation.range_reversed"),
            ("DueDateTo", "validation.range_reversed"),
            ("TotalAmountTo", "validation.range_reversed"),
        ]);
        result.Errors[0].CustomState.ShouldBe(new Dictionary<string, object?>
        {
            ["field"] = "invoiceDateTo",
            ["from"] = "invoiceDateFrom",
        });
    }

    [Fact]
    public void Validate_SingleDayRange_IsValid()
    {
        var day = new DateOnly(2026, 10, 1);

        new ListInvoicesValidator().Validate(new ListInvoicesQuery { InvoiceDateFrom = day, InvoiceDateTo = day })
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_UnknownSort_ListsAllowedNames()
    {
        ValidationResult result = new ListInvoicesValidator().Validate(new ListInvoicesQuery { Sort = "-amount" });

        ValidationFailure failure = result.Errors.ShouldHaveSingleItem();
        failure.ErrorCode.ShouldBe("validation.not_allowed");
        ((IReadOnlyDictionary<string, object?>)failure.CustomState)["allowed"].ShouldBe(InvoiceSort.Names);
    }

    [Fact]
    public void Validate_InvalidCurrencyAndLongSearch_ReportsDomainCodes()
    {
        var query = new ListInvoicesQuery
        {
            Currency = "XYZ",
            Search = new string('a', ListInvoicesQuery.MaxSearchLength + 1),
        };

        ValidationResult result = new ListInvoicesValidator().Validate(query);

        result.Errors.Select(e => (e.PropertyName, e.ErrorCode)).ShouldBe(
        [
            ("Currency", "validation.currency_invalid"),
            ("Search", "validation.max_length"),
        ]);
    }
}
