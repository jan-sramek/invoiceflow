using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.UpdateFields;

internal sealed class UpdateInvoiceFieldsHandler(IInvoiceRepository invoices, ICurrentUser user, TimeProvider time)
    : InvoiceCommandHandler<UpdateInvoiceFieldsCommand>(invoices, user, time)
{
    protected override void Apply(
        Invoice invoice, UpdateInvoiceFieldsCommand command, Actor actor, DateTimeOffset now)
    {
        InvoiceFields fields = command.Fields.ToFields(out IReadOnlyList<RequestFieldError> errors);
        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        invoice.UpdateFields(actor, now, fields);
    }
}
