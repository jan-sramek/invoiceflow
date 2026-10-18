using FluentValidation;
using InvoiceFlow.Application.Common;

namespace InvoiceFlow.Application.Invoices.GetInvoiceAudit;

internal sealed class GetInvoiceAuditValidator : AbstractValidator<GetInvoiceAuditQuery>
{
    public GetInvoiceAuditValidator()
    {
        RuleFor(q => q.Page).InRange("page", 1, 10_000);
        RuleFor(q => q.PageSize).InRange("pageSize", 1, GetInvoiceAuditQuery.MaxPageSize);
    }
}
