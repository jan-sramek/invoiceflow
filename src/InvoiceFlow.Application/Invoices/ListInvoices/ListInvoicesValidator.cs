using FluentValidation;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Domain.Common;

namespace InvoiceFlow.Application.Invoices.ListInvoices;

internal sealed class ListInvoicesValidator : AbstractValidator<ListInvoicesQuery>
{
    public ListInvoicesValidator()
    {
        RuleFor(query => query.InvoiceDateTo)
            .NotBefore(query => query.InvoiceDateFrom, "invoiceDateTo", "invoiceDateFrom");
        RuleFor(query => query.DueDateTo).NotBefore(query => query.DueDateFrom, "dueDateTo", "dueDateFrom");
        RuleFor(query => query.TotalAmountTo)
            .NotBefore(query => query.TotalAmountFrom, "totalAmountTo", "totalAmountFrom");
        RuleFor(query => query.Currency).MeetsDomainRules(CurrencyCode.Create);
        RuleFor(query => query.Search).MaxLength("search", ListInvoicesQuery.MaxSearchLength);
        RuleFor(query => query.Sort).OneOf(sort => InvoiceSort.TryParse(sort, out _), InvoiceSort.Names, "sort");
        RuleFor(query => query.Page).InRange("page", 1, ListInvoicesQuery.MaxPage);
        RuleFor(query => query.PageSize).InRange("pageSize", 1, ListInvoicesQuery.MaxPageSize);
    }
}
